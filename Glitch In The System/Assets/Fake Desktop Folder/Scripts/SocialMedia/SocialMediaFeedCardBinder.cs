using System;
using System.Text;
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.GameData;
using GlitchInTheSystem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Updates text on scene-authored editor feed cards (does not change RectTransforms).
    /// Non-static so component references can be cached per-card after first bind.
    /// </summary>
    public sealed class SocialMediaFeedCardBinder
    {
        // ── Pre-baked comment name constants — avoids $"Comment_{i}" alloc in loop ──
        private static readonly string[] CommentSlotNames = { "Comment_0", "Comment_1", "Comment_2" };

        // ── Shared StringBuilder — reused across all instances for string composition ──
        private static readonly StringBuilder _sb = new StringBuilder(256);

        // ── Engagement label colors — cached to avoid new Color() allocs ──────────
        private static readonly Color ColorTrending  = new Color(1f,    0.62f, 0.28f, 1f);
        private static readonly Color ColorEngagement = new Color(0.65f, 0.72f, 0.82f, 1f);

        // ── Per-card cached component references ─────────────────────────────────
        // Populated on first Apply() call for this cardRoot, then reused every
        // subsequent bind. Safe because card GameObjects are pooled/reused in place;
        // the hierarchy never changes between binds.
        private Transform  _cardRoot;
        private TMP_Text   _authorText;
        private TMP_Text   _categoryTag;
        private TMP_Text   _bodyText;
        private TMP_Text   _engagementText;
        private TMP_Text   _timeText;
        private TMP_Text   _engagementLabel;
        private TMP_Text   _stateText;
        private Transform  _commentsSection;
        private Transform  _commentsPanel;
        private Button     _commentsToggleButton; // ActionButton or CommentsToggle
        private TMP_Text   _commentsToggleLabel;
        private TMP_Text[] _commentLines;          // [0..2]
        private bool       _cached;


        private void ApplyInternal(
            Transform cardRoot,
            PostData post,
            UserProfileData user,
            bool expandComments,
            Func<string, UserProfileData> getCommentUser = null)
        {
            if (cardRoot == null || post == null) return;

            // ── Populate cache on first call for this root ────────────────────
            if (!_cached || _cardRoot != cardRoot)
                BuildCache(cardRoot);

            // ── Author ────────────────────────────────────────────────────────
            if (_authorText != null)
            {
                _sb.Clear();
                if (user != null)
                    _sb.Append(user.displayName).Append("  @").Append(user.username);
                else
                    _sb.Append('@').Append(post.authorUserId);
                _authorText.text = _sb.ToString();
            }

            // ── Category tag ─────────────────────────────────────────────────
            if (_categoryTag != null)
            {
                string kind = SocialMediaFeedPresentation.FeedKindLabel(post.feedKind);
                _sb.Clear();
                if (string.IsNullOrEmpty(kind))
                    _sb.Append(SocialMediaFeedPresentation.CategoryLabel(post.category));
                else
                    _sb.Append(kind).Append(" \u00b7 ")
                       .Append(SocialMediaFeedPresentation.CategoryLabel(post.category));
                _categoryTag.text  = _sb.ToString();
                _categoryTag.color = SocialMediaFeedPresentation.CategoryColor(post.category);
            }

            // ── Body ──────────────────────────────────────────────────────────
            if (_bodyText != null)
            {
                _sb.Clear();
                _sb.Append(SocialMediaFeedPresentation.SanitizeForTMP(post.text));
                if (!string.IsNullOrWhiteSpace(post.imageDescription))
                    _sb.Append("\n\n[Image: ")
                       .Append(SocialMediaFeedPresentation.SanitizeForTMP(post.imageDescription))
                       .Append(']');
                _bodyText.text = _sb.ToString();
            }

            // ── Engagement / time ─────────────────────────────────────────────
            if (_engagementText != null) _engagementText.text = post.EngagementDisplay;
            if (_timeText       != null) _timeText.text       = post.timestampLabel;

            // ── Engagement label ──────────────────────────────────────────────
            if (_engagementLabel != null)
            {
                bool show = !string.IsNullOrEmpty(post.engagementLabel);
                _engagementLabel.gameObject.SetActive(show);
                if (show)
                {
                    _engagementLabel.text  = post.engagementLabel;
                    _engagementLabel.color = post.engagementLabel == "TRENDING"
                        ? ColorTrending : ColorEngagement;
                }
            }

            // ── State text ────────────────────────────────────────────────────
            if (_stateText != null)
            {
                string state = SocialMediaFeedPresentation.BuildStateLabel(post, user);
                bool hasState = !string.IsNullOrEmpty(state);
                _stateText.gameObject.SetActive(hasState);
                if (hasState) _stateText.text = state;
            }

            // ── Comments section visibility ───────────────────────────────────
            int  commentCount = post.commentPreview?.Count ?? 0;
            bool hasComments  = commentCount > 0;

            if (_commentsSection != null)
                _commentsSection.gameObject.SetActive(hasComments);

            if (_commentsToggleButton != null)
            {
                _commentsToggleButton.gameObject.SetActive(hasComments);
                if (hasComments && _commentsToggleLabel != null)
                {
                    // "Comments (N)" — use Append+ToString to avoid interpolation alloc
                    _sb.Clear();
                    _sb.Append("Comments (").Append(commentCount).Append(')');
                    _commentsToggleLabel.text = _sb.ToString();
                }
            }

            // ── Comments panel ────────────────────────────────────────────────
            if (_commentsPanel != null)
            {
                bool isEditorPreview = false;
#if UNITY_EDITOR
                isEditorPreview = !Application.isPlaying
                    && cardRoot.GetComponent<SocialMediaFeedEditorPost>() != null;
#endif
                bool showPanel = (expandComments && hasComments) || isEditorPreview;
                _commentsPanel.gameObject.SetActive(showPanel);

                int show = Mathf.Min(3, commentCount > 0 ? commentCount : 3);
                for (int i = 0; i < 3; i++)
                {
                    var line = _commentLines[i];
                    if (line == null) continue;
                    bool visible = isEditorPreview || i < show;
                    line.gameObject.SetActive(visible);
                    if (!visible) continue;
                    if (commentCount > 0 && i < post.commentPreview.Count)
                    {
                        var c = post.commentPreview[i];
                        var commentUser = getCommentUser?.Invoke(c.authorUserId);
                        if (commentUser == null && user != null && user.id == c.authorUserId)
                            commentUser = user;
                        string commenter = SocialMediaFeedPresentation.CommentAuthorLabel(c, commentUser);
                        _sb.Clear();
                        _sb.Append(commenter).Append(": ")
                           .Append(SocialMediaFeedPresentation.SanitizeForTMP(c.text));
                        line.text = _sb.ToString();
                    }
                    else if (isEditorPreview)
                        line.text = line.text.Length > 0 ? line.text : "@user: Sample comment for layout.";
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && cardRoot.GetComponent<SocialMediaFeedEditorPost>() != null)
                SocialMediaFeedEditorUtility.ForcePostVisible(cardRoot as RectTransform);
#endif
        }

        /// <summary>Brief corrupted flash when algorithm touched this post.</summary>
        private void FlashInternal(Transform cardRoot, PostData post, bool emphasizeRewrite = false)
        {
            if (cardRoot == null || post == null) return;
            if (!_cached || _cardRoot != cardRoot) BuildCache(cardRoot);

            bool manipulated = AlgorithmManager.Instance != null
                && AlgorithmManager.Instance.TryGetManipulatedPost(post.id, out _);

            bool rewrite = emphasizeRewrite || post.wasRewrittenByAlgorithm;
            if (rewrite && _bodyText != null)
                AlgorithmGlitchHighlight.FlashTmpAfterLayout(_bodyText, isRewrite: true, frameDelay: 1);

            if (manipulated)
            {
                if (_engagementText != null)
                    AlgorithmGlitchHighlight.FlashTmpAfterLayout(_engagementText, isRewrite: false, frameDelay: 1);
                if (_stateText != null && _stateText.gameObject.activeSelf)
                    AlgorithmGlitchHighlight.FlashTmpAfterLayout(_stateText, isRewrite: false, frameDelay: 1);
            }
        }

        // ── Public static API — compatible with all existing callers ─────────
        public static void Apply(
            Transform cardRoot,
            PostData post,
            UserProfileData user,
            bool expandComments,
            Func<string, UserProfileData> getCommentUser = null)
        {
            var b = new SocialMediaFeedCardBinder();
            b.ApplyInternal(cardRoot, post, user, expandComments, getCommentUser);
        }

        public static void FlashAlterationGlitch(
            Transform cardRoot, PostData post, bool emphasizeRewrite = false)
        {
            var b = new SocialMediaFeedCardBinder();
            b.FlashInternal(cardRoot, post, emphasizeRewrite);
        }


        // ── Cache population ──────────────────────────────────────────────────
        private void BuildCache(Transform root)
        {
            _cardRoot = root;

            _authorText      = FindTmp(root, "AuthorText");
            _categoryTag     = FindTmp(root, "CategoryTag");
            _bodyText        = FindTmp(root, "BodyText");
            _engagementText  = FindTmp(root, "EngagementText");
            _timeText        = FindTmp(root, "TimeText");
            _engagementLabel = FindTmp(root, "EngagementLabel");
            _stateText       = FindTmp(root, "StateText");

            _commentsSection = root.Find("CommentsSection");

            // CommentsPanel can be a direct child or under CommentsSection
            _commentsPanel = root.Find("CommentsPanel");
            if (_commentsPanel == null && _commentsSection != null)
                _commentsPanel = _commentsSection.Find("CommentsPanel");

            // Comments toggle button — find ActionButton or CommentsToggle without allocating
            _commentsToggleButton = null;
            _commentsToggleLabel  = null;
            // Direct children only first (avoids GetComponentsInChildren alloc in common case)
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                string n = child.name;
                if (n == "ActionButton" || n == "CommentsToggle")
                {
                    _commentsToggleButton = child.GetComponent<Button>();
                    _commentsToggleLabel  = child.GetComponentInChildren<TMP_Text>(true);
                    break;
                }
            }
            // Fallback: deep search (only runs once per card lifetime)
            if (_commentsToggleButton == null)
            {
                foreach (var btn in root.GetComponentsInChildren<Button>(true))
                {
                    if (btn == null) continue;
                    string n = btn.name;
                    if (n == "ActionButton" || n == "CommentsToggle")
                    {
                        _commentsToggleButton = btn;
                        _commentsToggleLabel  = btn.GetComponentInChildren<TMP_Text>(true);
                        break;
                    }
                }
            }

            // Comment line TMP refs — resolved once using pre-baked name constants
            _commentLines = new TMP_Text[3];
            if (_commentsPanel != null)
            {
                for (int i = 0; i < 3; i++)
                    _commentLines[i] = FindTmp(_commentsPanel, CommentSlotNames[i]);
            }

            _cached = true;
        }

        // ── FindTmp: direct find first, no-fallback-alloc path ───────────────
        // Falls back to GetComponentsInChildren ONLY during BuildCache (once per card),
        // never during per-frame Apply() calls.
        private static TMP_Text FindTmp(Transform root, string name)
        {
            // Fast path: direct child or named descendant via Unity's native find
            var t = root.Find(name);
            if (t != null) return t.GetComponent<TMP_Text>();

            // Fallback: deep scan — allocates once during cache build, never per-frame
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                if (tmp.name == name) return tmp;
            return null;
        }
    
    
}
}
