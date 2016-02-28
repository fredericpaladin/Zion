using FileSystemInterface;
using System;

namespace SimpleFS
{
    public class FileSystem : IFileSystem
    {
        #region Events

        public event FileSystemEventHandler LookupCompleted;

        public event FileSystemEventHandler CreateCompleted;

        public event FileSystemEventHandler DeleteCompleted;

        public event FileSystemEventHandler ReadCompleted;

        public event FileSystemEventHandler WriteCompleted;

        protected virtual void OnLookupCompleted(FileSystemEventArgs e)
        {
            if (LookupCompleted != null)
                LookupCompleted(this, e);
        }

        protected virtual void OnCreateCompleted(FileSystemEventArgs e)
        {
            if (CreateCompleted != null)
                CreateCompleted(this, e);
        }

        protected virtual void OnDeleteCompleted(FileSystemEventArgs e)
        {
            if (DeleteCompleted != null)
                DeleteCompleted(this, e);
        }

        protected virtual void OnReadCompleted(FileSystemEventArgs e)
        {
            if (ReadCompleted != null)
                ReadCompleted(this, e);
        }

        protected virtual void OnWriteCompleted(FileSystemEventArgs e)
        {
            if (WriteCompleted != null)
                WriteCompleted(this, e);
        }

        #endregion Events

        private FileAllocationTable _entries;
        private uint _currentEntry;
        private LookupAlgorithm _algorithm;

        public IVolume VolumeManager { get; set; }

        public string Name { get; set; }

        public long Size { get { return _entries.Count * 16; } } // Simulate that each entry is 16 bytes

        public Parameters FileSystemParameters
        {
            get
            {
                Parameters parameters = new Parameters();
                parameters.Add("Lookup Algorithm", typeof(LookupAlgorithm), DisablingMode.AfterFormat, "", LookupAlgorithm.Circular);

                return parameters;
            }
        }

        public void DriverEntry(IVolume volume, params object[] args)
        {
            VolumeManager = volume;

            _algorithm = (LookupAlgorithm)args[0];
            _entries = new FileAllocationTable(VolumeManager.Format.BlocksCount);

            if (_entries.Count == 0)
                throw new FileSystemException("Disk size cannot be zero.");
            
            // Divide the size of the data occupied by the File System by the cluster size:
            // this is how many clusters are occupied by the file system
            uint blocks = (uint)Math.Ceiling((decimal)Size / (decimal)VolumeManager.Format.BlockSize);

            // The first 2 blocks are reserved (like in FAT)
            _entries.CreateEntry(0, "", EntryStatus.Reserved);
            _entries.CreateEntry(1, "", EntryStatus.Reserved);

            // Create root directory at address 2 (the first cluster after the reserved area)
            _entries.CreateEntry(2, "C:", EntryStatus.Reserved);
            VolumeManager.WriteBlock(new byte[0], 2);

            // Write into the disk the area occupied by the file system data structure
            for (uint b = 3; b < blocks + 3; b++)
            {
                VolumeManager.WriteBlock(new byte[0], b);
                _entries.CreateEntry(b, "", EntryStatus.Reserved);
                _currentEntry = b;
            }
        }

        public uint Create(string filename)
        {
            if (_entries.ContainsEntry(filename))
                throw new FileSystemException("File '{0}' already present on the disk.", filename);

            // Get next address
            uint addr = LookupNextFree();

            // Create the entry in the table
            _entries.CreateEntry(addr, filename, EntryStatus.Used);

            // Write the data into the disk
            VolumeManager.WriteBlock(new byte[0], addr, 0);

            // Send notification
            OnCreateCompleted(new FileSystemEventArgs(addr, filename, new byte[0], 1));

            return addr;
        }

        public void Delete(string filename)
        {
            uint firstAddr = GetFileAddress(filename);
            uint addr = firstAddr;
            int totBlocks = 0;

            uint count = 0;
            do
            {
                // "Remove" all the entries until the last one of the chain
                addr = _entries.RemoveEntry(addr);
                totBlocks++;

                // Prevents infinite loops
                count++;
                if (count > _entries.Count)
                    throw new FileSystemException("Error while deleting file '{0}'", filename);
            } while (addr != 0);

            // Send notification
            OnDeleteCompleted(new FileSystemEventArgs(firstAddr, filename, new byte[0], totBlocks));
        }

