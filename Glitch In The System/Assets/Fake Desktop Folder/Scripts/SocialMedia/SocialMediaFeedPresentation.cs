using System.Text;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Social
{
    /// <summary>Shared feed card copy/colors — used by post views and preview data.</summary>
    public static class SocialMediaFeedPresentation
    {
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
            if (post.wasRewrittenByAlgorithm) return "Rewritten by algorithm";
            if (user != null && user.isShadowBanned) return "Author visibility limited";
            if (post.isShadowBanned) return "Post visibility limited";
            if (post.isRemoved) return "Removed from public feed";
            return string.Empty;
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
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
