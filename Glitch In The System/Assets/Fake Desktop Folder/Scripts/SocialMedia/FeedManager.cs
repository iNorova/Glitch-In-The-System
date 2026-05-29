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
                .Where(p => ShouldShowInFeed(p, includeRemoved))
                .OrderByDescending(p => p.feedRank)
                .ThenByDescending(GetPostSortKey)
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
                if (p == null)
                {
                    sb.Append("|<null>");
                    continue;
                }

                sb.Append('|');
                AppendField(sb, p.id);
                AppendField(sb, p.authorUserId);
                AppendField(sb, p.text);
                AppendField(sb, p.imageDescription);
                AppendField(sb, p.timestampLabel);
                AppendField(sb, p.engagementLabel);
                sb.Append(p.likes).Append(',');
                sb.Append(p.shares).Append(',');
                sb.Append(p.comments).Append(',');
                sb.Append(p.feedRank).Append(',');
                sb.Append((int)p.category).Append(',');
                sb.Append((int)p.feedKind).Append(',');
                sb.Append((int)p.presentationFormat).Append(',');
                sb.Append(p.isPublished ? 1 : 0).Append(',');
                sb.Append(p.wasRewrittenByAlgorithm ? 1 : 0).Append(',');
                sb.Append(p.isRemoved ? 1 : 0).Append(',');
                sb.Append(p.isShadowBanned ? 1 : 0).Append(',');
                sb.Append(p.commentPreview != null ? p.commentPreview.Count : 0).Append(',');

                if (p.commentPreview == null) continue;
                for (int j = 0; j < p.commentPreview.Count; j++)
                {
                    var c = p.commentPreview[j];
                    if (c == null)
                    {
                        sb.Append("<null-comment>,");
                        continue;
                    }

                    AppendField(sb, c.id);
                    AppendField(sb, c.postId);
                    AppendField(sb, c.authorUserId);
                    AppendField(sb, c.displayHandle);
                    AppendField(sb, c.text);
                    AppendField(sb, c.timestampLabel);
                    sb.Append(c.likes).Append(',');
                    sb.Append(c.isHidden ? 1 : 0).Append(',');
                    sb.Append(c.replyToIndex).Append(',');
                    sb.Append(c.botFlag ? 1 : 0).Append(',');
                }
            }

            return sb.ToString();
        }

        private static void AppendField(StringBuilder sb, string value)
        {
            value ??= string.Empty;
            sb.Append(value.Length).Append(':').Append(value).Append(',');
        }

        /// <summary>Higher engagement surfaces first so viral outcomes feel immediate.</summary>
        private static int GetPostSortKey(PostData post) => post != null ? post.likes : 0;

        private static bool ShouldShowInFeed(PostData post, bool includeRemoved)
        {
            if (post == null) return false;
            if (includeRemoved) return post.isPublished || post.isRemoved;
            return post.isPublished && !post.isRemoved;
        }
    }
}
