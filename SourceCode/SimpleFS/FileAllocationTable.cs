using FileSystemInterface;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SimpleFS
{
    public class FileAllocationTable : IEnumerable
    {
        private FileControlBlock[] _entries;

        public FileAllocationTable(uint clustersCount)
        {
            _entries = new FileControlBlock[clustersCount];
            for (uint i = 0; i < clustersCount; i++)
                _entries[i] = new FileControlBlock(EntryStatus.Free);
        }

        public FileControlBlock this[uint index] { get { return _entries[index]; } }

        public int Count { get { return _entries.Length; } }

        public FileEntryInfo[] GetFileList()
        {
            List<FileEntryInfo> files = new List<FileEntryInfo>();

            foreach (FileControlBlock fcb in _entries)
            {
                if (fcb.Status != EntryStatus.Used)
                    continue;

                if (String.IsNullOrEmpty(fcb.FileName))
                    continue;

                FileEntryInfo info = new FileEntryInfo();
                info.Name = fcb.FileName;
                info.Length = fcb.FileLength;
                info.CreationTime = fcb.CreationTime;
                info.LastAccessTime = fcb.LastAccessTime;
                info.LastWriteTime = fcb.LastWriteTime;
                info.FirstAddress = fcb.BlockAddress;
                files.Add(info);
            }

            return files.ToArray();
        }

        internal void CreateEntry(uint entry, string filename, EntryStatus status)
        {
            _entries[entry].FileName = ParseFileName(filename);
            _entries[entry].Status = status;
            _entries[entry].BlockLength = 0;
            _entries[entry].FileLength = 0;
            _entries[entry].BlockAddress = entry;
            _entries[entry].NextBlockAddress = 0;

            DateTime dateTime = DateTime.Now;
            UpdateCreationTime(entry, dateTime);
            UpdateLastAccessTime(entry, dateTime);
            UpdateLastWriteTime(entry, dateTime);
        }

        internal uint RemoveEntry(uint entry)
        {
            uint nextAddr = _entries[entry].NextBlockAddress;
            _entries[entry].FileName = "";
            _entries[entry].Status = EntryStatus.Free;
            _entries[entry].BlockAddress = 0;
            _entries[entry].NextBlockAddress = 0;

            return nextAddr;
        }

        internal bool ContainsEntry(string filename)
        {
            string name = ParseFileName(filename);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].FileName == name && _entries[i].Status == EntryStatus.Used)
                    return true;
            }

            return false;
        }

        #region Update Methods

        internal void UpdateEntryNextAddress(uint entry, uint nextAddr)
        {
            _entries[entry].NextBlockAddress = nextAddr;
        }

        internal void UpdateFileLength(uint entry, int fileLength)
        {
            _entries[entry].FileLength = fileLength;
        }

        internal void UpdateBlockLength(uint entry, int blockLength)
        {
            _entries[entry].BlockLength = blockLength;
        }

        internal void UpdateEntryStatus(uint entry, EntryStatus status)
        {
            _entries[entry].Status = status;
        }

        internal void UpdateCreationTime(uint entry, DateTime dateTime)
        {
            _entries[entry].CreationTime = dateTime;
        }

        internal void UpdateLastAccessTime(uint entry, DateTime dateTime)
        {
            _entries[entry].LastAccessTime = dateTime;
        }

        internal void UpdateLastWriteTime(uint entry, DateTime dateTime)
        {
            _entries[entry].LastWriteTime = dateTime;
        }

        #endregion Update Methods

        internal static string ParseFileName(string filename)
        {
            // This file system does not allow directories
            string[] split = filename.Split('\\');
            string name = filename;
            if (split.Length >= 2)
                name = String.Format("{0}\\{1}", split[0], split[split.Length - 1]);

            return name;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _entries.GetEnumerator();
        }
    }
}