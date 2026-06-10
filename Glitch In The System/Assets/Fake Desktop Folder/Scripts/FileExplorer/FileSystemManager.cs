using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-data virtual file system. Batch 6: mutation API added.
/// Batch 7: added Sticky Notes folder, app shortcuts on Desktop, Screenshots under Pictures.
/// Upgrade: NotifyChanged() now called after every mutation so FileExplorerApp.OnFsChanged fires.
/// </summary>
public sealed class FileSystemManager : MonoBehaviour
{
    public static FileSystemManager Instance { get; private set; }

    /// <summary>Fired whenever the virtual FS changes (create/rename/delete/register).</summary>
    public event System.Action OnChanged;

    public enum EntryType { Folder, File }

    [System.Serializable]
    public sealed class FsEntry
    {
        public string    name;
        public EntryType type;
        public string    parentPath;
        public string    fullPath;

        public FsEntry(string name, EntryType type, string parentPath)
        {
            this.name       = name;
            this.type       = type;
            this.parentPath = parentPath;
            this.fullPath   = parentPath == "" ? "/" + name : parentPath + "/" + name;
        }
    }

    private readonly Dictionary<string, FsEntry>       _entries  = new();
    private readonly Dictionary<string, List<FsEntry>> _children = new();

    // ── Persistence ───────────────────────────────────────────────────────
    private const  string SaveKey    = "FileExplorer_v1";
    private        bool   _dirty;
    private        float  _dirtySince = float.MaxValue;
    private const  float  SaveDebounce = 2f;

    [Serializable]
    private sealed class SaveEnvelope
    {
        public List<EntrySave> entries = new List<EntrySave>();
    }

    [Serializable]
    private sealed class EntrySave
    {
        public string name;
        public string parentPath;
        public string fullPath;
        public bool   isFolder;
    }

    public static readonly string[] SidebarRoots =
        { "Desktop", "Documents", "Pictures", "Sticky Notes", "Downloads", "Trash" };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!LoadFromPrefs())
            BuildVirtualFS();
    }

    private void BuildVirtualFS()
    {
        foreach (var root in SidebarRoots) AddFolder(root, "");

        AddFolder("Screenshots",     "/Desktop");
        AddFolder("Projects",        "/Desktop");
        AddFile  ("readme.txt",      "/Desktop");
        AddFile  ("notes.txt",       "/Desktop");
        AddFile("Sticky Notes.lnk",   "/Desktop");
        AddFile("Paint.lnk",          "/Desktop");
        AddFile("Social Media.lnk",   "/Desktop");
        AddFile("Work Dashboard.lnk", "/Desktop");

        AddFolder("Work",            "/Documents");
        AddFolder("Personal",        "/Documents");
        AddFile  ("budget.txt",      "/Documents");
        AddFile  ("todo.txt",        "/Documents");
        AddFolder("Reports",         "/Documents/Work");
        AddFile  ("q1_report.txt",   "/Documents/Work");
        AddFile  ("q2_report.txt",   "/Documents/Work");

        AddFolder("Wallpapers",      "/Pictures");
        AddFolder("Screenshots",     "/Pictures");
        AddFile  ("photo_001.png",   "/Pictures");
        AddFile  ("photo_002.png",   "/Pictures");

        AddFile("note_1.note",       "/Sticky Notes");
        AddFile("note_2.note",       "/Sticky Notes");
        AddFile("note_3.note",       "/Sticky Notes");

        AddFile  ("installer.exe",   "/Downloads");
        AddFile  ("archive.zip",     "/Downloads");
        AddFile  ("manual.pdf",      "/Downloads");

        AddFile  ("old_project.txt", "/Trash");
    }

    private void Update()
    {
        if (!_dirty) return;
        if (_dirtySince == float.MaxValue) _dirtySince = Time.unscaledTime;
        if (Time.unscaledTime - _dirtySince >= SaveDebounce)
            SaveToPrefs();
    }

    private void OnApplicationQuit() => SaveToPrefs();

    // ── Read API ──────────────────────────────────────────────────────────
    public string DisplayName(string fullPath) =>
        fullPath == "" ? "File Explorer"
        : _entries.TryGetValue(fullPath, out var e) ? e.name : fullPath;

    public IReadOnlyList<FsEntry> GetChildren(string folderPath)
    {
        if (_children.TryGetValue(folderPath, out var list)) return list;
        return System.Array.Empty<FsEntry>();
    }

    public bool Exists(string fullPath)     => _entries.ContainsKey(fullPath);
    public bool FolderExists(string p)      => _entries.TryGetValue(p, out var e) && e.type == EntryType.Folder;
    public FsEntry GetEntry(string p)       => _entries.TryGetValue(p, out var e) ? e : null;

    // ── Mutation API ──────────────────────────────────────────────────────

    public FsEntry CreateFolder(string parentPath, string name)
    {
        name = SanitizeName(name);
        var e = new FsEntry(name, EntryType.Folder, parentPath);
        if (_entries.ContainsKey(e.fullPath)) return null;
        Register(e);
        MarkDirty();
        NotifyChanged();   // FIX: was never called
        return e;
    }

    public FsEntry CreateFile(string parentPath, string name)
    {
        name = SanitizeName(name);
        var e = new FsEntry(name, EntryType.File, parentPath);
        if (_entries.ContainsKey(e.fullPath)) return null;
        Register(e);
        MarkDirty();
        NotifyChanged();   // FIX: was never called
        return e;
    }

    public bool Rename(string fullPath, string newName)
    {
        newName = SanitizeName(newName);
        if (!_entries.TryGetValue(fullPath, out var entry)) return false;
        if (string.IsNullOrWhiteSpace(newName)) return false;

        string newFullPath = entry.parentPath == ""
            ? "/" + newName
            : entry.parentPath + "/" + newName;

        if (_entries.ContainsKey(newFullPath)) return false;

        Unregister(entry);
        entry.name     = newName;
        entry.fullPath = newFullPath;
        Register(entry);
        RebuildChildPaths(fullPath, newFullPath);
        MarkDirty();
        NotifyChanged();   // FIX: was never called
        return true;
    }

    public bool Delete(string fullPath)
    {
        if (!_entries.TryGetValue(fullPath, out var entry)) return false;

        if (_children.TryGetValue(fullPath, out var kids))
        {
            var copy = new List<FsEntry>(kids);
            foreach (var child in copy) Delete(child.fullPath);
        }

        Unregister(entry);
        MarkDirty();
        NotifyChanged();   // FIX: was never called
        return true;
    }

    public bool Move(string fullPath, string newParentPath)
    {
        if (!_entries.TryGetValue(fullPath, out var entry)) return false;
        if (!FolderExists(newParentPath) && newParentPath != "") return false;

        string newFull = newParentPath == ""
            ? "/" + entry.name
            : newParentPath + "/" + entry.name;

        if (_entries.ContainsKey(newFull)) return false;

        string oldFull = entry.fullPath;
        Unregister(entry);
        entry.parentPath = newParentPath;
        entry.fullPath   = newFull;
        Register(entry);
        RebuildChildPaths(oldFull, newFull);
        MarkDirty();
        NotifyChanged();   // FIX: was never called
        return true;
    }

    // ── Batch 7: Integration helpers ──────────────────────────────────────

    public FsEntry RegisterScreenshot(string baseName)
    {
        const string folder = "/Pictures/Screenshots";
        if (!FolderExists(folder)) return null;

        string name = baseName + ".png";
        int n = 1;
        while (Exists(folder + "/" + name))
            name = baseName + "_" + (n++) + ".png";

        // CreateFile already calls NotifyChanged internally
        return CreateFile(folder, name);
    }

    private void MarkDirty()
    {
        _dirty = true;
        // Don't reset _dirtySince — keep the first-dirty timestamp
    }

    private void NotifyChanged() => OnChanged?.Invoke();

    private void SaveToPrefs()
    {
        var envelope = new SaveEnvelope();
        foreach (var e in _entries.Values)
            envelope.entries.Add(new EntrySave
            {
                name       = e.name,
                parentPath = e.parentPath,
                fullPath   = e.fullPath,
                isFolder   = e.type == EntryType.Folder,
            });
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(envelope));
        PlayerPrefs.Save();
        _dirty      = false;
        _dirtySince = float.MaxValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[FileExplorer] Saved {envelope.entries.Count} entries to PlayerPrefs.");
