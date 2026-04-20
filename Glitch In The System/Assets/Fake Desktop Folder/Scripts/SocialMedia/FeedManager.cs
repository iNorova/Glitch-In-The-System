using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Feed ordering and change-detection — no UI references.
    /// </summary>
    public static class FeedManager
    {
        public static List<PostData> GetPublishedPostsForFeed(GameDatabase db, bool includeRemoved)
        {
            if (db == null) return new List<PostData>();

            IReadOnlyList<PostData> source = includeRemoved ? db.Posts : db.GetFeedPosts();

            return source
                .Where(p => p != null && p.isPublished && !p.isRemoved)
                .OrderByDescending(GetPostSortKey)
                .ToList();
        }

        /// <summary>Detects when feed content changed enough to warrant a UI rebuild.</summary>
        public static string BuildSignature(IReadOnlyList<PostData> posts)
        {
            if (posts == null || posts.Count == 0) return "0";

            var sb = new StringBuilder();
            sb.Append(posts.Count);
            for (int i = 0; i < posts.Count; i++)
            {
                var p = posts[i];
                if (p == null) continue;
                sb.Append('|');
                sb.Append(p.id);
                sb.Append(',');
                sb.Append(p.likes);
                sb.Append(',');
                sb.Append(p.shares);
                sb.Append(',');
                sb.Append(p.comments);
                sb.Append(',');
                sb.Append(p.wasRewrittenByAlgorithm ? 1 : 0);
                sb.Append(',');
                sb.Append(p.isRemoved ? 1 : 0);
                sb.Append(',');
                sb.Append(p.commentPreview != null ? p.commentPreview.Count : 0);
                if (p.commentPreview != null && p.commentPreview.Count > 0)
                {
                    sb.Append(',');
                    sb.Append(p.commentPreview[0].text?.Length ?? 0);
                    sb.Append(',');
                    sb.Append(p.commentPreview[0].likes);
                }
            }

            return sb.ToString();
        }

        /// <summary>Higher engagement surfaces first so viral outcomes feel immediate.</summary>
        private static int GetPostSortKey(PostData post) => post != null ? post.likes : 0;
    }
}
