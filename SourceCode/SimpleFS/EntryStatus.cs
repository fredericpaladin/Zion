using System;

namespace SimpleFS
{
    [Flags]
    public enum EntryStatus
    {
        Free = 1 << 0,
        Used = 1 << 1,
        NextInChain = 1 << 2,
        EndOfFile = 1 << 3,
        Bad = 1 << 4,
        Reserved = 1 << 8,
    }
}