namespace SimpleFS
{
    public enum FileAttribute
    {
        File = 0,
        ReadOnly = 1, // It does not allow a file to be opened for modification.
        Hidden = 2, // Hides files or directories from normal directory views.
        System = 4, //  Indicates that the file belongs to the system and must not be physically moved.
        VolumeLabel = 8, // Indicates an optional directory volume label, normally only residing in a volume's root directory. Ideally, the volume label should be the first entry in the directory (after reserved entries) in order to avoid problems with VFAT LFNs.
        Subdirectory = 10, // Indicates that the cluster-chain associated with this entry gets interpreted as subdirectory instead of as a file. Subdirectories have a filesize entry of zero.
        Archive = 20, // Typically set by the operating system as soon as the file is created or modified to mark the file as "dirty", and reset by backup software once the file has been backed up to indicate "pure" state.
        Device = 40, // Internally set for character device names found in filespecs, never found on disk. It must not be changed by disk tools.
        Reserved = 80, // Must not be changed by disk tools.
    }
}