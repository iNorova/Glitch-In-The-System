using UnityEngine;

/// <summary>
/// Batch 7: Routes File Explorer open/double-click actions to the correct app.
///
/// Rules (checked in order):
///   1. Folders       — FileExplorerApp.NavigateTo()            (handled by caller, not here)
///   2. .lnk files    — launch the named app via DesktopLauncherHub
///   3. .note files   — open Sticky Notes app
///   4. .png/.jpg     — open Paint app (view)
///   5. Everything else — log quietly, no crash, no popup
///
/// This is the ONLY place that maps file types to apps. FileExplorerApp delegates
/// here for all file opens — no routing logic lives in FileExplorerApp itself.
///
/// To add a new file handler: add a case to OpenFile(). No other file needs changing.
/// </summary>
public static class FsAppRouter
{
    // App shortcut names (must match BuildVirtualFS lnk file names)
    private const string LnkStickyNotes = "Sticky Notes.lnk";
    private const string LnkPaint       = "Paint.lnk";
    private const string LnkSocialMedia = "Social Media.lnk";
    private const string LnkWorkDash    = "Work Dashboard.lnk";

    /// <summary>
    /// Called by FileExplorerApp when the user double-clicks a file entry.
    /// Folders are handled before this is called — only files reach here.
    /// </summary>
    public static void OpenFile(FileSystemManager.FsEntry entry)
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
                DesktopLauncherHub.OpenPaintApp();
                return;

            default:
                Debug.Log($"[FsAppRouter] No handler for '{name}' (ext='{ext}').");
                return;
        }
    }

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
