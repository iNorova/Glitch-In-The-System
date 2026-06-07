using System;
using System.Collections.Generic;

namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// JSON-serializable save envelope.
    ///
    /// JsonUtility cannot serialize polymorphic lists, so VirtualFile and
    /// VirtualFolder are stored in separate typed lists. On load, both lists
    /// are merged back into the manager's flat dictionary via RestoreFrom().
    ///
    /// Format version lets future batches migrate old saves gracefully.
    /// </summary>
    [Serializable]
    public sealed class FileSystemSaveData
    {
        public int Version = 1;

        /// <summary>The single root folder ("My Computer").</summary>
        public VirtualFolder Root;

        /// <summary>All folder nodes except Root.</summary>
        public List<VirtualFolder> Folders = new List<VirtualFolder>();

        /// <summary>All file nodes.</summary>
        public List<VirtualFile> Files = new List<VirtualFile>();

        /// <summary>
        /// Build a save envelope from a live FileSystemManager.
        /// </summary>
        public static FileSystemSaveData From(FileSystemManager fs)
        {
            var data = new FileSystemSaveData { Root = fs.Root };

            foreach (var kvp in fs.AllNodes)
            {
                if (kvp.Key == fs.Root.Id) continue; // Root stored separately

                if (kvp.Value is VirtualFolder folder)
                    data.Folders.Add(folder);
                else if (kvp.Value is VirtualFile file)
                    data.Files.Add(file);
            }

            return data;
        }

        /// <summary>
        /// Restore a FileSystemManager from this envelope.
        /// Returns a fully hydrated manager; original instance is replaced.
        /// </summary>
        public FileSystemManager ToFileSystemManager()
        {
            var fs = new FileSystemManager(buildDefaultFolders: false);

            var allNodes = new List<VirtualNode>(Folders.Count + Files.Count);
            allNodes.AddRange(Folders);
            allNodes.AddRange(Files);

            fs.RestoreFrom(Root, allNodes);
            return fs;
        }
    }
}
