using FileSystemInterface;
using System;

namespace StandardDisk
{
    public class VolumeManager : IVolume
    {
        #region Events

        public event VolumeEventHandler WriteAddressCompleted;

        public event VolumeEventHandler ReadAddressCompleted;

        protected virtual void OnWriteAddressCompleted(VolumeEventArgs e)
        {
            if (WriteAddressCompleted != null)
                WriteAddressCompleted(this, e);
        }

        protected virtual void OnReadAddressCompleted(VolumeEventArgs e)
        {
            if (ReadAddressCompleted != null)
                ReadAddressCompleted(this, e);
        }

        #endregion Events

        private Block[] _blocks;
        public decimal _seekTime;
        public decimal _latency;
        public decimal _transferTime;
        private SectorSize _sectorSize;
        private SectorNumber _sectorNum;

        public VolumeFormat Format { get; private set; }

        public Parameters VolumeParameters
        {
            get
            {
                Parameters parameters = new Parameters();
                parameters.Add("Seek Time", typeof(decimal), DisablingMode.AfterMount, "ms", 15, 1, 20);
                parameters.Add("Latency", typeof(decimal), DisablingMode.AfterMount, "ms", 3, 0, 10);
                parameters.Add("Transfer Time", typeof(decimal), DisablingMode.AfterMount, "ms", 0.5, 0.1, 2);
                parameters.Add("Sector Size", typeof(SectorSize), DisablingMode.AfterMount, "bytes", SectorSize.Size_512);
                parameters.Add("Sector Number", typeof(SectorNumber), DisablingMode.Never, "", SectorNumber.Sectors_004);

                return parameters;
            }
        }

        public void Init(params object[] args)
        {
            // The args follow the same sequence as the Parameters
            _seekTime = (decimal)args[0];
            _latency = (decimal)args[1];
            _transferTime = (decimal)args[2];
            _sectorSize = (SectorSize)args[3];

            if (_sectorSize == 0)
                throw new VolumeException("Must specify Sector Size");
        }

        public void FormatVolume(string fileSystem, uint size, params object[] args)
        {
            if (size == 0)
                throw new VolumeException("Drive size cannot be zero.");

            _sectorNum = (SectorNumber)args[4];
            if (_sectorNum == 0)
                throw new VolumeException("Must specify Sector Number");

            uint sizeBytes = size * (uint)VolumeFormat.MEBIBYTE;
            uint sectorsCount = sizeBytes / (uint)_sectorSize;
            uint blocksCount = sectorsCount / (uint)_sectorNum;
            uint blockSize = (uint)_sectorNum * (uint)_sectorSize;

            Format = new VolumeFormat(fileSystem, blocksCount, blockSize);

            _blocks = new Block[Format.BlocksCount];
            for (uint addr = 0; addr < Format.BlocksCount; addr++)
                _blocks[addr] = new Block((uint)_sectorSize, (uint)_sectorNum, addr);
        }

        public byte[] ReadBlock(uint address)
        {
            decimal time = _seekTime + _latency + _transferTime;
            byte[] data = _blocks[address].ReadData();

            OnReadAddressCompleted(new VolumeEventArgs(time, address, data.Length));

            return data;
        }

        public byte[] ReadBlock(uint address, params object[] args)
        {
            throw new NotImplementedException("ReadBlock not implemented");
        }

        public void WriteBlock(byte[] data, uint address)
        {
            decimal time = _seekTime + _latency + _transferTime;
            _blocks[address].WriteData(data, 0);

            OnWriteAddressCompleted(new VolumeEventArgs(time, address, data.Length));
        }

        public void WriteBlock(byte[] data, uint address, params object[] args)
        {
            int writeAtOffset = (int)args[0];

            decimal time = _seekTime + _latency + _transferTime;
            _blocks[address].WriteData(data, writeAtOffset);

            OnWriteAddressCompleted(new VolumeEventArgs(time, address, data.Length));
        }

        public int GetBlockLength(uint address)
        {
            return _blocks[address].Length;
        }
    }
}