using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the selected email's full content.
/// Attach to the right panel of EmailAppWindow.
/// ShowEmail() / ShowEmpty() called from EmailApp — no Update() needed.
/// Batch 6: attachment list + File Explorer integration (read-only, navigate-to-folder only).
/// </summary>
public sealed class EmailPreviewPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI senderLabel;
    [SerializeField] private TextMeshProUGUI recipientLabel;
    [SerializeField] private TextMeshProUGUI timestampLabel;
    [SerializeField] private TextMeshProUGUI subjectLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [SerializeField] private GameObject      emptyState;    // "Select an email" placeholder
    [SerializeField] private GameObject      previewHeader; // Phase2: hidden in empty state — labels show nothing

    // ── Batch 6: attachments ────────────────────────────────────────────────
    [SerializeField] private GameObject attachmentsSection;          // hidden when no attachments
    [SerializeField] private Transform  attachmentButtonsContainer;  // parent for cloned rows
    [SerializeField] private GameObject attachmentRowTemplate;       // inactive template, cloned per row

    // Pool of cloned attachment row buttons — avoids repeated Instantiate/Destroy across selections.
    private readonly List<GameObject> _attachmentRowPool = new List<GameObject>(8);

    private void Awake() => ShowEmpty();

    public void ShowEmail(EmailData data)
    {
        if (emptyState     != null) emptyState.SetActive(false);
        if (previewHeader  != null) previewHeader.SetActive(true);  // Phase2
        if (senderLabel    != null) senderLabel.text    = $"From:  {data.sender}";
        if (recipientLabel != null) recipientLabel.text = $"To:      {data.recipient}";
        if (timestampLabel != null) timestampLabel.text = data.timestamp;
        if (subjectLabel   != null) subjectLabel.text   = data.subject;
        if (bodyLabel      != null) bodyLabel.text      = data.body;

        ShowAttachments(data.attachments);
    }

    public void ShowEmpty()
    {
        if (emptyState     != null) emptyState.SetActive(true);
        if (previewHeader  != null) previewHeader.SetActive(false); // Phase2: hide empty labels
        if (senderLabel    != null) senderLabel.text    = "";
        if (recipientLabel != null) recipientLabel.text = "";
        if (timestampLabel != null) timestampLabel.text = "";
        if (subjectLabel   != null) subjectLabel.text   = "";
        if (bodyLabel      != null) bodyLabel.text      = "";

        ShowAttachments(null);
    }

    // ── Batch 6: attachment rendering ──────────────────────────────────────

    private void ShowAttachments(string[] attachments)
    {
        bool hasAny = attachments != null && attachments.Length > 0;

        if (attachmentsSection != null)
            attachmentsSection.SetActive(hasAny);

        if (attachmentButtonsContainer == null || attachmentRowTemplate == null)
            return;

        // Return all currently active rows to the pool (hide, keep parented)
        for (int i = attachmentButtonsContainer.childCount - 1; i >= 0; i--)
        {
            var child = attachmentButtonsContainer.GetChild(i).gameObject;
            if (child == attachmentRowTemplate) continue; // never recycle the template itself
            child.SetActive(false);
            if (!_attachmentRowPool.Contains(child))
                _attachmentRowPool.Add(child);
        }

        if (!hasAny) return;

        for (int i = 0; i < attachments.Length; i++)
        {
            string fullPath = attachments[i];
            var row = RentAttachmentRow();
            row.transform.SetParent(attachmentButtonsContainer, false);
            row.transform.SetAsLastSibling();
            row.SetActive(true);

            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = GetFileName(fullPath); // icon now comes from IconPlaceholder Image (future PNG)

            var btn = row.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                string capturedPath = fullPath; // capture for closure
                btn.onClick.AddListener(() => OpenAttachmentInFileExplorer(capturedPath));
            }
        }
    }

    private GameObject RentAttachmentRow()
    {
        for (int i = _attachmentRowPool.Count - 1; i >= 0; i--)
        {
            var pooled = _attachmentRowPool[i];
            if (pooled != null)
            {
                _attachmentRowPool.RemoveAt(i);
                return pooled;
            }
        }
        var clone = Instantiate(attachmentRowTemplate, attachmentButtonsContainer);
        return clone;
    }

    // ── Batch 6: File Explorer integration ───────────────────────────────────
    // Confirmed pattern: SimpleAppWindow.OpenIfClosed() + FileExplorerApp.NavigateTo(parentFolder).
    // No new file system, no file-selection API — navigates to the attachment's parent folder only.

    private static void OpenAttachmentInFileExplorer(string fullFilePath)
    {
        if (string.IsNullOrEmpty(fullFilePath)) return;

        var fileExplorerApp = FindFirstObjectByType<FileExplorerApp>(FindObjectsInactive.Include);
        if (fileExplorerApp == null) return;

        var windowGO = fileExplorerApp.gameObject;
        var simpleWindow = windowGO.GetComponent<SimpleAppWindow>();
        simpleWindow?.OpenIfClosed();

        string parentFolder = GetParentFolder(fullFilePath);
        fileExplorerApp.NavigateTo(parentFolder);
    }

    private static string GetParentFolder(string fullPath)
    {
        int lastSlash = fullPath.LastIndexOf('/');
        return lastSlash <= 0 ? "" : fullPath.Substring(0, lastSlash);
    }

    private static string GetFileName(string fullPath)
    {
        int lastSlash = fullPath.LastIndexOf('/');
        return lastSlash < 0 ? fullPath : fullPath.Substring(lastSlash + 1);
    }
}