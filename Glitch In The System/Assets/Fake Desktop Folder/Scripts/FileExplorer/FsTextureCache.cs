using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight texture cache for File Explorer image previews.
/// Key = absolute file path (or any stable unique string).
/// Lifetime: textures are destroyed when Evict() or Clear() is called,
/// or when the cache entry is replaced by a newer load of the same path.
///
/// USAGE
///   // Load (cache-first):
///   Texture2D tex = FsTextureCache.Get(filePath);
///   if (tex == null) { tex = LoadFromDisk(filePath); FsTextureCache.Set(filePath, tex); }
///
///   // Register a texture already in memory (e.g. SnippingCapture result):
///   FsTextureCache.Set(filePath, existingTexture, owned: false); // don't destroy it here
///
///   // Cleanup:
///   FsTextureCache.Evict(filePath);   // remove one entry
///   FsTextureCache.Clear();            // remove all
/// </summary>
public static class FsTextureCache
{
    private struct Entry
    {
        public Texture2D texture;
        public bool      owned; // true = we destroy this texture on eviction
    }

    // String key = absolute disk path or virtual path (e.g. screenshot base name).
    private static readonly Dictionary<string, Entry> _cache =
        new Dictionary<string, Entry>(StringComparer.Ordinal);

    /// <summary>Returns the cached texture for <paramref name="key"/>, or null if not cached.</summary>
    public static Texture2D Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return _cache.TryGetValue(key, out var e) && e.texture != null ? e.texture : null;
    }

    /// <summary>
    /// Stores <paramref name="texture"/> under <paramref name="key"/>.
    /// If <paramref name="owned"/> is true (default), the cache will Destroy the texture
    /// when it is evicted or replaced. Pass owned=false for textures whose lifetime is
    /// managed externally (e.g. SnippingCapture.LastCapture).
    /// </summary>
    public static void Set(string key, Texture2D texture, bool owned = true)
    {
        if (string.IsNullOrEmpty(key) || texture == null) return;

        // Evict stale entry for this key before overwriting
        if (_cache.TryGetValue(key, out var existing))
            DestroyEntry(existing);

        _cache[key] = new Entry { texture = texture, owned = owned };
    }

    /// <summary>Removes and optionally destroys the cached texture for <paramref name="key"/>.</summary>
    public static void Evict(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_cache.TryGetValue(key, out var e))
        {
            DestroyEntry(e);
            _cache.Remove(key);
        }
    }

    /// <summary>Removes ALL cached textures, destroying owned ones.</summary>
    public static void Clear()
    {
        foreach (var e in _cache.Values)
            DestroyEntry(e);
        _cache.Clear();
    }

    /// <summary>Number of textures currently in cache.</summary>
    public static int Count => _cache.Count;

    private static void DestroyEntry(Entry e)
    {
        if (e.owned && e.texture != null)
            UnityEngine.Object.Destroy(e.texture);
    }
}
