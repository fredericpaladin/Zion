using System;

namespace SimpleFS
{
    public struct FileControlBlock
    {
        public string FileName { get; set; }

        public EntryStatus Status { get; set; }

        public DateTime CreationTime { get; set; }

        public DateTime LastAccessTime { get; set; }

        public DateTime LastWriteTime { get; set; }

        public int BlockLength { get; set; }

        public int FileLength { get; set; }

        public uint BlockAddress { get; set; }

        public uint NextBlockAddress { get; set; }

        public FileControlBlock(EntryStatus status)
            : this()
        {
            Status = status;
        }

        public override string ToString()
        {
            return String.Format("{0} - {1} (File Size: {2:n0}  /  Block Size: {3:n0})", FileName, Status, FileLength, BlockLength);
        }
    }
}