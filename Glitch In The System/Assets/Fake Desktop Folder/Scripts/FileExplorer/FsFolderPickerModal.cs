using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class FsFolderPickerModal : MonoBehaviour
{
    private Action<string> _onConfirm;
    private Action         _onCancel;
    private string         _sourcePath;
    private string         _selected;
    private RectTransform  _panel;
    private RectTransform  _listContent;
    private TextMeshProUGUI _titleLabel;
    private Button          _confirmBtn;
    private Button          _cancelBtn;
    private readonly List<GameObject> _rowPool = new List<GameObject>(16);

    public void Init()   { BuildModal(); gameObject.SetActive(false); }
    public void Show(string sourcePath, Action<string> onConfirm, Action onCancel)
    {
        _sourcePath = sourcePath; _onConfirm = onConfirm; _onCancel = onCancel; _selected = null;
        UpdateConfirmButton(); PopulateFolderList();
        gameObject.SetActive(true); transform.SetAsLastSibling();
    }
    public void Hide() { gameObject.SetActive(false); }

    private void BuildModal()
    {
        // backdrop
        var bdGO = new GameObject("FsPicker_Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bdGO.transform.SetParent(transform, false);
        var bdRT = bdGO.GetComponent<RectTransform>();
        bdRT.anchorMin = Vector2.zero; bdRT.anchorMax = Vector2.one;
        bdRT.offsetMin = Vector2.zero; bdRT.offsetMax = Vector2.zero;
        bdGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // panel
        var panel = new GameObject("FsPicker_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        _panel = panel.GetComponent<RectTransform>();
        _panel.anchorMin = new Vector2(0.5f,0.5f); _panel.anchorMax = new Vector2(0.5f,0.5f);
        _panel.pivot     = new Vector2(0.5f,0.5f); _panel.sizeDelta = new Vector2(320f,380f);
        _panel.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.13f,0.12f,0.11f,0.98f);

        // title
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(_panel, false);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f,1f); titleRT.anchorMax = new Vector2(1f,1f);
        titleRT.pivot = new Vector2(0.5f,1f); titleRT.sizeDelta = new Vector2(0f,36f);
        titleRT.anchoredPosition = Vector2.zero;
        _titleLabel = titleGO.GetComponent<TextMeshProUGUI>();
        _titleLabel.text = "Move to..."; _titleLabel.fontSize = 13f;
        _titleLabel.color = new Color(0.9f,0.88f,0.84f,1f);
        _titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        _titleLabel.raycastTarget = false;

        // scroll
        var scrollGO = new GameObject("Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        scrollGO.transform.SetParent(_panel, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f,0f); scrollRT.anchorMax = new Vector2(1f,1f);
        scrollRT.offsetMin = new Vector2(0f,44f); scrollRT.offsetMax = new Vector2(0f,-36f);
        scrollGO.GetComponent<Image>().color = new Color(0.08f,0.08f,0.07f,1f);

        var vpGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        vpGO.transform.SetParent(scrollGO.transform, false);
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        vpGO.GetComponent<Image>().color = Color.clear;

        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(CanvasRenderer), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(vpGO.transform, false);
        _listContent = contentGO.GetComponent<RectTransform>();
        _listContent.anchorMin = new Vector2(0f,1f); _listContent.anchorMax = new Vector2(1f,1f);
        _listContent.pivot = new Vector2(0.5f,1f); _listContent.sizeDelta = Vector2.zero;
        var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4,4,4,4); vlg.spacing = 1;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGO.GetComponent<ScrollRect>();
        sr.viewport = vpRT; sr.content = _listContent;
        sr.horizontal = false; sr.vertical = true;
        sr.scrollSensitivity = 100f; sr.decelerationRate = 0.06f;

        // button bar
        var barGO = new GameObject("ButtonBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorizontalLayoutGroup));
        barGO.transform.SetParent(_panel, false);
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f,0f); barRT.anchorMax = new Vector2(1f,0f);
        barRT.pivot = new Vector2(0.5f,0f); barRT.sizeDelta = new Vector2(0f,40f);
        barRT.anchoredPosition = Vector2.zero;
        var barHLG = barGO.GetComponent<HorizontalLayoutGroup>();
        barHLG.padding = new RectOffset(12,12,6,6); barHLG.spacing = 8;
        barHLG.childAlignment = TextAnchor.MiddleRight;
        barHLG.childControlWidth = true; barHLG.childControlHeight = true;
        barHLG.childForceExpandWidth = false; barHLG.childForceExpandHeight = true;

        _confirmBtn = BuildBarButton(barGO.transform, "Move",   new Color(0.25f,0.50f,0.90f,1f));
        _cancelBtn  = BuildBarButton(barGO.transform, "Cancel", new Color(0.22f,0.21f,0.20f,1f));
        _confirmBtn.onClick.AddListener(OnConfirmClicked);
        _cancelBtn.onClick.AddListener(OnCancelClicked);
    }

    private Button BuildBarButton(Transform parent, string label, Color bg)
    {
        var go = new GameObject("Btn_"+label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredWidth = 90f;
        go.GetComponent<Image>().color = bg;
        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        var hc = bg * 1.15f; hc.a = 1f; colors.highlightedColor = hc;
        var pc = bg * 0.80f; pc.a = 1f; colors.pressedColor     = pc;
        btn.colors = colors;
        var lblGO = new GameObject("L", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var rt = lblGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 12f;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    private void PopulateFolderList()
    {
        var fs = FileExplorerManager.Instance;
        if (fs == null) return;
        foreach (var r in _rowPool) r.SetActive(false);
        int idx = 0;
        foreach (var sidebarRoot in FileExplorerManager.SidebarRoots)
        {
            string rootPath = "/" + sidebarRoot;
            AddRow(ref idx, rootPath, sidebarRoot, 0, fs);
            foreach (var child in fs.GetChildren(rootPath))
            {
                if (child.type != FileExplorerManager.EntryType.Folder) continue;
                AddRow(ref idx, child.fullPath, child.name, 1, fs);
            }
        }
    }

    private void AddRow(ref int idx, string path, string displayName, int indent, FileExplorerManager fs)
    {
        if (!string.IsNullOrEmpty(_sourcePath) &&
            (path == _sourcePath || path.StartsWith(_sourcePath + "/", System.StringComparison.Ordinal)))
            return;
        GameObject row;
        if (idx < _rowPool.Count) { row = _rowPool[idx]; row.SetActive(true); }
        else { row = BuildRow(); _rowPool.Add(row); }
        idx++;
        var tmp = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            string indent2 = new string(' ', indent * 4);
            tmp.text   = indent2 + "[+] " + displayName;
            tmp.margin = new Vector4(indent * 8f, 0f, 0f, 0f);
        }
        var btn = row.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        var capturedPath = path;
        btn.onClick.AddListener(() => SelectFolder(capturedPath, row));
        var img = row.GetComponent<Image>();
        if (img != null) img.color = new Color(1f,1f,1f,0f);
    }

    private GameObject BuildRow()
    {
        var go = new GameObject("FsPicker_Row", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(_listContent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 28f;
        go.GetComponent<Image>().color = new Color(1f,1f,1f,0f);
        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f,1f,1f,0f);
        colors.highlightedColor = new Color(1f,1f,1f,0.08f);
        colors.pressedColor     = new Color(1f,1f,1f,0.15f);
        btn.colors = colors;
        var lblGO = new GameObject("L", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var rt = lblGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f,0f); rt.offsetMax = new Vector2(-4f,0f);
        var tmp = lblGO.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = 12f; tmp.color = new Color(0.88f,0.86f,0.82f,1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false; tmp.overflowMode = TextOverflowModes.Ellipsis;
        return go;
    }

    private void SelectFolder(string path, GameObject row)
    {
        foreach (var r in _rowPool)
        { var img = r.GetComponent<Image>(); if (img != null) img.color = new Color(1f,1f,1f,0f); }
        var selImg = row.GetComponent<Image>();
        if (selImg != null) selImg.color = new Color(0.30f,0.55f,0.90f,0.30f);
        _selected = path;
        UpdateConfirmButton();
    }

    private void UpdateConfirmButton()
    { if (_confirmBtn != null) _confirmBtn.interactable = !string.IsNullOrEmpty(_selected); }

    private void OnConfirmClicked()
    { if (string.IsNullOrEmpty(_selected)) return; Hide(); _onConfirm?.Invoke(_selected); }

    private void OnCancelClicked()
    { Hide(); _onCancel?.Invoke(); }
}
