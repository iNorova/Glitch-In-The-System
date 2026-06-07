using System;
using System.Collections.Generic;

namespace GlitchInTheSystem.FileSystem
{
    /// <summary>
    /// Base record shared by both files and folders.
    /// Kept as a plain class (not struct) so the manager can hold polymorphic
    /// references and mutate nodes in place without boxing overhead.
    ///
    /// Serialization note: all fields are primitive or string — JsonUtility,
    /// Newtonsoft, and Unity's own serializer can all round-trip this without
    /// custom converters. Batch 2 (persistence) can use any of them.
    /// </summary>
    [Serializable]
    public abstract class VirtualNode
    {
        // ── Identity ──────────────────────────────────────────────────────
        public string Id;           // GUID string, e.g. "a3f1…"
        public string Name;         // display name, mutable via Rename()
        public string ParentId;     // null or empty only for the root folder

        // ── Timestamps ────────────────────────────────────────────────────
        public string CreatedUtc;   // ISO-8601, set once on creation
        public string ModifiedUtc;  // ISO-8601, updated on every mutation

        // ── Metadata ──────────────────────────────────────────────────────
        // Flat string→string bag. Keeps the base class lean while allowing
        // per-type extensions without subclass explosion.
        // Examples: { "color":"#FFD700" }, { "appId":"StickyNotes" }
        public List<MetaEntry> Metadata = new List<MetaEntry>();

        // ── Helpers ───────────────────────────────────────────────────────
        public bool IsFolder => this is VirtualFolder;

        public string GetMeta(string key)
        {
            foreach (var e in Metadata)
                if (e.Key == key) return e.Value;
            return null;
        }

        public void SetMeta(string key, string value)
        {
            for (int i = 0; i < Metadata.Count; i++)
            {
                if (Metadata[i].Key != key) continue;
                Metadata[i] = new MetaEntry(key, value);
                return;
            }
            Metadata.Add(new MetaEntry(key, value));
        }

        public void RemoveMeta(string key)
        {
            for (int i = Metadata.Count - 1; i >= 0; i--)
                if (Metadata[i].Key == key) Metadata.RemoveAt(i);
        }

        internal void TouchModified() =>
            ModifiedUtc = DateTime.UtcNow.ToString("o");
    }

    /// <summary>
    /// JsonUtility-safe key/value pair (Dictionary is not supported by JsonUtility).
    /// </summary>
    [Serializable]
    public struct MetaEntry
    {
        public string Key;
        public string Value;
        public MetaEntry(string k, string v) { Key = k; Value = v; }
    }
}
