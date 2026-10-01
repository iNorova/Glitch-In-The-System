using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Batch 5 — independent filter layer on top of folders.</summary>
public enum EmailCategory { Inbox, Important, Spam, Deleted }

/// <summary>
/// Email app controller — Batch 3 + Batch 4.
/// Serialized fields match the exact names recovered from GameplayScene YAML.
/// Batch 3: folder navigation, list build, preview, unread→read.
/// Batch 4: live search, sidebar active state, refresh current folder.
/// No Update() — all event-driven.
/// </summary>
public sealed class EmailApp : MonoBehaviour
{
    // ── Serialized refs (exact YAML field names — DO NOT rename) ─────────
    [SerializeField] private RectTransform   listContent;
    [SerializeField] private GameObject      listItemTemplate;
    [SerializeField] private TextMeshProUGUI folderTitle;
    [SerializeField] private TextMeshProUGUI countLabel;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private EmailPreviewPanel previewPanel;

    // ── Batch 4: search + sidebar ─────────────────────────────────────────
    [Header("Batch 4")]
    [SerializeField] private TMP_InputField  searchField;
    [SerializeField] private Button          refreshButton;

    // Sidebar folder buttons — assigned in Inspector or found by name
    [SerializeField] private Button sidebarInbox;
    [SerializeField] private Button sidebarSent;
    [SerializeField] private Button sidebarDrafts;
    [SerializeField] private Button sidebarSpam;
    [SerializeField] private Button sidebarTrash;

    // ── Batch 5: category toolbar buttons ─────────────────────────────────
    [Header("Batch 5")]
    [SerializeField] private Button categoryInboxButton;
    [SerializeField] private Button categoryImportantButton;
    [SerializeField] private Button categorySpamButton;
    [SerializeField] private Button categoryDeletedButton;

    // ── Runtime state ─────────────────────────────────────────────────────
    private EmailFolder   _currentFolder   = EmailFolder.Inbox;
    private EmailCategory _activeCategory  = EmailCategory.Inbox;
    private Button        _activeSidebarButton;
    private Button        _activeCategoryButton;
    private EmailListItem    _selectedItem;
    private ComposeEmailWindow _compose;

    // Pool of list rows (avoid Instantiate/Destroy per navigation)
    private readonly List<EmailListItem> _rowPool = new List<EmailListItem>(32);

    // Full email dataset — built once in Awake
    private readonly List<EmailData> _emails = new List<EmailData>(32);

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildMockData();
        WireSidebarButtons();
        WireSearchField();
        WireRefreshButton();
        WireCategoryButtons();
        WireCompose();
        StyleCategoryChips(); // Batch UI-4: visual differentiation only, no filtering changes

