using System.Text;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Social
{
    /// <summary>Shared feed card copy/colors — used by post views and preview data.</summary>
    public static class SocialMediaFeedPresentation
    {
        public static string FeedKindLabel(FeedPostKind kind)
        {
            return kind switch
            {
                FeedPostKind.PersonalUpdate => "Update",
                FeedPostKind.NewsRepost => "Repost",
                FeedPostKind.Meme => "Meme",
                FeedPostKind.EmotionalVent => "Vent",
                FeedPostKind.SponsoredAd => "Ad",
                FeedPostKind.ViralClip => "Viral",
                _ => string.Empty
            };
        }

        public static string CategoryLabel(PostCategory category)
        {
            return category switch
            {
                PostCategory.Harmless => "Harmless",
                PostCategory.Violation => "Violation",
                PostCategory.Misinformation => "Misinformation",
                PostCategory.GrayArea => "Gray Area",
                PostCategory.Narrative => "Narrative",
                PostCategory.AlgorithmManipulation => "Meta",
                _ => "Post"
            };
        }

        public static UnityEngine.Color CategoryColor(PostCategory category)
        {
            return category switch
            {
                PostCategory.Harmless => new UnityEngine.Color(0.54f, 0.88f, 0.56f, 1f),
                PostCategory.Violation => new UnityEngine.Color(0.96f, 0.43f, 0.43f, 1f),
                PostCategory.Misinformation => new UnityEngine.Color(0.99f, 0.64f, 0.31f, 1f),
                PostCategory.GrayArea => new UnityEngine.Color(0.95f, 0.84f, 0.35f, 1f),
                PostCategory.Narrative => new UnityEngine.Color(0.64f, 0.77f, 1f, 1f),
                PostCategory.AlgorithmManipulation => new UnityEngine.Color(0.88f, 0.56f, 0.95f, 1f),
                _ => UnityEngine.Color.white
            };
        }

        public static string BuildStateLabel(PostData post, UserProfileData user)
        {
            if (post == null) return string.Empty;
            if (post.isRemoved) return "Removed from public feed";
            if (user != null && user.isShadowBanned) return "Author visibility limited";
            if (post.isShadowBanned) return "Post visibility limited";
            if (post.wasRewrittenByAlgorithm) return "Rewritten by algorithm";
            return string.Empty;
        }

        public static string CommentAuthorLabel(CommentData comment, UserProfileData user = null)
        {
            string handle = comment != null ? comment.displayHandle : null;
            if (string.IsNullOrWhiteSpace(handle))
                handle = user != null ? user.username : null;
            if (string.IsNullOrWhiteSpace(handle))
                handle = comment != null ? comment.authorUserId : null;
            if (string.IsNullOrWhiteSpace(handle))
                handle = "user";

            handle = SanitizeForTMP(handle.Trim());
            if (string.IsNullOrWhiteSpace(handle))
                handle = "user";
            return handle.StartsWith("@") ? handle : $"@{handle}";
        }

        public static string SanitizeForTMP(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsSurrogate(ch))
                {
                    if (i + 1 < value.Length && char.IsSurrogatePair(value[i], value[i + 1]))
                        i++;
                    continue;
                }

                if (!IsSafeForGameFont(ch))
                    continue;

                sb.Append(ch);
            }

            return sb.ToString();
        }

        /// <summary>Pixeboy-style TMP assets are ASCII-first; drop emoji and symbol blocks that spam missing-glyph warnings.</summary>
        private static bool IsSafeForGameFont(char ch)
        {
            if (ch == '\n' || ch == '\r' || ch == '\t') return true;
            if (ch < 128) return true;
            // Latin-1 supplement + Latin extended-A (accents in names/copy).
            if (ch is >= '\u00A0' and <= '\u024F') return true;
            // Common punctuation used in post copy.
            if (ch is '\u2013' or '\u2014' or '\u2018' or '\u2019' or '\u201C' or '\u201D' or '\u2026')
                return true;
            return false;
        }
    }
}
