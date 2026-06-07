using System;
using System.Collections.Generic;

namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// Central in-memory virtual file system.
    ///
    /// Lifetime: plain C# class, no MonoBehaviour. Instantiate once and keep alive
    /// however the game prefers (static field, ScriptableObject wrapper, or the
    /// persistence manager introduced in Batch 2).
    ///
    /// All mutations go through this class — nodes never edit themselves.
    /// </summary>
    public sealed class FileSystemManager
    {
        // ── Well-known folder names ───────────────────────────────────────
        public const string FolderDesktop    = "Desktop";
        public const string FolderDocuments  = "Documents";
        public const string FolderPictures   = "Pictures";
        public const string FolderStickyNotes = "Sticky Notes";
        public const string FolderDownloads  = "Downloads";
        public const string FolderTrash      = "Trash";

        // ── Storage ───────────────────────────────────────────────────────
        // Flat dictionary: O(1) lookup by id for all operations.
        private readonly Dictionary<string, VirtualNode> _nodes =
            new Dictionary<string, VirtualNode>(64);

        // ── Root ──────────────────────────────────────────────────────────
        /// <summary>The invisible root that owns all top-level folders.</summary>
        public VirtualFolder Root { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────
        /// <param name="buildDefaultFolders">
        /// Pass false when restoring from a save — Batch 2 will call this
        /// with false and then re-populate _nodes from serialized data.
        /// </param>
        public FileSystemManager(bool buildDefaultFolders = true)
        {
            Root = new VirtualFolder
            {
                Id          = NewId(),
                Name        = "My Computer",
                ParentId    = null,
                CreatedUtc  = Now(),
                ModifiedUtc = Now(),
            };
            _nodes[Root.Id] = Root;

            if (buildDefaultFolders)
                BuildDefaultFolders();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Create a new folder inside <paramref name="parentId"/>.</summary>
        /// <returns>The new folder, or null if parent not found / not a folder.</returns>
        public VirtualFolder CreateFolder(string name, string parentId)
        {
            if (!TryGetFolder(parentId, out var parent)) return null;

            var folder = new VirtualFolder
            {
                Id          = NewId(),
                Name        = ValidateName(name),
                ParentId    = parentId,
                CreatedUtc  = Now(),
                ModifiedUtc = Now(),
            };

            _nodes[folder.Id] = folder;
            parent.AddChild(folder.Id);
            parent.TouchModified();
            MarkDirty();
            return folder;
        }

        /// <summary>Create a new file inside <paramref name="parentId"/>.</summary>
        /// <returns>The new file, or null if parent not found / not a folder.</returns>
        public VirtualFile CreateFile(string name, FileType type, string parentId,
                                      string payload = null)
        {
            if (!TryGetFolder(parentId, out var parent)) return null;

            var file = new VirtualFile
            {
                Id          = NewId(),
                Name        = ValidateName(name),
                Type        = type,
                ParentId    = parentId,
                Payload     = payload,
                SizeBytes   = payload != null
                                  ? System.Text.Encoding.UTF8.GetByteCount(payload)
                                  : 0L,
                CreatedUtc  = Now(),
                ModifiedUtc = Now(),
            };

            _nodes[file.Id] = file;
            parent.AddChild(file.Id);
            parent.TouchModified();
            MarkDirty();
            return file;
        }

        /// <summary>
        /// Delete a node by id. If it is a folder, recursively deletes all descendants.
        /// Root cannot be deleted.
        /// </summary>
        /// <returns>True on success.</returns>
        public bool Delete(string id)
        {
            if (id == Root.Id) return false;
            if (!_nodes.TryGetValue(id, out var node)) return false;

            // Detach from parent
            if (!string.IsNullOrEmpty(node.ParentId) &&
                TryGetFolder(node.ParentId, out var parent))
            {
                parent.RemoveChild(id);
                parent.TouchModified();
            }

            // Recursively remove descendants
            DeleteSubtree(node);
            MarkDirty();
            return true;
        }

        /// <summary>Rename a node. Returns false if id not found.</summary>
        public bool Rename(string id, string newName)
        {
            if (!_nodes.TryGetValue(id, out var node)) return false;
            node.Name = ValidateName(newName);
            node.TouchModified();

            // Touch parent too — its directory listing changed
            if (!string.IsNullOrEmpty(node.ParentId) &&
                _nodes.TryGetValue(node.ParentId, out var parent))
                parent.TouchModified();

            MarkDirty();
            return true;
        }

        /// <summary>
        /// Move <paramref name="id"/> to <paramref name="newParentId"/>.
        /// Prevents moving a folder into one of its own descendants.
        /// </summary>
        public bool Move(string id, string newParentId)
        {
            if (!_nodes.TryGetValue(id, out var node)) return false;
            if (id == Root.Id) return false;
            if (!TryGetFolder(newParentId, out var newParent)) return false;

            // Guard: new parent must not be a descendant of the node being moved
            if (IsDescendantOf(newParentId, id)) return false;

            // Detach from old parent
            if (!string.IsNullOrEmpty(node.ParentId) &&
                TryGetFolder(node.ParentId, out var oldParent))
            {
                oldParent.RemoveChild(id);
                oldParent.TouchModified();
            }

            node.ParentId = newParentId;
            node.TouchModified();

            newParent.AddChild(id);
            newParent.TouchModified();
            MarkDirty();
            return true;
        }

        /// <summary>
        /// Returns the direct children of <paramref name="folderId"/> in order.
        /// Folders come first, then files, both sorted by name.
        /// </summary>
        public List<VirtualNode> GetChildren(string folderId)
        {
            if (!TryGetFolder(folderId, out var folder))
                return new List<VirtualNode>(0);

            var folders = new List<VirtualNode>();
            var files   = new List<VirtualNode>();

            foreach (var childId in folder.ChildIds)
            {
                if (!_nodes.TryGetValue(childId, out var child)) continue;
                if (child.IsFolder) folders.Add(child);
                else                files.Add(child);
            }

            folders.Sort((a, b) => string.Compare(a.Name, b.Name,
                StringComparison.OrdinalIgnoreCase));
            files.Sort((a, b) => string.Compare(a.Name, b.Name,
                StringComparison.OrdinalIgnoreCase));

            folders.AddRange(files);
            return folders;
        }

        /// <summary>
        /// Returns the full display path for a node, e.g.
        /// "My Computer / Documents / Notes / ideas.txt"
        /// </summary>
        public string GetPath(string id)
        {
            if (!_nodes.TryGetValue(id, out var node)) return string.Empty;

            var parts = new List<string> { node.Name };
            var current = node;

            while (!string.IsNullOrEmpty(current.ParentId) &&
                   _nodes.TryGetValue(current.ParentId, out var p))
            {
                parts.Add(p.Name);
                current = p;
            }

            parts.Reverse();
            return string.Join(" / ", parts);
        }

        /// <summary>O(1) lookup by id. Returns null if not found.</summary>
        public VirtualNode FindById(string id)
        {
            _nodes.TryGetValue(id, out var node);
            return node;
        }

        /// <summary>Convenience: find a top-level folder by exact name.</summary>
        public VirtualFolder FindRootFolder(string name)
        {
            foreach (var childId in Root.ChildIds)
            {
                if (_nodes.TryGetValue(childId, out var n) &&
                    n is VirtualFolder f &&
                    string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
            return null;
        }

        /// <summary>
        /// Exposes the raw node dictionary — used by Batch 2 persistence only.
        /// Do not mutate externally.
        /// </summary>
        public IReadOnlyDictionary<string, VirtualNode> AllNodes => _nodes;

        // ── Dirty tracking (Batch 2) ──────────────────────────────────────
        /// <summary>True when the filesystem has unsaved mutations.</summary>
        public bool IsDirty { get; private set; }
        /// <summary>Called by FileSystemService after a successful save.</summary>
        public void ClearDirty() => IsDirty = false;
        internal void MarkDirty()  => IsDirty = true;

        // ── Internal helpers ──────────────────────────────────────────────

        /// Recursively remove a subtree from the dictionary.
        private void DeleteSubtree(VirtualNode node)
        {
            if (node is VirtualFolder folder)
            {
                // Copy list: we'll modify ChildIds as we recurse
                var children = new List<string>(folder.ChildIds);
                foreach (var childId in children)
                    if (_nodes.TryGetValue(childId, out var child))
                        DeleteSubtree(child);
            }
            _nodes.Remove(node.Id);
        }

        /// Returns true if <paramref name="candidateId"/> is inside the subtree
        /// rooted at <paramref name="ancestorId"/>.
        private bool IsDescendantOf(string candidateId, string ancestorId)
        {
            var current = candidateId;
            while (!string.IsNullOrEmpty(current))
            {
                if (current == ancestorId) return true;
                if (!_nodes.TryGetValue(current, out var n)) break;
                current = n.ParentId;
            }
            return false;
        }

        private bool TryGetFolder(string id, out VirtualFolder folder)
        {
            folder = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (!_nodes.TryGetValue(id, out var node)) return false;
            folder = node as VirtualFolder;
            return folder != null;
        }

        private void BuildDefaultFolders()
        {
            foreach (var name in new[]
            {
                FolderDesktop,
                FolderDocuments,
                FolderPictures,
                FolderStickyNotes,
                FolderDownloads,
                FolderTrash,
            })
            {
                CreateFolder(name, Root.Id);
            }
        }

        // ── Restore support (Batch 2) ─────────────────────────────────────

        /// <summary>
        /// Re-hydrate the manager from a flat list of serialised nodes.
        /// Called by the persistence layer after deserialisation — do not call directly.
        /// </summary>
        internal void RestoreFrom(VirtualFolder root, IEnumerable<VirtualNode> nodes)
        {
            _nodes.Clear();
            Root = root;
            _nodes[root.Id] = root;
            foreach (var n in nodes)
                _nodes[n.Id] = n;
        }

        private static string NewId()   => Guid.NewGuid().ToString("N");
        private static string Now()     => DateTime.UtcNow.ToString("o");

        private static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Node name cannot be empty.");
            return name.Trim();
        }
    }
}