        public byte[] Read(string filename, long length)
        {
            uint firstAddr = GetFileAddress(filename);
            uint addr = firstAddr;
            byte[] data = new byte[length];
            int bytesRead = 0;

            if (data.Length == 0)
                throw new FileSystemException("File '{0}' has no data to read", filename);

            int totBlocks = 0;
            do
            {
                // Read the content of the entire block
                byte[] block = VolumeManager.ReadBlock(addr);
                int blockLength = length < block.Length ? (int)length : block.Length;

                // Merge the block data into the final byte array
                MergeData(data, block, bytesRead);
                bytesRead += blockLength;

                // Update timestamp
                _entries.UpdateLastAccessTime(firstAddr, DateTime.Now);

                // Get next address
                addr = _entries[addr].NextBlockAddress;

                totBlocks++;
                if (totBlocks > _entries.Count) // Prevents infinite loops
                    throw new FileSystemException("Error while reading file '{0}'", filename);
            } while (addr != 0 && bytesRead < length);

            // Send notification
            OnReadCompleted(new FileSystemEventArgs(firstAddr, filename, data, totBlocks));

            return data;
        }

        public void Write(string filename, byte[] data)
        {
            uint firstAddr = GetFileAddress(filename);
            uint prevAddr = 0;
            uint addr = LookupNextInChain(firstAddr);
            int initFileLength = 0;
            int totSize = 0;
            int totBlocks = 0;

            // If the current address in the chain is not the first block of the file,
            // then get the current file length: it needs to be added the total
            if (addr != firstAddr)
                initFileLength = _entries[firstAddr].FileLength;

            // This is the block of data that will be passed to the Volume Manager
            // If the current block has some data in it, the new data (from the parameter)
            // needs to be merged with this part, and send it to the Volume Manager
            byte[] dataToWrite;
            int remainingBytes;
            if (_entries[addr].BlockLength > 0)
            {
                // The last block already has some data in it: read it and
                // merge it with the new data to write
                byte[] block = VolumeManager.ReadBlock(addr);
                remainingBytes = data.Length + _entries[addr].BlockLength;
                dataToWrite = new byte[remainingBytes];

                // First add the current data and copy to dataToWrite
                MergeData(dataToWrite, block, 0);

                // Then append the new data
                MergeData(dataToWrite, data, _entries[addr].BlockLength);

                // Subtract the length of the current data: the entire block will be rewritten
                if (initFileLength > 0)
                    initFileLength -= _entries[addr].BlockLength;
            }
            else
            {
                // The block is completely empty
                remainingBytes = data.Length;
                dataToWrite = data;
            }

            do
            {
                bool isFirstChunk = remainingBytes == dataToWrite.Length;
                bool isBlockFull = _entries[addr].BlockLength == VolumeManager.Format.BlockSize;
                if ((isFirstChunk && isBlockFull) || !isFirstChunk)
                {
                    // Set status of the previous block (it could be "end of file")
                    if (addr != firstAddr)
                        _entries.UpdateEntryStatus(addr, EntryStatus.NextInChain);

                    // Get next address block
                    addr = LookupNextFree();

                    // Create the entry in the table
                    _entries.CreateEntry(addr, filename, EntryStatus.NextInChain);
                    totBlocks++;
                }

                // Grab only the right amount of data to pass to the Volume Manager
                // (no more than the block size or what's left to write)
                int readAtOffset = dataToWrite.Length - remainingBytes;
                int blockLength = (remainingBytes <= VolumeManager.Format.BlockSize) ? remainingBytes : (int)VolumeManager.Format.BlockSize;
                byte[] dataBlock = SplitData(dataToWrite, blockLength, readAtOffset);

                // Write the data into the disk
                VolumeManager.WriteBlock(dataBlock, addr);
                totSize += blockLength;

                // Update timestamps (only the first entry stores CreationTime)
                DateTime dateTime = DateTime.Now;
                _entries.UpdateLastAccessTime(firstAddr, dateTime);
                _entries.UpdateLastWriteTime(firstAddr, dateTime);

                // Update block length of the current entry
                _entries.UpdateBlockLength(addr, blockLength);

                // Update the "next address" reference in the previous entry
                // This creates the linked-list
                if (remainingBytes < dataToWrite.Length)
                    _entries.UpdateEntryNextAddress(prevAddr, addr);

                // Decrease the amount of data to write
                remainingBytes -= dataBlock.Length;
                prevAddr = addr;
            } while (remainingBytes > 0);

            // Set status of the last block
            if (addr != firstAddr)
                _entries.UpdateEntryStatus(addr, EntryStatus.EndOfFile);

            // Update the total length of the file (only the first block stores this)
            _entries.UpdateFileLength(firstAddr, initFileLength + totSize);

            // Send notification
            OnWriteCompleted(new FileSystemEventArgs(firstAddr, filename, dataToWrite, totBlocks, initFileLength + totSize));
        }