#endif
    }

    private bool LoadFromPrefs()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
            if (envelope?.entries == null || envelope.entries.Count == 0) return false;

            foreach (var e in envelope.entries)
            {
                var entry = new FsEntry(e.name,
                    e.isFolder ? EntryType.Folder : EntryType.File,
                    e.parentPath);
                entry.fullPath = e.fullPath;
                _entries[entry.fullPath] = entry;
                if (!_children.ContainsKey(entry.parentPath))
                    _children[entry.parentPath] = new List<FsEntry>();
                if (!_children[entry.parentPath].Contains(entry))
                    _children[entry.parentPath].Add(entry);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[FileExplorer] Loaded {envelope.entries.Count} entries from PlayerPrefs.");
#endif
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[FileExplorer] Load failed: {ex.Message} — rebuilding defaults.");
            return false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void AddFolder(string name, string parent) => Register(new FsEntry(name, EntryType.Folder, parent));
    private void AddFile  (string name, string parent) => Register(new FsEntry(name, EntryType.File,   parent));

    private void Register(FsEntry e)
    {
        _entries[e.fullPath] = e;
        if (!_children.ContainsKey(e.parentPath))
            _children[e.parentPath] = new List<FsEntry>();
        if (!_children[e.parentPath].Contains(e))
            _children[e.parentPath].Add(e);
    }

    private void Unregister(FsEntry e)
    {
        _entries.Remove(e.fullPath);
        if (_children.TryGetValue(e.parentPath, out var list))
            list.Remove(e);
    }

    private void RebuildChildPaths(string oldParent, string newParent)
    {
        if (!_children.TryGetValue(oldParent, out var kids)) return;
        var copy = new List<FsEntry>(kids);

        _children.Remove(oldParent);
        foreach (var child in copy)
        {
            _entries.Remove(child.fullPath);
            string oldFull   = child.fullPath;
            child.parentPath = newParent;
            child.fullPath   = newParent + "/" + child.name;
            Register(child);
            RebuildChildPaths(oldFull, child.fullPath);
        }
    }

    private static string SanitizeName(string n) =>
        string.IsNullOrWhiteSpace(n) ? "Untitled"
        : n.Trim().Replace("/", "").Replace("\\", "");
}
