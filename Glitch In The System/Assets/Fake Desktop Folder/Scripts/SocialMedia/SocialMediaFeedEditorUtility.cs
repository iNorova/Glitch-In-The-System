#if UNITY_EDITOR
using GlitchInTheSystem.GameData;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Builds the single scene design template (EditorFeedPost_Template) for edit mode.
    /// Play mode clones it for each live feed post.
    /// </summary>
    public static class SocialMediaFeedEditorUtility
    {
        private static bool _busy;
        private static TMP_FontAsset _sceneFont;

        public static void EnsurePreviewPosts(SocialMediaFeedController controller, bool rebuildAll = false, bool selectFirst = true)
            => EnsureDesignTemplatePost(controller, rebuildAll, selectFirst);

        public static void EnsureDesignTemplatePost(SocialMediaFeedController controller, bool rebuildAll = false, bool selectTemplate = true)
        {
            if (controller == null || Application.isPlaying || _busy) return;

            var content = GetSceneFeedContent(controller.transform);
            if (content == null)
            {
                Debug.LogWarning(
                    "Social feed: could not find FeedScroll/Content under SocialMediaAppWindow. " +
                    "Expected path: FloatingPanel/Body/FeedScroll/Viewport/Content",
                    controller);
                return;
            }

            var templateMarker = content.GetComponentInChildren<SocialMediaFeedPostTemplate>(true);
            var templateTransform = templateMarker != null
                ? templateMarker.transform
                : content.Find(SocialMediaFeedPostTemplate.TemplateObjectName);

            if (templateTransform == null)
                templateTransform = TryUpgradeLegacyEditorPostToTemplate(content);

            if (!rebuildAll && templateTransform != null)
            {
                RemoveExtraEditorPosts(content, templateTransform);
                AssignTemplateReference(controller, templateTransform as RectTransform);

                var card = templateTransform as RectTransform;
                if (NeedsDesignTemplateLayoutRepair(card))
                {
                    EnsureDesignTemplatePost(controller, rebuildAll: true, selectTemplate);
                    return;
                }

                ApplyEditModeTemplatePresentation(controller, refreshLayout: false);
                return;
            }

            _busy = true;
            try
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Create social feed design template");

                RemoveAllEditorAndTemplatePosts(content);

                var sample = SocialMediaFeedPreviewData.GetDesignTemplateSamplePost();
                var card = BuildPreviewPostCard(content, SocialMediaFeedPostTemplate.TemplateObjectName, sample, isDesignTemplate: true);

                SyncTemplatePreviewText(controller, sample, rebuildLayout: true);
                AssignTemplateReference(controller, card);
                Undo.CollapseUndoOperations(group);

                if (controller.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);

                if (selectTemplate && card != null)
                {
                    Selection.activeGameObject = card.gameObject;
                    EditorGUIUtility.PingObject(card.gameObject);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        private static Transform TryUpgradeLegacyEditorPostToTemplate(RectTransform content)
        {
            Transform legacy = content.Find("EditorFeedPost_0");
            if (legacy == null)
            {
                var posts = content.GetComponentsInChildren<SocialMediaFeedEditorPost>(true);
                if (posts.Length > 0)
                    legacy = posts[0].transform;
            }

            if (legacy == null) return null;

            Undo.RecordObject(legacy.gameObject, "Upgrade to design template");
            legacy.name = SocialMediaFeedPostTemplate.TemplateObjectName;
            if (legacy.GetComponent<SocialMediaFeedPostTemplate>() == null)
                Undo.AddComponent<SocialMediaFeedPostTemplate>(legacy.gameObject);
            if (legacy.GetComponent<SocialMediaFeedEditorPost>() == null)
                Undo.AddComponent<SocialMediaFeedEditorPost>(legacy.gameObject);

            return legacy;
        }

        private static void RemoveExtraEditorPosts(RectTransform content, Transform keep)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                if (child == null || child == keep) continue;
                if (child.GetComponent<SocialMediaFeedPostTemplate>() != null
                    || child.GetComponent<SocialMediaFeedEditorPost>() != null
                    || child.name.StartsWith("EditorFeedPost_", System.StringComparison.Ordinal))
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void RemoveAllEditorAndTemplatePosts(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                if (child == null) continue;
                if (child.GetComponent<SocialMediaFeedPostTemplate>() != null
                    || child.GetComponent<SocialMediaFeedEditorPost>() != null
                    || child.name.StartsWith("EditorFeedPost_", System.StringComparison.Ordinal))
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void AssignTemplateReference(SocialMediaFeedController controller, RectTransform card)
        {
            if (controller == null || card == null) return;
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("postDesignTemplate");
            if (prop != null)
            {
                prop.objectReferenceValue = card;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void SyncTemplatePreviewText(SocialMediaFeedController controller, PostData sample = null, bool rebuildLayout = false)
        {
            if (controller == null || Application.isPlaying) return;
            var template = controller.GetPostDesignTemplate();
            if (template == null) return;

            sample ??= SocialMediaFeedPreviewData.GetDesignTemplateSamplePost();
            var user = SocialMediaFeedPreviewData.CreatePreviewUser();
            SocialMediaFeedCardBinder.Apply(template, sample, user, expandComments: true);

            if (rebuildLayout)
            {
                FinalizeCardLayout(template);
                SocialMediaFeedLayoutConstraints.ApplyToDesignTemplate(template);
                var content = GetSceneFeedContent(controller.transform);
                if (content != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
            else
                ForcePostVisible(template);

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Keeps the template readable in edit mode without resetting your RectTransforms.</summary>
        public static void ApplyEditModeTemplatePresentation(SocialMediaFeedController controller, bool refreshLayout = false)
        {
            if (controller == null || Application.isPlaying) return;
            var template = controller.GetPostDesignTemplate();
            if (template == null) return;

            if (refreshLayout || NeedsDesignTemplateLayoutRepair(template))
                SyncTemplatePreviewText(controller, rebuildLayout: true);
            else
            {
                ForcePostVisible(template);
                EnsureCommentsPanelExpanded(template);
                SocialMediaFeedLayoutConstraints.ApplyToDesignTemplate(template);
            }

            var content = GetSceneFeedContent(controller.transform);
            if (content != null)
            {
                ApplyEditModeFeedContentPadding(content);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static bool NeedsDesignTemplateLayoutRepair(RectTransform card)
        {
            if (card == null) return true;
            if (card.rect.height < 120f) return true;
            if (card.GetComponent<VerticalLayoutGroup>() == null) return true;
            if (card.Find("CommentsPanel") == null) return true;
            if (card.Find("BodyText") == null) return true;
            return false;
        }

        private static void EnsureCommentsPanelExpanded(RectTransform card)
        {
            var panel = card.Find("CommentsPanel");
            if (panel == null)
                panel = card.Find("CommentsSection/CommentsPanel");
            if (panel == null) return;

            panel.gameObject.SetActive(true);
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.enabled = true;
                tmp.gameObject.SetActive(true);
            }
        }

        private static void ApplyEditModeFeedContentPadding(RectTransform content)
        {
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) return;
            layout.padding = new RectOffset(8, 8, 10, 16);
            layout.spacing = 10;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
        }

        public static void SyncAllPreviewText(SocialMediaFeedController controller, System.Collections.Generic.List<PostData> samples = null)
        {
            if (controller == null || Application.isPlaying) return;
            samples ??= SocialMediaFeedPreviewData.CreatePlayFillerPosts();
            var sample = samples.Count > 0 ? samples[0] : null;
            SyncTemplatePreviewText(controller, sample);
        }

        public static void EnsurePostsVisibleOnly(SocialMediaFeedController controller)
        {
            if (controller == null || Application.isPlaying) return;
            var content = GetSceneFeedContent(controller.transform);
            if (content == null) return;

            var template = controller.GetPostDesignTemplate();
            if (template != null)
                ForcePostVisible(template);

            Canvas.ForceUpdateCanvases();
        }

        public static void FinalizeAllCardLayouts(SocialMediaFeedController controller)
        {
            if (controller == null || Application.isPlaying) return;
            if (controller.IsEditModeFreeformLayout)
            {
                EnsurePostsVisibleOnly(controller);
                return;
            }

            var content = GetSceneFeedContent(controller.transform);
            if (content == null) return;

            var template = controller.GetPostDesignTemplate();
            if (template != null)
            {
                FinalizeCardLayout(template);
                ForcePostVisible(template);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
        }

        public static void ForcePostVisible(RectTransform card)
        {
            if (card == null) return;

            card.gameObject.SetActive(true);
            card.localScale = Vector3.one;

            var group = card.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = true;
                var c = graphic.color;
                graphic.color = new Color(c.r, c.g, c.b, 1f);
            }

            foreach (var tmp in card.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.enabled = true;
                tmp.gameObject.SetActive(true);
                ApplySceneFont(tmp);
                if (string.IsNullOrWhiteSpace(tmp.text) && tmp.name == "BodyText")
                    tmp.text = "Edit this post body in the Inspector.";
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f);
                tmp.ForceMeshUpdate(true);
            }

            var body = card.Find("BodyText") as RectTransform;
            if (body != null)
            {
                body.gameObject.SetActive(true);
                var bodyLE = body.GetComponent<LayoutElement>() ?? body.gameObject.AddComponent<LayoutElement>();
                bodyLE.minHeight = 80f;
                bodyLE.preferredHeight = 96f;
                bodyLE.flexibleWidth = 1f;
            }

            var commentsPanel = card.Find("CommentsPanel");
            if (commentsPanel != null)
            {
                commentsPanel.gameObject.SetActive(true);
                foreach (var t in commentsPanel.GetComponentsInChildren<Transform>(true))
                    t.gameObject.SetActive(true);
            }

            var engagementLabel = card.Find("EngagementLabel");
            if (engagementLabel != null)
                engagementLabel.gameObject.SetActive(true);

            var stateText = card.Find("StateText");
            if (stateText != null && string.IsNullOrWhiteSpace(stateText.GetComponent<TMP_Text>()?.text))
                stateText.gameObject.SetActive(false);
        }

        public static void FinalizeCardLayout(RectTransform card)
        {
            if (card == null) return;
            if (card.GetComponentInParent<SocialMediaFeedFreeformLayout>() != null)
            {
                ForcePostVisible(card);
                return;
            }

            card.gameObject.SetActive(true);
            card.localScale = Vector3.one;
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(0f, 0f);

            var cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.enabled = true;
                cardImage.color = new Color(0.14f, 0.15f, 0.18f, 0.96f);
            }

            var cardLayout = card.GetComponent<VerticalLayoutGroup>();
            if (cardLayout == null)
            {
                cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
                cardLayout.padding = new RectOffset(14, 14, 12, 12);
                cardLayout.spacing = 8;
            }
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            var cardFitter = card.GetComponent<ContentSizeFitter>();
            if (cardFitter == null)
                cardFitter = card.gameObject.AddComponent<ContentSizeFitter>();
            cardFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cardLE = card.GetComponent<LayoutElement>();
            if (cardLE == null)
                cardLE = card.gameObject.AddComponent<LayoutElement>();
            cardLE.minHeight = 280f;
            cardLE.preferredHeight = 280f;
            cardLE.flexibleWidth = 1f;

            var commentsPanel = card.Find("CommentsPanel") as RectTransform;
            if (commentsPanel != null)
            {
                commentsPanel.gameObject.SetActive(true);
                var panelImage = commentsPanel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.enabled = true;
                    panelImage.color = new Color(0.10f, 0.11f, 0.14f, 1f);
                }

                var panelLayout = commentsPanel.GetComponent<VerticalLayoutGroup>();
                if (panelLayout == null)
                {
                    panelLayout = commentsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
                    panelLayout.padding = new RectOffset(8, 8, 6, 6);
                    panelLayout.spacing = 4;
                }
                panelLayout.childControlWidth = true;
                panelLayout.childControlHeight = false;
                panelLayout.childForceExpandHeight = false;

                SocialMediaFeedLayoutConstraints.ApplyFixedPanelLayout(commentsPanel);
            }

            SocialMediaFeedLayoutConstraints.ConstrainDecorImagesUnder(card);

            foreach (var tmp in card.GetComponentsInChildren<TMP_Text>(true))
            {
                tmp.enabled = true;
                var c = tmp.color;
                tmp.color = new Color(c.r, c.g, c.b, 1f);
                tmp.ForceMeshUpdate(true);

                if (tmp.name == "BodyText")
                {
                    var bodyLE = tmp.GetComponent<LayoutElement>();
                    if (bodyLE == null)
                        bodyLE = tmp.gameObject.AddComponent<LayoutElement>();
                    bodyLE.minHeight = 72f;
                    bodyLE.preferredHeight = 88f;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(card);

            if (card.rect.height < 80f)
                card.sizeDelta = new Vector2(0f, 300f);

            ForcePostVisible(card);
        }

        private static void ApplySceneFont(TMP_Text tmp)
        {
            if (tmp == null) return;
            if (_sceneFont == null)
            {
                var any = Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
                if (any != null)
                    _sceneFont = any.font;
            }
            if (_sceneFont != null)
                tmp.font = _sceneFont;
        }

        public static RectTransform GetSceneFeedContent(Transform controllerRoot)
        {
            if (controllerRoot == null) return null;
            var scroll = controllerRoot.Find("FloatingPanel/Body/FeedScroll")?.GetComponent<ScrollRect>();
            if (scroll?.content != null) return scroll.content;
            foreach (var sr in controllerRoot.GetComponentsInChildren<ScrollRect>(true))
            {
                if (sr.name == "FeedScroll" && sr.content != null)
                    return sr.content;
            }
            return null;
        }

        public static void AddPreviewPost(SocialMediaFeedController controller, PostData sample = null)
            => EnsureDesignTemplatePost(controller, rebuildAll: false, selectTemplate: true);

        public static void CreatePreviewCardOnContent(RectTransform content, string name, PostData sample)
            => BuildPreviewPostCard(content, name, sample, isDesignTemplate: name == SocialMediaFeedPostTemplate.TemplateObjectName);

        private static RectTransform BuildPreviewPostCard(RectTransform content, string name, PostData sample, bool isDesignTemplate = false)
        {
            var card = CreatePanel(name, content, new Color(0.14f, 0.15f, 0.18f, 0.96f));
            if (isDesignTemplate)
            {
                card.gameObject.AddComponent<SocialMediaFeedPostTemplate>();
                card.gameObject.AddComponent<SocialMediaFeedEditorPost>();
            }
            else
                card.gameObject.AddComponent<SocialMediaFeedEditorPost>();

            var cardLayout = Undo.AddComponent<VerticalLayoutGroup>(card.gameObject);
            cardLayout.padding = new RectOffset(14, 14, 12, 12);
            cardLayout.spacing = 8;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            var cardFitter = Undo.AddComponent<ContentSizeFitter>(card.gameObject);
            cardFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cardLE = Undo.AddComponent<LayoutElement>(card.gameObject);
            cardLE.minHeight = isDesignTemplate ? 280f : 120f;
            if (isDesignTemplate)
                cardLE.preferredHeight = 280f;

            var topRow = CreateRow("TopRow", card, true);
            SetPreferredHeight(topRow, 28f);
            CreateTMP("AuthorText", topRow, "Author", 17, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.96f, 0.98f, 1f));
            var tag = CreateTMP("CategoryTag", topRow, "Category", 12, TextAlignmentOptions.MidlineRight, Color.white);
            var tagLE = Undo.AddComponent<LayoutElement>(tag.gameObject);
            tagLE.preferredWidth = 140f;

            string bodyText = sample != null
                ? SocialMediaFeedPresentation.SanitizeForTMP(sample.text)
                : "Post body — edit this text in the Inspector.";
            var body = CreateTMP("BodyText", card, bodyText, 18, TextAlignmentOptions.TopLeft, Color.white);
            body.textWrappingMode = TextWrappingModes.Normal;
            var bodyLE = Undo.AddComponent<LayoutElement>(body.gameObject);
            bodyLE.minHeight = 72f;
            bodyLE.preferredHeight = 88f;

            var metaRow = CreateRow("MetaRow", card, true);
            SetPreferredHeight(metaRow, 22f);
            CreateTMP("EngagementText", metaRow, "Likes 0", 14, TextAlignmentOptions.MidlineLeft, new Color(0.76f, 0.83f, 0.97f, 1f));
            CreateTMP("TimeText", metaRow, "0m", 14, TextAlignmentOptions.MidlineRight, new Color(0.72f, 0.72f, 0.76f, 1f));

            CreateTMP("EngagementLabel", card, "TRENDING", 12, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.62f, 0.28f, 1f));
            CreateTMP("StateText", card, "", 13, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.76f, 0.35f, 1f));

            int commentCount = sample?.commentPreview?.Count ?? 0;
            if (commentCount > 0 || isDesignTemplate)
            {
                int displayCount = isDesignTemplate ? Mathf.Max(commentCount, 3) : commentCount;
                CreateActionButton(card, $"Comments ({displayCount})");

                var commentsPanel = CreatePanel("CommentsPanel", card, new Color(0.10f, 0.11f, 0.14f, 1f));
                var commentsPanelLayout = Undo.AddComponent<VerticalLayoutGroup>(commentsPanel.gameObject);
                commentsPanelLayout.padding = new RectOffset(8, 8, 6, 6);
                commentsPanelLayout.spacing = 4;
                commentsPanelLayout.childControlWidth = true;
                commentsPanelLayout.childControlHeight = true;
                commentsPanel.gameObject.SetActive(true);

                var panelLE = Undo.AddComponent<LayoutElement>(commentsPanel.gameObject);
                panelLE.minHeight = SocialMediaFeedLayoutConstraints.DefaultCommentsPanelHeight;
                panelLE.preferredHeight = SocialMediaFeedLayoutConstraints.DefaultCommentsPanelHeight;
                panelLE.flexibleHeight = 0f;
                commentsPanelLayout.childControlHeight = false;
                commentsPanelLayout.childForceExpandHeight = false;
                Undo.AddComponent<RectMask2D>(commentsPanel.gameObject);

                CreateTMP("CommentHeader", commentsPanel, "Top comments", 13, TextAlignmentOptions.MidlineLeft, new Color(0.70f, 0.76f, 0.88f, 1f));
                for (int i = 0; i < 3; i++)
                {
                    var line = CreateTMP($"Comment_{i}", commentsPanel, "@user: comment", 13, TextAlignmentOptions.TopLeft, new Color(0.88f, 0.90f, 0.95f, 1f));
                    line.textWrappingMode = TextWrappingModes.Normal;
                    var lineLE = Undo.AddComponent<LayoutElement>(line.gameObject);
                    lineLE.minHeight = 22f;
                    lineLE.preferredHeight = 26f;
                }
            }

            var previewUser = isDesignTemplate ? SocialMediaFeedPreviewData.CreatePreviewUser() : null;
            SocialMediaFeedCardBinder.Apply(card, sample, previewUser, expandComments: true);
            if (isDesignTemplate)
            {
                FinalizeCardLayout(card);
                SocialMediaFeedLayoutConstraints.ApplyToDesignTemplate(card);
            }

            return card;
        }

        private static RectTransform CreateRow(string name, RectTransform parent, bool horizontal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create feed preview UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            if (horizontal)
            {
                var h = Undo.AddComponent<HorizontalLayoutGroup>(go);
                h.spacing = 6;
                h.childControlWidth = true;
                h.childControlHeight = true;
            }
            return rt;
        }

        private static void CreateActionButton(RectTransform parent, string label)
        {
            var go = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(go, "Create feed preview UI");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;
            go.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.24f, 1f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 28f;
            var txt = CreateTMP("Label", go.GetComponent<RectTransform>(), label, 12, TextAlignmentOptions.Center, new Color(0.79f, 0.88f, 0.98f, 1f));
            Stretch(txt.rectTransform);
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create feed preview UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            go.GetComponent<Image>().color = bg;
            return rt;
        }

        private static TextMeshProUGUI CreateTMP(string name, RectTransform parent, string text, int size, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "Create feed preview UI");
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            ApplySceneFont(tmp);
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetPreferredHeight(RectTransform rt, float height)
        {
            var le = rt.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(rt.gameObject);
            le.preferredHeight = height;
        }
    }
}
#endif
