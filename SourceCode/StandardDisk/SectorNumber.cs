namespace StandardDisk
{
    /// <summary>
    /// Number of sectors for each block in the volume.
    /// </summary>
    public enum SectorNumber
    {
        Sectors_001 = 1 << 0,
        Sectors_002 = 1 << 1,
        Sectors_004 = 1 << 2,
        Sectors_008 = 1 << 3,
        Sectors_016 = 1 << 4,
        Sectors_032 = 1 << 5,
        Sectors_064 = 1 << 6,
        Sectors_128 = 1 << 7,
        Sectors_256 = 1 << 8,
    }
}