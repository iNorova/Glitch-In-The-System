using System.IO;
using UnityEngine;

/// <summary>
/// Routes File Explorer open/double-click actions to the correct app.
///
/// Rules (checked in order):
///   1. Folders       — FileExplorerApp.NavigateTo()          (handled by caller)
///   2. .lnk files    — launch the named app via DesktopLauncherHub
///   3. .note files   — open Sticky Notes
///   4. .png/.jpg     — open IMAGE PREVIEW ONLY (NOT Paint)
///   5. Everything else — log quietly, no crash
///
/// Only one place maps file types to apps. FileExplorerApp delegates here.
/// </summary>
public static class FsAppRouter
{
    private const string LnkStickyNotes = "Sticky Notes.lnk";
    private const string LnkPaint       = "Paint.lnk";
    private const string LnkSocialMedia = "Social Media.lnk";
    private const string LnkWorkDash    = "Work Dashboard.lnk";

    public static void OpenFile(FileExplorerManager.FsEntry entry)
    {
        if (entry == null) return;

        string name = entry.name;
        string ext  = GetExtension(name);

        switch (ext)
        {
            case ".lnk":
                OpenAppShortcut(name);
                return;

            case ".note":
                DesktopLauncherHub.OpenStickyNotes();
                return;

            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".bmp":
                OpenImageFile(entry);
                return;

            default:
                Debug.Log($"[FsAppRouter] No handler for '{name}' (ext='{ext}').");
                return;
        }
    }

    // ── Image preview — view only, never opens Paint ──────────────────────
    private static void OpenImageFile(FileExplorerManager.FsEntry entry)
    {
        // Load from persistentDataPath/Screenshots
        string filePath = Path.Combine(
            Application.persistentDataPath, "Screenshots", entry.name);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"[FsAppRouter] Image file not found on disk: {filePath}");
            // Still open preview with null — shows empty modal rather than silently failing
            DesktopLauncherHub.OpenImagePreview(null);
            return;
        }

        // Cache-first: avoid redundant disk reads and Texture2D allocations
        // when the same image is opened multiple times (e.g. Browse → Preview → Back → Preview).
        Texture2D texture = FsTextureCache.Get(filePath);
        if (texture == null)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.filterMode = FilterMode.Bilinear;

            if (!texture.LoadImage(bytes))
            {
                Debug.LogWarning($"[FsAppRouter] Failed to decode image: {filePath}");
                Object.Destroy(texture);
                return;
            }
            // Store with owned=true — cache manages lifetime of disk-loaded textures.
            FsTextureCache.Set(filePath, texture, owned: true);
        }

        DesktopLauncherHub.OpenImagePreview(texture, entry.name);
    }

    // ── App shortcuts ─────────────────────────────────────────────────────
    private static void OpenAppShortcut(string lnkName)
    {
        switch (lnkName)
        {
            case LnkStickyNotes:      DesktopLauncherHub.OpenStickyNotes();    break;
            case LnkPaint:            DesktopLauncherHub.OpenPaintApp();        break;
            case LnkSocialMedia:      DesktopLauncherHub.OpenSocialMedia();     break;
            case LnkWorkDash:         DesktopLauncherHub.OpenWorkDashboard();   break;
            case "File Explorer.lnk": DesktopLauncherHub.OpenFileExplorer();    break;
            default:
                Debug.Log($"[FsAppRouter] Unknown app shortcut: '{lnkName}'.");
                break;
        }
    }

    private static string GetExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return string.Empty;
        int dot = fileName.LastIndexOf('.');
        return dot >= 0 ? fileName.Substring(dot).ToLowerInvariant() : string.Empty;
    }
}