        public FileEntryInfo[] GetFileList()
        {
            return _entries.GetFileList();
        }

        public DiskStatistics GetDiskStatistics()
        {
            decimal freeBlocks = 0M;
            decimal freeBlockLength = 0M;
            decimal maxFreeBlockLength = 0M;
            uint reservedBlocks = 0;
            uint usedBlocks = 0;
            long usedSpace = 0;

            for (uint i = 2; i < _entries.Count; i++)
            {
                if (_entries[i].Status == EntryStatus.Reserved)
                {
                    reservedBlocks++;
                }
                else if (_entries[i].Status == EntryStatus.Used || _entries[i].Status == EntryStatus.NextInChain || _entries[i].Status == EntryStatus.EndOfFile)
                {
                    usedBlocks++;
                    usedSpace += _entries[i].FileLength;
                }
                else if (_entries[i].Status == EntryStatus.Free)
                {
                    freeBlocks++;
                    freeBlockLength++;
                    continue;
                }

                if (freeBlockLength > maxFreeBlockLength)
                    maxFreeBlockLength = freeBlockLength;
                freeBlockLength = 0;
            }

            if (freeBlockLength > maxFreeBlockLength)
                maxFreeBlockLength = freeBlockLength;

            long reservedSpace = reservedBlocks * VolumeManager.Format.BlockSize;
            long freeSpace = (long)freeBlocks * VolumeManager.Format.BlockSize;
            long usedSpaceOnDisk = usedBlocks * VolumeManager.Format.BlockSize;

            decimal fragmentation = freeBlocks == 0 ? 1M : (freeBlocks - maxFreeBlockLength) / freeBlocks;
            // http://stackoverflow.com/questions/4586972/how-to-calculate-fragmentation/4587077#4587077
            //(free - freemax)
            //----------------   x 100%    (or 100% for free=0)
            //    free

            return new DiskStatistics(usedSpace, freeSpace, reservedSpace, usedSpaceOnDisk, fragmentation);
        }

        private uint LookupNextFree()
        {
            if (_algorithm == LookupAlgorithm.Circular)
            {
                uint count = 0;
                do
                {
                    // Keep the current position: chances are that the all the previous ones
                    // are alrady used, and the next one is available
                    _currentEntry++;
                    if (_currentEntry >= _entries.Count)
                        _currentEntry = 0; // Restart from the beginning

                    if (_entries[_currentEntry].Status == EntryStatus.Free)
                    {
                        OnLookupCompleted(new FileSystemEventArgs(_currentEntry, count));
                        return _currentEntry;
                    }

                    count++;
                    if (count > _entries.Count)
                        throw new FileSystemException("Disk full");
                } while (true);
            }
            else if (_algorithm == LookupAlgorithm.None)
            {
                for (uint i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Status != EntryStatus.Free)
                        continue;

                    OnLookupCompleted(new FileSystemEventArgs(i, i));
                    return i;
                }

                throw new FileSystemException("Disk full");
            }

            throw new FileSystemException("Invalid lookup alogirthm");
        }

        private uint LookupNextInChain(uint address)
        {
            uint seekAddr = address;
            uint addr = address;

            long count = 0;
            do
            {
                // Get next address
                seekAddr = _entries[seekAddr].NextBlockAddress;
                if (seekAddr > 0)
                    addr = seekAddr;

                // Prevents infinite loops
                count++;
                if (count > _entries.Count)
                    throw new FileSystemException("Error while seeking file");
            } while (seekAddr != 0);

            OnLookupCompleted(new FileSystemEventArgs(addr, count));

            return addr;
        }

        private uint GetFileAddress(string filename)
        {
            string name = FileAllocationTable.ParseFileName(filename);

            for (uint i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].FileName == name && _entries[i].Status == EntryStatus.Used)
                    return i;
            }

            throw new FileSystemException("File '{0}' not found.", filename);
        }

        private byte[] SplitData(byte[] sourceData, int blockLength, int readAtOffset)
        {
            byte[] block = new byte[blockLength];

            for (int i = 0; i < block.Length; i++)
            {
                if (i + readAtOffset >= sourceData.Length)
                    return block;
                block[i] = sourceData[i + readAtOffset];
            }

            return block;
        }

        private byte[] MergeData(byte[] destinationData, byte[] sourceData, int writeAtOffset)
        {
            int count = 0;
            for (int i = writeAtOffset; i < destinationData.Length; i++)
            {
                if (count >= sourceData.Length)
                    return destinationData; // End of the block

                destinationData[i] = sourceData[count];
                count++;
            }

            return destinationData;
        }
    }
}