using System.IO;
using UnityEngine;

namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// MonoBehaviour host for the virtual file system.
    ///
    /// Lifetime: spawned once by GameBootstrap (same pattern as GameDatabase),
    /// adopted by RuntimePersistency so it survives scene loads.
    ///
    /// Responsibilities:
    ///   • Load saved filesystem on startup (or build defaults on first run)
    ///   • Expose the singleton FileSystemManager instance
    ///   • Auto-save whenever the manager is dirty (checked each frame, debounced)
    ///   • Save on application quit
    /// </summary>
    public sealed class FileSystemService : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────
        public static FileSystemService Instance { get; private set; }

        // ── Live manager ──────────────────────────────────────────────────
        public FileSystemManager FS { get; private set; }

        // ── Config ────────────────────────────────────────────────────────
        [Tooltip("Seconds of idle time after last mutation before auto-save fires.")]
        [SerializeField] private float autoSaveDebounceSeconds = 3f;

        // ── Save path ─────────────────────────────────────────────────────
        // Application.persistentDataPath is writable on all platforms.
        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "filesystem.json");

        // ── Internal ──────────────────────────────────────────────────────
        private float _dirtySince = float.MaxValue; // timestamp of first dirty mark

        // ── Unity lifecycle ───────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            FS = Load();
        }

        private void Update()
        {
            if (FS == null || !FS.IsDirty) return;

            // Record when dirtiness first appeared
            if (_dirtySince == float.MaxValue)
                _dirtySince = Time.unscaledTime;

            // Debounce: wait until the user stops making changes
            if (Time.unscaledTime - _dirtySince >= autoSaveDebounceSeconds)
            {
                Save();
                _dirtySince = float.MaxValue;
            }
        }

        private void OnApplicationQuit() => Save();

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Force an immediate save regardless of dirty state.</summary>
        public void Save()
        {
            if (FS == null) return;

            try
            {
                var data = FileSystemSaveData.From(FS);
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SavePath, json);
                FS.ClearDirty();
                _dirtySince = float.MaxValue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[FileSystemService] Saved → {SavePath}");
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FileSystemService] Save failed: {ex.Message}");
            }
        }

        /// <summary>Load from disk, or build fresh defaults if no save exists.</summary>
        public FileSystemManager Load()
        {
            if (!File.Exists(SavePath))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[FileSystemService] No save found — building default filesystem.");
#endif
                var fresh = new FileSystemManager(buildDefaultFolders: true);
                return fresh;
            }

            try
            {
                var json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<FileSystemSaveData>(json);

                if (data == null || data.Root == null)
                {
                    Debug.LogWarning("[FileSystemService] Save file corrupt — rebuilding.");
                    return new FileSystemManager(buildDefaultFolders: true);
                }

                var fs = data.ToFileSystemManager();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[FileSystemService] Loaded {fs.AllNodes.Count} nodes from {SavePath}");
#endif
                return fs;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FileSystemService] Load failed: {ex.Message} — rebuilding.");
                return new FileSystemManager(buildDefaultFolders: true);
            }
        }

        /// <summary>
        /// Wipe the save file and rebuild default folders.
        /// Useful for debug resets; not exposed in production UI.
        /// </summary>
        public void ResetToDefaults()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);

            FS = new FileSystemManager(buildDefaultFolders: true);
            Save();
        }
    }
}
