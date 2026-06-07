namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// Every file in the virtual file system has exactly one type.
    /// Add new entries here as new app ecosystems are introduced.
    /// Folder is intentionally absent — folders are VirtualFolder, not VirtualFile.
    /// </summary>
    public enum FileType
    {
        // Generic
        Unknown     = 0,
        TextFile    = 1,

        // Rich content
        Image       = 10,   // screenshots, snipping tool captures
        StickyNote  = 20,   // sticky note payload

        // OS-level
        Shortcut    = 30,   // points to an app or another node by id
    }
}
