using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages note list. Attach to StickyNotesAppWindow.
///
/// Inspector-wire:
///   notePrefab     — fallback prefab (used if sceneTemplate is null)
///   sceneTemplate  — scene-view-editable NoteTemplate object (preferred source)
///   noteContainer  — Content RectTransform (scroll view content)
///   searchField    — TopBar search InputField
///
/// TEMPLATE PRIORITY: sceneTemplate > notePrefab.
/// Designers edit NoteTemplate in Scene View; all spawned notes inherit that design.
/// NoteTemplate is deactivated at runtime — only its clone is used.
/// </summary>
public sealed class StickyNotesApp : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StickyNote     notePrefab;
    [SerializeField] private RectTransform  noteContainer;
    [SerializeField] private TMP_InputField searchField;

    [Header("Scene Template (optional — overrides notePrefab when set)")]
    [Tooltip("Drag NoteTemplate scene object here. Disabled at runtime; cloned for each note.")]
    [SerializeField] private RectTransform sceneTemplate;

    // Baked runtime clone of sceneTemplate — created once in Awake, reused for all Instantiate calls.
    private GameObject _runtimeTemplatePrefab;

    private readonly List<StickyNote> _notes = new List<StickyNote>();
    private const string SaveKey = "StickyNotes_v1";
    private Coroutine _saveRoutine;

    // ── Lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        BakeSceneTemplate();
    }

    private void OnEnable()
    {
        // Guard: remove before add to prevent duplicate listeners on repeated open/close cycles.
        if (searchField != null)
        {
            searchField.onValueChanged.RemoveListener(OnSearchChanged);
            searchField.onValueChanged.AddListener(OnSearchChanged);
        }

        LoadNotes();
    }

    private void OnDisable()
    {
        searchField?.onValueChanged.RemoveListener(OnSearchChanged);
        SaveNow();
    }

    // ── Template baking ────────────────────────────────────────────────────
    /// <summary>
    /// Converts the scene-placed NoteTemplate into a runtime-usable source clone.
    /// Called once in Awake — before any note spawning in OnEnable.
    ///
    /// Template lifecycle:
    ///   sceneTemplate          — Designer edits this in Scene View; deactivated at runtime.
    ///   _runtimeTemplatePrefab — Inactive clone parented to this GO; never displayed.
    ///   SpawnNote()            — Instantiates _runtimeTemplatePrefab into Content.
    ///
    /// We keep _runtimeTemplatePrefab as a separate clone so sceneTemplate is never
    /// touched again after baking. Designer changes are captured once at play-start,
    /// and runtime is fully decoupled from the scene object thereafter.
    /// </summary>
    private void BakeSceneTemplate()
    {
        if (sceneTemplate == null)
        {
            if (notePrefab == null)
                Debug.LogError("[StickyNotesApp] Both sceneTemplate and notePrefab are null. " +
                               "Assign at least one in the Inspector. Notes cannot spawn.", this);
            else
                Debug.LogWarning("[StickyNotesApp] sceneTemplate is null — falling back to notePrefab. " +
                                 "Assign NoteTemplate in the Inspector for scene-driven design.", this);
            return;
        }

        // Belt-and-suspenders: deactivate template and lock its CanvasGroup so it
        // is never visible or interactive, even in the one frame before layout settles.
        sceneTemplate.gameObject.SetActive(false);
        var cg = sceneTemplate.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.interactable   = false;
            cg.blocksRaycasts = false;
        }

        // Clone once into a hidden, inert runtime source object.
        _runtimeTemplatePrefab = Instantiate(sceneTemplate.gameObject, transform);
        _runtimeTemplatePrefab.SetActive(false);
        _runtimeTemplatePrefab.name = "NoteTemplate_RuntimeClone";

        // Exclude the clone from any layout pass — it lives off-screen as a source only.
        var le = _runtimeTemplatePrefab.GetComponent<LayoutElement>();
        if (le == null) le = _runtimeTemplatePrefab.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // BUG FIX: The runtime template clone inherited interactable=false/blocksRaycasts=false
        // from the scene template's CanvasGroup. Reset it here so every spawned note starts
        // fully interactive. SpawnNote() will re-enable these on the live instance anyway,
        // but resetting on the source prevents the inherited-false values from ever reaching
        // a live note even if SpawnNote's reset is skipped in an edge case.
        var templateCg = _runtimeTemplatePrefab.GetComponent<CanvasGroup>();
        if (templateCg != null)
        {
            templateCg.interactable   = true;
            templateCg.blocksRaycasts = true;
            templateCg.alpha          = 1f;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────
    public void AddNote()
    {
        // Re-validate: if bake silently failed but a template exists, retry once.
        if (_runtimeTemplatePrefab == null && sceneTemplate != null)
        {
            Debug.LogWarning("[StickyNotesApp] AddNote: _runtimeTemplatePrefab missing. " +
                             "Re-running BakeSceneTemplate.", this);
            BakeSceneTemplate();
        }

        var note = SpawnNote();
        if (note == null)
        {
            Debug.LogError("[StickyNotesApp] AddNote failed — no template or prefab available. " +
                           "Assign sceneTemplate or notePrefab in the Inspector.", this);
            return;
        }
        note.Init(this);
        _notes.Add(note);
        ScheduleSave();

        // BUG FIX: Force EventSystem focus onto the new note's InputField so the user
        // can start typing immediately after clicking Add Note, without needing to click again.
        var newInputField = note.GetInputField();
        if (newInputField != null && UnityEngine.EventSystems.EventSystem.current != null)
        {
            newInputField.Select();
            newInputField.ActivateInputField();
        }
    }

    public void RemoveNote(StickyNote note)
    {
        if (note == null) return;
        _notes.Remove(note);
        Destroy(note.gameObject);
        if (_notes.Count == 0) AddNote();
        ScheduleSave();
    }

    /// <summary>Called by StickyNote on any change. Debounced — saves 1 s after last change.</summary>
    public void ScheduleSave()
    {
        if (_saveRoutine != null) StopCoroutine(_saveRoutine);
        _saveRoutine = StartCoroutine(SaveAfterDelay());
    }

    // ── Persistence ────────────────────────────────────────────────────────
    private IEnumerator SaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        SaveNow();
        _saveRoutine = null;
    }

    private void SaveNow()
    {
        var wrapper = new NoteListWrapper();
        foreach (var n in _notes)
            if (n != null) wrapper.notes.Add(n.ExportData());
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    private void LoadNotes()
    {
        // Destroy only managed notes — never touch sceneTemplate or _runtimeTemplatePrefab.
        for (int i = _notes.Count - 1; i >= 0; i--)
            if (_notes[i] != null) Destroy(_notes[i].gameObject);
        _notes.Clear();

        string json = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            var wrapper = JsonUtility.FromJson<NoteListWrapper>(json);
            if (wrapper?.notes != null && wrapper.notes.Count > 0)
            {
                foreach (var d in wrapper.notes)
                {
                    var note = SpawnNote();
                    if (note == null) continue;
                    note.Init(this, d);
                    _notes.Add(note);
                }
                return;
            }
        }

        // No saved data — seed with one blank note.
        AddNote();
    }

    // ── Internal spawn ─────────────────────────────────────────────────────
    /// <summary>
    /// Spawns a note from the best available source:
    ///   1. _runtimeTemplatePrefab (baked scene template) — preferred.
    ///   2. notePrefab (legacy fallback).
    /// Returns null if neither is available.
    /// </summary>
    private StickyNote SpawnNote()
    {
        if (noteContainer == null)
        {
            Debug.LogError("[StickyNotesApp] noteContainer is null — cannot spawn note. " +
                           "Wire the Content RectTransform in the Inspector.", this);
            return null;
        }

        GameObject source = _runtimeTemplatePrefab != null
            ? _runtimeTemplatePrefab
            : (notePrefab != null ? notePrefab.gameObject : null);

        if (source == null)
        {
            Debug.LogWarning("[StickyNotesApp] SpawnNote: no source available.", this);
            return null;
        }

        var go = Instantiate(source, noteContainer, false);
        go.SetActive(true);
        go.name = "StickyNote";

        // ── Reset transform: do NOT inherit template position offsets ──────
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale       = Vector3.one;
            rt.anchoredPosition = Vector2.zero;
            // Stretch horizontally inside Content so the VLG can stack notes cleanly.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
        }

        // Remove the ignoreLayout flag that was set on the runtime clone source.
        var le = go.GetComponent<LayoutElement>();
        if (le != null) le.ignoreLayout = false;

        // BUG FIX (Critical): Reset CanvasGroup on spawned note so it is fully interactive.
        // The runtime template clone was baked from sceneTemplate which had interactable=false
        // and blocksRaycasts=false set on it. Without this reset, all TMP_InputFields inside
        // spawned notes are blocked from receiving input — clicking does nothing, typing fails.
        var spawnedCg = go.GetComponent<CanvasGroup>();
        if (spawnedCg != null)
        {
            spawnedCg.alpha          = 1f;
            spawnedCg.interactable   = true;
            spawnedCg.blocksRaycasts = true;
        }

        var note = go.GetComponent<StickyNote>();
        if (note == null)
        {
            Debug.LogError("[StickyNotesApp] Spawned object has no StickyNote component. " +
                           "Check that NoteTemplate has StickyNote attached at its root.", this);
            Destroy(go);
            return null;
        }

        return note;
    }

    // ── Search ─────────────────────────────────────────────────────────────
    private void OnSearchChanged(string query)
    {
        query = query.Trim().ToLowerInvariant();
        foreach (var note in _notes)
        {
            if (note == null) continue;
            var data = note.ExportData();
            bool show = string.IsNullOrEmpty(query) ||
                        data.text.ToLowerInvariant().Contains(query);
            note.gameObject.SetActive(show);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    [System.Serializable]
    private class NoteListWrapper
    {
        public List<StickyNote.NoteData> notes = new List<StickyNote.NoteData>();
    }
}
