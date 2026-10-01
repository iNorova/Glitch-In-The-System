/// <summary>Folder an email belongs to.</summary>
public enum EmailFolder { Inbox, Sent, Drafts, Spam, Trash }

/// <summary>
/// Plain data struct for one email entry.
/// Expandable in future batches (attachments, flags, corruption state).
/// </summary>
[System.Serializable]
public struct EmailData
{
    public string      sender;
    public string      recipient;
    public string      subject;
    public string      preview;      // short excerpt shown in list row
    public string      body;         // full body shown in preview panel
    public string      timestamp;
    public bool        isUnread;
    public EmailFolder folder;

    // ── Batch 5: category tags (independent of folder) ───────────────────
    public bool        isImportant;
    public bool        isSpam;
    public bool        isDeleted;

    // ── Batch 6: attachments (full virtual FS paths, e.g. "/Documents/Work/report.txt") ──
    public string[]    attachments;
}