        // FIX Phase2: Removed contentVLG.spacing=4 override — the 1px Divider child
        // inside each email row already provides visual row separation. VLG spacing=4
        // added a second gap on top of the Divider, causing double spacing.
    }

    private void Start()
    {
        ShowInbox();
        SetActiveCategoryButton(categoryInboxButton); // default category highlight
    }

    // ── Folder navigation ─────────────────────────────────────────────────

    public void ShowInbox()  => SelectFolder(EmailFolder.Inbox,  "Inbox",  sidebarInbox);
    public void ShowSent()   => SelectFolder(EmailFolder.Sent,   "Sent",   sidebarSent);
    public void ShowDrafts() => SelectFolder(EmailFolder.Drafts, "Drafts", sidebarDrafts);
    public void ShowSpam()   => SelectFolder(EmailFolder.Spam,   "Spam",   sidebarSpam);
    public void ShowTrash()  => SelectFolder(EmailFolder.Trash,  "Trash",  sidebarTrash);

    public void SelectFolder(EmailFolder folder, string title, Button sidebarBtn = null)
    {
        _currentFolder = folder;

        // Batch 4: sidebar active state
        SetActiveSidebarButton(sidebarBtn);

        // Clear search so folder switch shows all
        if (searchField != null) searchField.SetTextWithoutNotify("");

        if (folderTitle != null) folderTitle.text = title;
        if (previewPanel != null) previewPanel.ShowEmpty();
        _selectedItem = null;

        RebuildList(); // folder + category + (cleared) search
    }

    // ── List building ─────────────────────────────────────────────────────

    private void BuildEmailList(List<EmailData> emails)
    {
        if (listContent == null || listItemTemplate == null) return;

        // Return active rows to pool — explicitly skip the template itself.
        // listItemTemplate lives as a child of listContent (inactive, for layout/style
        // reference) and also has an EmailListItem component, so without this guard it
        // gets swept into the pool and can be rented out as a live row — corrupting the
        // template and producing a visible ghost row bound to real email data.
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            var child = listContent.GetChild(i);
            if (child.gameObject == listItemTemplate) continue; // never pool/rent the template
            var item = child.GetComponent<EmailListItem>();
            if (item != null)
            {
                child.gameObject.SetActive(false);
                _rowPool.Add(item);
            }
        }

        // Populate
        int unreadCount = 0;
        for (int i = 0; i < emails.Count; i++)
        {
            var item = RentRow();
            var capture = i; // capture for lambda
            var data = emails[i];
            item.Bind(data, _ => OnEmailSelected(capture, emails), i);
            item.gameObject.SetActive(true);
            item.transform.SetParent(listContent, false);
            item.transform.SetAsLastSibling();
            if (data.isUnread) unreadCount++;
        }

        // Update count and status
        if (countLabel != null)
            countLabel.text = unreadCount > 0 ? $"{unreadCount} unread" : "";
        if (statusLabel != null)
            statusLabel.text = $"{emails.Count} message{(emails.Count != 1 ? "s" : "")}"  ;
    }

    private void OnEmailSelected(int index, List<EmailData> emailsInView)
    {
        if (index < 0 || index >= emailsInView.Count) return;

        // Deselect previous
        if (_selectedItem != null)
            _selectedItem.SetSelected(false);

        // Find the list item at this index
        _selectedItem = null;
        for (int i = 0; i < listContent.childCount; i++)
        {
            var item = listContent.GetChild(i).GetComponent<EmailListItem>();
            if (item != null && item.BoundIndex == index)
            {
                _selectedItem = item;
                break;
            }
        }

        if (_selectedItem != null)
            _selectedItem.SetSelected(true);

        var data = emailsInView[index];

        // Mark unread → read in master dataset
        if (data.isUnread)
        {
            // Find and update in _emails list
            for (int j = 0; j < _emails.Count; j++)
            {
                var e = _emails[j];
                if (e.sender == data.sender && e.subject == data.subject &&
                    e.timestamp == data.timestamp)
                {
                    e.isUnread = false;
                    _emails[j] = e;
                    break;
                }
            }
            // Update emailsInView entry too
            data.isUnread = false;
            emailsInView[index] = data;

            _selectedItem?.MarkRead();

            // Refresh unread count
            int unread = 0;
            foreach (var em in emailsInView) if (em.isUnread) unread++;
            if (countLabel != null)
                countLabel.text = unread > 0 ? $"{unread} unread" : "";
        }

        if (statusLabel != null) statusLabel.text = $"From: {data.sender}";
        if (previewPanel != null) previewPanel.ShowEmail(data);
    }

    // ── Batch 4: Search ───────────────────────────────────────────────────

    private void OnSearchChanged(string query) => RebuildList();

    /// <summary>
    /// Single source of truth for what's currently shown:
    /// folder filter -> category filter -> search filter (in that order), then BuildEmailList.
    /// Called by SelectFolder, category buttons, search field, and RefreshCurrentFolder.
    /// </summary>
    private void RebuildList()
    {
        var byFolder   = GetCurrentFolderEmails();
        var byCategory = ApplyCategoryFilter(byFolder, _activeCategory);
        var query      = searchField != null ? searchField.text : null;
        var final      = ApplySearchFilter(byCategory, query);
        BuildEmailList(final);
    }

    private static List<EmailData> ApplyCategoryFilter(List<EmailData> source, EmailCategory category)
    {
        var result = new List<EmailData>(source.Count);
        foreach (var e in source)
        {
            bool include = category switch
            {
                EmailCategory.Inbox     => !e.isDeleted && !e.isSpam,
                EmailCategory.Important => e.isImportant,
                EmailCategory.Spam      => e.isSpam,
                EmailCategory.Deleted   => e.isDeleted,
                _                       => true,
            };
            if (include) result.Add(e);
        }
        return result;
    }

    private static List<EmailData> ApplySearchFilter(List<EmailData> source, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return source;

        query = query.ToLowerInvariant();
        var filtered = new List<EmailData>(source.Count);
        foreach (var e in source)
        {
            if (e.sender.ToLowerInvariant().Contains(query)  ||
                e.subject.ToLowerInvariant().Contains(query) ||
                e.preview.ToLowerInvariant().Contains(query))
                filtered.Add(e);
        }
        return filtered;
    }

    // ── Batch 4: Sidebar active state ─────────────────────────────────────

    private void SetActiveSidebarButton(Button btn)
    {
        // Reset previous
        if (_activeSidebarButton != null)
        {
            var prev = _activeSidebarButton.GetComponent<Image>();
            if (prev != null) FadeImageColor(prev, SidebarNormal, SidebarFadeDuration);
        }

        _activeSidebarButton = btn;

        if (_activeSidebarButton != null)
        {
            var img = _activeSidebarButton.GetComponent<Image>();
            if (img != null) FadeImageColor(img, SidebarActive, SidebarFadeDuration);
        }
    }

    private static readonly Color SidebarNormal = new Color(0.12f, 0.13f, 0.16f, 1f);
    private static readonly Color SidebarActive = new Color(0.12f, 0.22f, 0.40f, 1f);
    private const float SidebarFadeDuration = 0.10f;  // Batch UI-10 spec

    // Batch UI-10: per-Image fade tracking so rapid folder switching never leaves a
    // previous in-flight fade fighting a new one on the same button.
    private readonly Dictionary<Image, Coroutine> _imageFades = new Dictionary<Image, Coroutine>();

    private void FadeImageColor(Image img, Color target, float duration)
    {
        if (img == null) return;
        if (_imageFades.TryGetValue(img, out var existing) && existing != null)
            StopCoroutine(existing);
        _imageFades[img] = StartCoroutine(ImageColorFadeRoutine(img, target, duration));
    }

    private IEnumerator ImageColorFadeRoutine(Image img, Color target, float duration)
    {
        // EmailHoverFeedback (Batch UI-8) writes to this same Image.color instantly on
        // pointer events. A click naturally fires Down+Up on the element being selected,
        // which would otherwise interrupt/fight this fade mid-transition. Pausing it for
        // the fade's duration only (then restoring) avoids touching EmailHoverFeedback.cs
        // itself while eliminating the race.
        var hoverFeedback = img.GetComponent<EmailHoverFeedback>();
        if (hoverFeedback != null) hoverFeedback.enabled = false;

        Color start = img.color;
        if (duration <= 0f)
        {
            img.color = target;
            _imageFades.Remove(img);
            if (hoverFeedback != null) hoverFeedback.enabled = true;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            img.color = Color.LerpUnclamped(start, target, Mathf.Clamp01(t));
            yield return null;
        }
        img.color = target;
        _imageFades.Remove(img);
        if (hoverFeedback != null) hoverFeedback.enabled = true;
    }

    // ── Batch 5: Category filter + visual state ───────────────────────────

    public void SetCategoryInbox()     => SelectCategory(EmailCategory.Inbox,     categoryInboxButton);
    public void SetCategoryImportant() => SelectCategory(EmailCategory.Important, categoryImportantButton);
    public void SetCategorySpam()      => SelectCategory(EmailCategory.Spam,      categorySpamButton);
    public void SetCategoryDeleted()   => SelectCategory(EmailCategory.Deleted,   categoryDeletedButton);

    private void SelectCategory(EmailCategory category, Button categoryBtn)
    {
        _activeCategory = category;
        SetActiveCategoryButton(categoryBtn);
        _selectedItem = null;
        if (previewPanel != null) previewPanel.ShowEmpty();
        RebuildList();
    }

    private void SetActiveCategoryButton(Button btn)
    {
        // Chip visual state — distinct from sidebar's highlight (different colors, bold text,
        // rounded pill shape). Only one chip active at a time. Filtering logic unaffected.
        if (_activeCategoryButton != null)
            ApplyChipVisual(_activeCategoryButton, active: false);

        _activeCategoryButton = btn;

        if (_activeCategoryButton != null)
            ApplyChipVisual(_activeCategoryButton, active: true);
    }

    private const float ChipFadeDuration = 0.12f;  // Batch UI-10 spec
    private readonly Dictionary<TextMeshProUGUI, Coroutine> _labelFades = new Dictionary<TextMeshProUGUI, Coroutine>();

    private void ApplyChipVisual(Button chip, bool active)
    {
        var img = chip.GetComponent<Image>();
        if (img != null) FadeImageColor(img, active ? ChipBgActive : ChipBgNormal, ChipFadeDuration);

        var label = chip.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (label != null)
        {
            // Bold/Normal can't be cross-faded (different glyph rendering, not a continuous
            // value) — toggle it instantly; only the color cross-fades.
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            FadeLabelColor(label, active ? ChipTextActive : ChipTextNormal, ChipFadeDuration);
        }
    }

    private void FadeLabelColor(TextMeshProUGUI label, Color target, float duration)
    {
        if (label == null) return;
        if (_labelFades.TryGetValue(label, out var existing) && existing != null)
            StopCoroutine(existing);
        _labelFades[label] = StartCoroutine(LabelColorFadeRoutine(label, target, duration));
    }

    private IEnumerator LabelColorFadeRoutine(TextMeshProUGUI label, Color target, float duration)
    {
        Color start = label.color;
        if (duration <= 0f) { label.color = target; _labelFades.Remove(label); yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            label.color = Color.LerpUnclamped(start, target, Mathf.Clamp01(t));
            yield return null;
        }
        label.color = target;
        _labelFades.Remove(label);
    }

    /// <summary>
    /// One-time visual pass: gives each category button a rounded-pill shape, a small
    /// height (28px) and chip-style sizing — distinct from the sidebar's square nav buttons.
    /// Filtering/click logic is untouched; this only touches Image.sprite/type and RectTransform.
    /// </summary>
    private void StyleCategoryChips()
    {
        var sprite = GetOrBuildChipSprite();
        foreach (var chip in new[] { categoryInboxButton, categoryImportantButton,
                                      categorySpamButton, categoryDeletedButton })
        {
            if (chip == null) continue;

            var img = chip.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = sprite;
                img.type   = Image.Type.Sliced; // 9-slice — rounded corners stay crisp at any width

                // Batch UI-8: hover/pressed feedback, distinct from the active state
                // (chip turns green when filtering is active — see ApplyChipVisual).
                // Base color resolved fresh per pointer-exit so it tracks the active chip
                // correctly even if the active filter changes while another chip is hovered.
                var chipRef = chip; // capture for closure
                var feedback = chip.gameObject.AddComponent<EmailHoverFeedback>();
                feedback.Init(img, () => _activeCategoryButton == chipRef ? ChipBgActive : ChipBgNormal);
            }

            var label = chip.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.fontSize = 11f; // slightly larger than the previous 10pt — chip readability

            var le = chip.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = 28f; // Gmail-chip height (28-30px range)
                le.minHeight       = 28f;
                // Width sized per-label so longer chip text ("Important", "Deleted") doesn't clip.
                // ~7.5px per character at 11pt bold + 24px horizontal padding for the pill shape.
                string text = label != null ? label.text : "";
                le.preferredWidth = Mathf.Max(52f, text.Length * 7.5f + 24f);
                le.minWidth       = le.preferredWidth;
            }

            // Set initial chip state INSTANTLY — do NOT call ApplyChipVisual/FadeImageColor here.
            // StyleCategoryChips() runs from Awake(), which fires even while EmailAppWindow is
            // inactive-in-hierarchy (Email App parent is closed at game start). StartCoroutine
            // inside FadeImageColor advances the IEnumerator synchronously to its first yield,
            // setting hoverFeedback.enabled = false — but the GO never becoming active means
            // the coroutine never continues, so enabled is never restored to true. Bypass the
            // fade entirely and write the initial resting state directly.
            if (img   != null) img.color     = ChipBgNormal;
            if (label != null) { label.color = ChipTextNormal; label.fontStyle = FontStyles.Normal; }
        }
    }

    /// <summary>
    /// Builds a small rounded-pill sprite procedurally (no asset import) and caches it.
    /// Uses Image.Type.Sliced via sprite border so the pill scales cleanly to any chip width.
    /// </summary>
    private static Sprite GetOrBuildChipSprite()
    {
        if (_chipSprite != null) return _chipSprite;

        const int size   = 28;   // texture is square; corner radius = size/2 for a full pill
        const int radius = 14;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 1f;
                // Rounded-corner mask: treat each corner as a circle of `radius`
                int cx = x < radius ? radius : (x >= size - radius ? size - radius - 1 : x);
                int cy = y < radius ? radius : (y >= size - radius ? size - radius - 1 : y);
                bool inCornerZone = (x < radius || x >= size - radius) && (y < radius || y >= size - radius);
                if (inCornerZone)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = dist <= radius ? 1f : 0f;
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        // Border = corner radius on all sides so 9-slice keeps corners round at any chip width.
        _chipSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        _chipSprite.name = "EmailApp_ChipPill";
        return _chipSprite;
    }

    // Gmail-chip palette — deliberately distinct from sidebar's blue navigation highlight
    // (0.2,0.4,0.8) so filters never look like folder navigation at a glance.
    private static readonly Color ChipBgNormal     = new Color(0.20f, 0.21f, 0.25f, 1f);   // neutral grey pill
    private static readonly Color ChipBgActive     = new Color(0.35f, 0.62f, 0.42f, 1f);   // green — "filter applied"
    private static readonly Color ChipTextNormal   = new Color(0.72f, 0.74f, 0.78f, 1f);
    private static readonly Color ChipTextActive   = new Color(0.06f, 0.10f, 0.07f, 1f);   // dark text on green
    private static Sprite _chipSprite; // procedural rounded-pill sprite, built once and cached

    // ── Batch 4: Refresh ──────────────────────────────────────────────────

    public void RefreshCurrentFolder() => RebuildList();

    // ── Batch 7: Notification bridge ───────────────────────────────────────
    // Infrastructure only — no timed/narrative triggering yet (that's Batch 8).
    // Reuses the existing FsStatusToast queue (File Explorer's toast system).

    /// <summary>
    /// Adds a new email to the dataset, refreshes counts/list if the email lands
    /// in the currently visible folder+category+search combo, and shows a toast.
    /// Single entry point for all future email-arrival triggers (Batch 8).
    /// </summary>
    public void ReceiveEmail(EmailData email)
    {
        _emails.Add(email);

        // Only rebuild the visible list if this email would actually appear in it —
        // avoids unnecessary pool churn when an email lands in a folder/category
        // the player isn't currently looking at.
        if (email.folder == _currentFolder)
            RebuildList();

        ShowEmailToast(email);
    }

    private static void ShowEmailToast(EmailData email)
    {
        // Format per spec: title "New Email", body "{sender}\n{subject}".
        // FsStatusToast splits on " — " for title/subtitle — sender+subject must be
        // combined into the subtitle line since the toast only supports one subtitle line.
        // Use "New Email" as title, "{sender}: {subject}" as the single subtitle line.
        string title    = "\uD83D\uDCE7 New Email"; // 📧 New Email
        string subtitle = $"{email.sender}: {email.subject}";
        FsStatusToast.ShowGlobal($"{title} \u2014 {subtitle}");
    }

    // ── Batch 7: Debug test helper — REMOVE/REPLACE in Batch 8 ─────────────
    /// <summary>Temporary verification helper. Spawns one fake email through ReceiveEmail().</summary>
    public void DebugSpawnTestEmail()
    {
        var testEmail = new EmailData
        {
            sender      = "Security Team",
            recipient   = "root",
            subject     = "Security Notice: Unusual Login Detected",
            preview     = "We noticed a login from an unrecognized device.",
            body        = "We noticed a login from an unrecognized device. If this wasn't you, secure your account immediately.\n\nSECURITY TEAM",
            timestamp   = "Just now",
            isUnread    = true,
            folder      = EmailFolder.Inbox,
            isImportant = false,
            isSpam      = false,
            isDeleted   = false,
            attachments = null,
        };
        ReceiveEmail(testEmail);
    }

    // ── Wiring ────────────────────────────────────────────────────────────

    private void WireSidebarButtons()
    {
        // Phase2: reset every sidebar button to SidebarNormal before the runtime selection
        // system takes over. Prevents any stale baked-in color (set by a prior Editor session
        // or previous Play run) from making a non-active button look selected at startup.
        foreach (var b in new[] { sidebarInbox, sidebarSent, sidebarDrafts, sidebarSpam, sidebarTrash })
        {
            if (b == null) continue;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = SidebarNormal;
        }
        Wire(sidebarInbox,  ShowInbox);
        Wire(sidebarSent,   ShowSent);
        Wire(sidebarDrafts, ShowDrafts);
        Wire(sidebarSpam,   ShowSpam);
        Wire(sidebarTrash,  ShowTrash);

        void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(a);

            // Batch UI-8: hover/pressed feedback. Base color depends on whether THIS button
            // is the currently active sidebar button — resolved fresh on every pointer-exit/up
            // so it never goes stale if the active button changes while hovering a different one.
            var img = b.GetComponent<Image>();
            if (img != null)
            {
                var feedback = b.gameObject.AddComponent<EmailHoverFeedback>();
                feedback.Init(img, () => _activeSidebarButton == b ? SidebarActive : SidebarNormal);
            }
        }
    }

    private void WireCategoryButtons()
    {
        WireCat(categoryInboxButton,     SetCategoryInbox);
        WireCat(categoryImportantButton, SetCategoryImportant);
        WireCat(categorySpamButton,      SetCategorySpam);
        WireCat(categoryDeletedButton,   SetCategoryDeleted);

        static void WireCat(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(a);
        }
    }

    private void WireSearchField()
    {
        if (searchField == null) return;
        searchField.onValueChanged.RemoveAllListeners();
        searchField.onValueChanged.AddListener(OnSearchChanged);
    }

    private void WireRefreshButton()
    {
        if (refreshButton == null) return;
        refreshButton.onClick.RemoveAllListeners();
        refreshButton.onClick.AddListener(RefreshCurrentFolder);

        // Batch UI-8: hover/pressed feedback — fixed base color, no active-state toggling.
        var img = refreshButton.GetComponent<Image>();
        if (img != null)
        {
            var baseColor = img.color;
            var feedback = refreshButton.gameObject.AddComponent<EmailHoverFeedback>();
            feedback.Init(img, () => baseColor);
        }
    }

    // ── Pool helpers ──────────────────────────────────────────────────────

    private EmailListItem RentRow()
    {
        for (int i = _rowPool.Count - 1; i >= 0; i--)
        {
            var pooled = _rowPool[i];
            if (pooled == null) continue;
            if (listItemTemplate != null && pooled.gameObject == listItemTemplate) continue; // never rent the template
            _rowPool.RemoveAt(i);
            return pooled;
        }
        var clone = Instantiate(listItemTemplate, listContent);
        var item  = clone.GetComponent<EmailListItem>();
        if (item == null) item = clone.AddComponent<EmailListItem>();
        return item;
    }

    // ── Query helpers ─────────────────────────────────────────────────────

    private List<EmailData> GetCurrentFolderEmails()
    {
        var result = new List<EmailData>(_emails.Count);
        foreach (var e in _emails)
            if (e.folder == _currentFolder)
                result.Add(e);
        return result;
    }

    // ── Mock dataset ──────────────────────────────────────────────────────

    private void BuildMockData()
    {
        _emails.Clear();

        // Inbox — mix of unread and read
        Add("ADMIN@GLITCH.NET",    "root",             "[SYSTEM] Anomaly detected on node 7",
            "Automated diagnostic flagged sector 7. Immediate review required.",
            "Automated diagnostic flagged sector 7—access logs show unauthorized traversal. Immediate review is required. Archive all relevant files before the next maintenance window.\n\nADMIN SYSTEM",
            "Today 09:14", true,  EmailFolder.Inbox, isImportant: true,
            attachments: new[] { "/Documents/Work/Reports/q1_report.txt" });

        Add("vera.k@synthcorp.io", "root",             "Re: Project MIRROR files",
            "I sent over the transfer manifest. Did you receive—",
            "I sent over the transfer manifest this morning. Did you receive it? The window closes at midnight.\n\nV.",
            "Today 08:47", true,  EmailFolder.Inbox,
            attachments: new[] { "/Downloads/manual.pdf", "/Documents/todo.txt" });

        Add("noreply@uplink.net",  "root",             "Your account access report",
            "3 new sign-in events detected on your profile.",
            "3 new sign-in events were detected on your account from unrecognized devices.\n\nIf this was not you, change your credentials immediately.\n\nUPLINK SECURITY",
            "Yesterday",  false, EmailFolder.Inbox);

        Add("j.cross@redacted.org","root",             "Quiet question",
            "Don't reply on the main line. Use the address below.",
            "Don't reply on the main line. Use the address below. They're monitoring traffic.\n\n— J",
            "Mon 22:03",  true,  EmailFolder.Inbox, isImportant: true);

        Add("newsletter@devnull.io","root",            "Weekly digest — issue 41",
            "Top stories: Grid outage, silent patch rollout, forum thread...",
            "This week: Grid outage in sector 9, silent patch pushed to legacy nodes, forum thread on the MIRROR breach goes private.\n\nUNSUBSCRIBE | DEVNULL MEDIA",
            "Sun 07:00",  false, EmailFolder.Inbox);

        // Sent
        Add("root",               "vera.k@synthcorp.io","Re: Project MIRROR files",
            "Got it. Burning the original after review.",
            "Got it. Burning the original after review. Don't send anything else to this address.\n\n—",
            "Today 09:01", false, EmailFolder.Sent);

        Add("root",               "ADMIN@GLITCH.NET",  "Node 7 access confirmed",
            "Access was authorised. Disregard the alert.",
            "Access was authorised under maintenance token 8834-G. Disregard the automated alert.\n\noperator",
            "Today 09:20", false, EmailFolder.Sent);

        // Drafts
        Add("root",               "???",               "[DRAFT] What I found",
            "I don't know who to send this to. The logs show—",
            "I don't know who to send this to. The logs show the patch wasn't silent—it was targeted. Someone knew which nodes to hit and when. I need more time.\n\n[UNSENT]",
            "Draft",      true,  EmailFolder.Drafts);

        // Spam — isSpam=true so it's excluded from the Inbox category even if folder changes
        Add("promo@deals4u.biz",  "root",              "You've been selected!",
            "Congratulations. Claim your exclusive access token now.",
            "Congratulations — you have been randomly selected for an exclusive platform access token. Click below to claim within 24 hours.\n\nDEALS4U",
            "3 days ago", false, EmailFolder.Spam, isSpam: true);

        // Trash — isDeleted=true so it surfaces under the Deleted category too
        Add("ADMIN@GLITCH.NET",   "root",              "[AUTO] Routine check passed",
            "All systems nominal. No action required.",
            "All systems nominal. Diagnostic cycle complete. No action required.\n\nADMIN SYSTEM",
            "Last week",  false, EmailFolder.Trash, isDeleted: true);

        // Deleted-from-inbox example — folder stays Inbox, but isDeleted hides it from
        // the Inbox category while still being reachable via the Deleted category.
        Add("ADMIN@GLITCH.NET",   "root",              "[AUTO] Old anomaly report (resolved)",
            "Sector 4 anomaly resolved. Archived for reference.",
            "Sector 4 anomaly resolved automatically. This entry is retained for reference only.\n\nADMIN SYSTEM",
            "2 weeks ago", false, EmailFolder.Inbox, isDeleted: true);
    }

    private void Add(string from, string to, string subject, string preview,
                     string body, string timestamp, bool unread, EmailFolder folder,
                     bool isImportant = false, bool isSpam = false, bool isDeleted = false,
                     string[] attachments = null)
    {
        _emails.Add(new EmailData
        {
            sender      = from,
            recipient   = to,
            subject     = subject,
            preview     = preview,
            body        = body,
            timestamp   = timestamp,
            isUnread    = unread,
            folder      = folder,
            isImportant = isImportant,
            isSpam      = isSpam,
            isDeleted   = isDeleted,
            attachments = attachments,
        });
    }

    // ── Batch 7: Compose ─────────────────────────────────────────────────

    public void OpenCompose()
    {
        if (_compose == null)
        {
            // STEP 1: reuse a scene-saved ComposeModal if one already exists, instead of
            // building a duplicate. Evidence (this session): a ComposeModal was found active
            // in the scene before any EmailApp lifecycle method had run (_emails.Count==0,
            // _compose==null) — proof it is stale scene data, not something this script created.
            var existing = transform.Find("ComposeModal")?.GetComponent<ComposeEmailWindow>();
            if (existing != null)
            {
                _compose = existing;
                // Force it inactive immediately — the scene-saved instance was found active
                // with Panel.CanvasGroup.alpha=1. Reset to a known-closed state before Init()
                // wires anything, so it never flashes open on this or any future load.
                var panel = existing.transform.Find("Panel");
                var panelCG = panel?.GetComponent<CanvasGroup>();
                if (panelCG != null) panelCG.alpha = 0f;
                if (panel != null) panel.gameObject.SetActive(false);
                // Also zero backdrop alpha — stale scene modal had alpha=1 serialized
                var backdropCG = existing.transform.Find("Backdrop")?.GetComponent<CanvasGroup>();
                if (backdropCG != null) backdropCG.alpha = 0f;
                existing.gameObject.SetActive(false);

                // STEP 2: Init() must still run exactly once on this reused instance so
                // Close/Cancel/Save Draft get wired — BuildComposeModal() never ran this
                // session, so Init() never ran either (RemoveAllListeners+AddListener are
                // runtime-only, not persisted in the scene file).
                InitExistingCompose(existing);
            }
            else
            {
                // STEP 3: only build a new one if no existing instance was found.
                BuildComposeModal();
            }
        }
        _compose?.Show();
    }

    /// <summary>
    /// STEP 2: wires Close/Cancel/Save Draft on a ComposeModal that already existed in the
    /// scene (rather than one just built by BuildComposeModal, which wires + calls Init()
    /// itself). Locates the same child names BuildComposeModal creates, so this only runs
    /// successfully if the existing instance has the expected hierarchy.
    /// </summary>
    private void InitExistingCompose(ComposeEmailWindow compose)
    {
        var t = compose.transform;
        var panel       = t.Find("Panel");
        RepairComposePanelLayout(panel); // Phase2: repair stale LE defaults on scene-saved modal
        var closeBtn    = panel?.Find("TitleBar/CloseButton")?.GetComponent<Button>();
        var toRow       = panel?.Find("ToRow/Input")?.GetComponent<TMP_InputField>();
        var subjectRow  = panel?.Find("SubjectRow/Input")?.GetComponent<TMP_InputField>();
        var bodyField   = panel?.Find("BodyScrollView/Viewport/Input")?.GetComponent<TMP_InputField>();
        var actionBar   = panel?.Find("ActionBar");
        Button saveBtn = null, cancelBtn = null;
        if (actionBar != null)
        {
            saveBtn   = actionBar.Find("Save Draft")?.GetComponent<Button>();
            cancelBtn = actionBar.Find("Discard")?.GetComponent<Button>();
        }

        var cFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cType  = typeof(ComposeEmailWindow);
        cType.GetField("toField",        cFlags)?.SetValue(compose, toRow);
        cType.GetField("subjectField",   cFlags)?.SetValue(compose, subjectRow);
        cType.GetField("bodyField",      cFlags)?.SetValue(compose, bodyField);
        cType.GetField("saveDraftButton",cFlags)?.SetValue(compose, saveBtn);
        cType.GetField("cancelButton",   cFlags)?.SetValue(compose, cancelBtn);
        cType.GetField("closeButton",    cFlags)?.SetValue(compose, closeBtn);

        // Init() internally calls RemoveAllListeners() before AddListener() on each button,
        // so even if this somehow ran twice, listeners would not duplicate. It also ends
        // with gameObject.SetActive(false), reinforcing the closed state set above.
        compose.Init(SaveDraft);
    }

    /// <summary>Phase2: sets LE.flexibleHeight=0 and HLG.childForceExpandHeight=false on
    /// every fixed-height Compose row. Needed when reusing a scene-saved ComposeModal that
    /// was created before these Phase2 fixes were added to BuildComposeModal — the serialized
    /// LE defaults (-1) cause the Panel VLG to distribute flexible space equally across all
    /// rows, inflating them from their intended heights (36/36/36/46px) to ~61/61/61/71px.</summary>
    private static void RepairComposePanelLayout(Transform panel)
    {
        if (panel == null) return;
        foreach (var rowName in new[] { "TitleBar", "ToRow", "SubjectRow", "ActionBar" })
        {
            var child = panel.Find(rowName);
            if (child == null) continue;
            var le  = child.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le  != null) le.flexibleHeight = 0f;
            var hlg = child.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (hlg != null) hlg.childForceExpandHeight = false;
        }
    }

    private void SaveDraft(EmailData draft)
    {
        _emails.Add(draft);
        // If the player is currently viewing Drafts, refresh immediately.
        if (_currentFolder == EmailFolder.Drafts)
            RebuildList();
    }

    private void WireCompose()
    {
        // ComposeButton lives in FloatingPanel/Toolbar/ComposeButton — relative to THIS
        // window (EmailAppWindow), not its parent shell. transform.parent was wrong:
        // it pointed to the "Email App" shell, which has no FloatingPanel child.
        var compBtn = transform.Find("FloatingPanel/Toolbar/ComposeButton")
                   ?.GetComponent<Button>();
        if (compBtn != null)
        {
            compBtn.interactable = true;
            compBtn.onClick.RemoveAllListeners();
            compBtn.onClick.AddListener(OpenCompose);

            // Batch UI-8: hover/pressed feedback. Compose button's background never changes
            // outside of this (no active-state toggling), so base color is fixed at whatever
            // was painted on the Image at wiring time.
            var img = compBtn.GetComponent<Image>();
            if (img != null)
            {
                var baseColor = img.color;
                var feedback = compBtn.gameObject.AddComponent<EmailHoverFeedback>();
                feedback.Init(img, () => baseColor);
            }
        }
    }

    private void BuildComposeModal()
    {
        // Build modal GO as a child of this window — always on top via SetAsLastSibling()
        var mo = new GameObject("ComposeModal", typeof(RectTransform));
        mo.transform.SetParent(transform, false);
        var moRT = mo.GetComponent<RectTransform>();
        moRT.anchorMin = Vector2.zero; moRT.anchorMax = Vector2.one;
        moRT.offsetMin = Vector2.zero; moRT.offsetMax = Vector2.zero;

        // Semi-transparent backdrop — darker than before for stronger elevation above EmailApp
        var bdGO = new GameObject("Backdrop", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        bdGO.transform.SetParent(mo.transform, false);
        var bdRT = bdGO.GetComponent<RectTransform>();
        bdRT.anchorMin = Vector2.zero; bdRT.anchorMax = Vector2.one;
        bdRT.offsetMin = Vector2.zero; bdRT.offsetMax = Vector2.zero;
        bdGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.62f);
        // Batch UI-10: independent alpha-only fade target (no scale — a fullscreen dim
        // shouldn't shrink). Starts at 0; ComposeEmailWindow fades it in/out alongside Panel.
        var bdCG = bdGO.AddComponent<CanvasGroup>();
        bdCG.alpha = 0f;

        // ── Panel ── elevated card with border + shadow-like outline for depth
        var pnGO = new GameObject("Panel", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.VerticalLayoutGroup));
        pnGO.transform.SetParent(mo.transform, false);
        var pnRT = pnGO.GetComponent<RectTransform>();
        pnRT.anchorMin = new Vector2(0.5f, 0.5f); pnRT.anchorMax = new Vector2(0.5f, 0.5f);
        pnRT.pivot     = new Vector2(0.5f, 0.5f); pnRT.sizeDelta = new Vector2(440f, 420f);
        pnGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.13f, 0.14f, 0.17f, 1f);
        // Subtle outline for elevation — panel reads as "above" the email window
        var pnOutline = pnGO.AddComponent<UnityEngine.UI.Outline>();
        pnOutline.effectColor    = new Color(0f, 0f, 0f, 0.5f);
        pnOutline.effectDistance = new Vector2(0f, -3f);
        pnOutline.useGraphicAlpha = false;

        // Batch UI-10: Panel motion — reuses the existing WindowAnimator pattern (the same
        // class every other app window in this project already uses for open/close
        // scale+fade). Configured here via reflection to match the exact spec values
        // (180ms/0.94 open, 120ms/0.97 close) without altering WindowAnimator's shared
        // public surface used by every other window.
        // FIX (regressions #1/#2): deactivate BEFORE AddComponent<WindowAnimator>, not after.
        // AddComponent on an ACTIVE GameObject fires Awake() synchronously; WindowAnimator's
        // Awake() reads gameObject.activeSelf + CanvasGroup.alpha to seed IsLogicallyOpen.
        // With the old order, Panel was still active at that exact moment (a fresh
        // CanvasGroup defaults to alpha=1), so IsLogicallyOpen was incorrectly set to
        // true during construction, before any real Show() ever happened. Deactivating
        // first defers Awake() until the real first AnimateOpen() call.
        pnGO.SetActive(false);
        pnGO.AddComponent<CanvasGroup>();
        var pnAnimator = pnGO.AddComponent<WindowAnimator>();
        var animFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(WindowAnimator).GetField("openDuration",   animFlags)?.SetValue(pnAnimator, 0.18f);
        typeof(WindowAnimator).GetField("openStartScale", animFlags)?.SetValue(pnAnimator, 0.94f);
        typeof(WindowAnimator).GetField("closeDuration",  animFlags)?.SetValue(pnAnimator, 0.12f);
        typeof(WindowAnimator).GetField("closeEndScale",  animFlags)?.SetValue(pnAnimator, 0.97f);

        var vlg = pnGO.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 0, 0); vlg.spacing = 0;
        vlg.childControlWidth = true; vlg.childControlHeight = true;  // FIX (Batch UI-9B): was false —
        // VLG never applied any child's preferredHeight/flexibleHeight, so every direct child
        // (TitleBar, ToRow, dividers, SubjectRow, BodyScrollView, ActionBar) rendered at Unity's
        // default sizeDelta=(100,100) instead of its intended height. See root-cause analysis.
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // ── TitleBar (36px) ───────────────────────────────────────────────
        var tbGO = new GameObject("TitleBar", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        tbGO.transform.SetParent(pnGO.transform, false);
        var tbLE = tbGO.AddComponent<UnityEngine.UI.LayoutElement>();
        tbLE.preferredHeight = 36f;
        tbLE.flexibleHeight  = 0f; // Phase2: TitleBar is fixed-height — no flex growth
        tbGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.11f, 0.13f, 1f);
        var tbHLG = tbGO.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        tbHLG.padding = new RectOffset(14, 8, 0, 0); tbHLG.spacing = 6;
        tbHLG.childAlignment = TextAnchor.MiddleLeft;
        tbHLG.childControlWidth = false; tbHLG.childControlHeight = true;
        tbHLG.childForceExpandWidth = false; tbHLG.childForceExpandHeight = false; // Phase2

        var titleLblGO = new GameObject("TitleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleLblGO.transform.SetParent(tbGO.transform, false);
        titleLblGO.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth = 1f;
        var titleLbl = titleLblGO.GetComponent<TMPro.TextMeshProUGUI>();
        titleLbl.text = "New Message"; titleLbl.fontSize = 13f; titleLbl.fontStyle = TMPro.FontStyles.Bold;
        titleLbl.color = new Color(0.88f, 0.89f, 0.92f, 1f);
        titleLbl.alignment = TMPro.TextAlignmentOptions.MidlineLeft; titleLbl.raycastTarget = false;

        var closeGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        closeGO.transform.SetParent(tbGO.transform, false);
        closeGO.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 32f;
        var closeImg = closeGO.GetComponent<UnityEngine.UI.Image>();
        closeImg.color = new Color(1f, 1f, 1f, 0.18f); // clearly visible close button
        var closeBtnComp = closeGO.GetComponent<UnityEngine.UI.Button>();
        // Hover feedback via ColorTint — same Button component, no extra script needed
        var closeColors = closeBtnComp.colors;
        closeColors.normalColor      = new Color(1f, 1f, 1f, 0.18f);
        closeColors.highlightedColor = new Color(1f, 0.30f, 0.30f, 0.85f);
        closeColors.pressedColor     = new Color(0.80f, 0.18f, 0.18f, 1f);
        closeColors.fadeDuration     = 0.08f;
        closeBtnComp.colors = closeColors;

        var closeLblGO = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        closeLblGO.transform.SetParent(closeGO.transform, false);
        var closeLblRT = closeLblGO.GetComponent<RectTransform>();
        closeLblRT.anchorMin = Vector2.zero; closeLblRT.anchorMax = Vector2.one;
        closeLblRT.offsetMin = Vector2.zero; closeLblRT.offsetMax = Vector2.zero;
        var closeLbl = closeLblGO.GetComponent<TMPro.TextMeshProUGUI>();
        closeLbl.text = "X"; closeLbl.fontSize = 13f;
        closeLbl.color = Color.white;
        closeLbl.alignment = TMPro.TextAlignmentOptions.Center; closeLbl.raycastTarget = false;

        // ── Divider ───────────────────────────────────────────────────────
        MakeDivider(pnGO.transform);

        // ── Helper: labelled row, label LEFT, field RIGHT ──────────────────
        TMP_InputField MakeFieldRow(string labelText, float rowHeight = 36f)
        {
            var rowGO = new GameObject(labelText + "Row", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            rowGO.transform.SetParent(pnGO.transform, false);
            var rowLE = rowGO.AddComponent<UnityEngine.UI.LayoutElement>();
            rowLE.preferredHeight = rowHeight;
            rowLE.flexibleHeight  = 0f; // FIX Phase2: prevent HLG from donating flexibleH upward
            var rHLG = rowGO.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            rHLG.padding = new RectOffset(14, 14, 6, 6); rHLG.spacing = 10;
            rHLG.childAlignment = TextAnchor.MiddleLeft;
            rHLG.childControlWidth = false; rHLG.childControlHeight = true;
            rHLG.childForceExpandWidth = false; rHLG.childForceExpandHeight = false; // FIX Phase2

            var lblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            lblGO.transform.SetParent(rowGO.transform, false);
            lblGO.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 56f; // fixed left label column
            var lbl = lblGO.GetComponent<TMPro.TextMeshProUGUI>();
            lbl.text = labelText; lbl.fontSize = 11f;
            lbl.color = new Color(0.55f, 0.57f, 0.62f, 1f); lbl.raycastTarget = false;
            lbl.alignment = TMPro.TextAlignmentOptions.MidlineLeft;

            var fGO = new GameObject("Input", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image));
            fGO.transform.SetParent(rowGO.transform, false);
            var fLE = fGO.AddComponent<UnityEngine.UI.LayoutElement>();
            fLE.flexibleWidth = 1f; fLE.preferredHeight = rowHeight - 12f; // field right, fills remaining width
            fGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.11f, 0.14f, 1f);
            var inf = fGO.AddComponent<TMPro.TMP_InputField>();

            var taGO = new GameObject("TextArea", typeof(RectTransform), typeof(CanvasRenderer));
            taGO.transform.SetParent(fGO.transform, false);
            var taRT = taGO.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(8, 2); taRT.offsetMax = new Vector2(-8, -2);
            taGO.AddComponent<UnityEngine.UI.RectMask2D>();

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            txtGO.transform.SetParent(taGO.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
            var txtTMP = txtGO.GetComponent<TMPro.TextMeshProUGUI>();
            txtTMP.fontSize = 12f; txtTMP.color = new Color(0.88f, 0.88f, 0.90f, 1f); txtTMP.raycastTarget = false;

            var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            phGO.transform.SetParent(taGO.transform, false);
            var phRT = phGO.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
            var phTMP = phGO.GetComponent<TMPro.TextMeshProUGUI>();
            phTMP.text = labelText + "..."; phTMP.fontSize = 12f;
            phTMP.color = new Color(0.40f, 0.42f, 0.48f, 1f); phTMP.raycastTarget = false;
            phTMP.fontStyle = TMPro.FontStyles.Italic;

            inf.textViewport  = taRT; inf.textComponent = txtTMP;
            inf.placeholder   = phTMP;
            return inf;
        }

        var toInf      = MakeFieldRow("To");
        MakeDivider(pnGO.transform);
        var subjectInf = MakeFieldRow("Subject");

        // ── BodySeparator ───────────────────────────────────────────────────
        MakeDivider(pnGO.transform);

        // ── BodyScrollView — long messages never clip ────────────────────
        var bodyInf = MakeBodyScrollView(pnGO.transform);

        // ── ActionBar ───────────────────────────────────────────────────────
        var barGO = new GameObject("ActionBar", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.HorizontalLayoutGroup));
        barGO.transform.SetParent(pnGO.transform, false);
        var barLE = barGO.AddComponent<UnityEngine.UI.LayoutElement>();
        barLE.preferredHeight = 46f;
        barLE.flexibleHeight  = 0f; // FIX Phase2
        barGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.11f, 0.13f, 1f);
        var bHLG = barGO.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        bHLG.padding = new RectOffset(14, 14, 7, 7);
        bHLG.childAlignment = TextAnchor.MiddleRight; bHLG.spacing = 8;
        bHLG.childControlWidth = false; bHLG.childControlHeight = true;
        bHLG.childForceExpandWidth = false; bHLG.childForceExpandHeight = false; // FIX Phase2

        UnityEngine.UI.Button MakeBtn(string label, Color bg, bool primary)
        {
            var bGO = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button),
                typeof(UnityEngine.UI.LayoutElement));
            bGO.transform.SetParent(barGO.transform, false);
            bGO.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 110f;
            var bImg = bGO.GetComponent<UnityEngine.UI.Image>();
            bImg.color = bg;
            var btnComp = bGO.GetComponent<UnityEngine.UI.Button>();
            var btnColors = btnComp.colors;
            btnColors.normalColor      = bg;
            btnColors.highlightedColor = primary
                ? new Color(bg.r * 1.15f, bg.g * 1.15f, bg.b * 1.15f, 1f)
                : new Color(bg.r * 1.25f, bg.g * 1.25f, bg.b * 1.25f, 1f);
            btnColors.pressedColor     = new Color(bg.r * 0.8f, bg.g * 0.8f, bg.b * 0.8f, 1f);
            btnColors.fadeDuration     = 0.08f;
            btnComp.colors = btnColors;

            var lGO = new GameObject("L", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            lGO.transform.SetParent(bGO.transform, false);
            var lRT = lGO.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
            var lTMP = lGO.GetComponent<TMPro.TextMeshProUGUI>();
            lTMP.text = label; lTMP.fontSize = 12f; lTMP.fontStyle = primary ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            lTMP.color = Color.white; lTMP.alignment = TMPro.TextAlignmentOptions.Center; lTMP.raycastTarget = false;
            return bGO.GetComponent<UnityEngine.UI.Button>();
        }

        // Save Draft = primary (blue), Discard = secondary (neutral grey) — order matches
        // ActionBar spec (Discard, Save Draft) while HLG childAlignment=MiddleRight means
        // Save Draft renders rightmost — primary action in the conventional bottom-right slot.
        var cancelBtn = MakeBtn("Discard",    new Color(0.22f, 0.24f, 0.30f, 1f), primary: false);
        var saveBtn   = MakeBtn("Save Draft", new Color(0.20f, 0.40f, 0.80f, 1f), primary: true);

        _compose = mo.AddComponent<ComposeEmailWindow>();
        var cFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cType  = typeof(ComposeEmailWindow);
        cType.GetField("toField",        cFlags)?.SetValue(_compose, toInf);
        cType.GetField("subjectField",   cFlags)?.SetValue(_compose, subjectInf);
        cType.GetField("bodyField",      cFlags)?.SetValue(_compose, bodyInf);
        cType.GetField("saveDraftButton",cFlags)?.SetValue(_compose, saveBtn);
        cType.GetField("cancelButton",   cFlags)?.SetValue(_compose, cancelBtn);
        cType.GetField("closeButton",    cFlags)?.SetValue(_compose, closeBtnComp);
        _compose.Init(SaveDraft);
        mo.SetActive(false); // FIX: guarantee modal is closed after build, independent of Init()
    }

    /// <summary>1px horizontal divider — used between TitleBar/To/Subject/Body sections.</summary>
    private static void MakeDivider(Transform parent)
    {
        var dGO = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        dGO.transform.SetParent(parent, false);
        dGO.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 1f;
        dGO.GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, 0.06f);
    }

    /// <summary>
    /// Body field as a proper ScrollView (Viewport + Content) so long messages
    /// scroll instead of clipping. Returns the TMP_InputField wired to the viewport.
    /// </summary>
    private static TMP_InputField MakeBodyScrollView(Transform parent)
    {
        var svGO = new GameObject("BodyScrollView", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.ScrollRect));
        svGO.transform.SetParent(parent, false);
        var svLE = svGO.AddComponent<UnityEngine.UI.LayoutElement>();
        svLE.flexibleHeight = 1f; svLE.minHeight = 140f; // body grows to fill remaining panel space
        svGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.11f, 0.14f, 1f);

        var vpGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.RectMask2D));
        vpGO.transform.SetParent(svGO.transform, false);
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(8, 8); vpRT.offsetMax = new Vector2(-8, -8);
        vpGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.001f); // raycast target, invisible

        var fGO = new GameObject("Input", typeof(RectTransform), typeof(CanvasRenderer));
        fGO.transform.SetParent(vpGO.transform, false);
        var fRT = fGO.GetComponent<RectTransform>();
        // Top-anchored, NOT vertically stretched — ContentSizeFitter below grows this
        // taller than the Viewport as text overflows, which is what makes scrolling possible.
        fRT.anchorMin = new Vector2(0f, 1f); fRT.anchorMax = new Vector2(1f, 1f);
        fRT.pivot     = new Vector2(0.5f, 1f);
        fRT.anchoredPosition = Vector2.zero;
        fRT.sizeDelta = new Vector2(0f, 0f);
        var fCSF = fGO.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fCSF.verticalFit   = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        fCSF.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
        var inf = fGO.AddComponent<TMPro.TMP_InputField>();
        inf.lineType = TMPro.TMP_InputField.LineType.MultiLineNewline;

        var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        txtGO.transform.SetParent(fGO.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        var txtTMP = txtGO.GetComponent<TMPro.TextMeshProUGUI>();
        txtTMP.fontSize = 12f; txtTMP.color = new Color(0.88f, 0.88f, 0.90f, 1f); txtTMP.raycastTarget = false;

        var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        phGO.transform.SetParent(fGO.transform, false);
        var phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        var phTMP = phGO.GetComponent<TMPro.TextMeshProUGUI>();
        phTMP.text = "Write your message..."; phTMP.fontSize = 12f;
        phTMP.color = new Color(0.40f, 0.42f, 0.48f, 1f); phTMP.raycastTarget = false;
        phTMP.fontStyle = TMPro.FontStyles.Italic;

        inf.textViewport = vpRT; inf.textComponent = txtTMP; inf.placeholder = phTMP;

        var sr = svGO.GetComponent<UnityEngine.UI.ScrollRect>();
        sr.viewport = vpRT; sr.content = fRT;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        return inf;
    }
}
