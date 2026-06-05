using System.Collections.Generic;
using System.Text;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Social
{
    /// <summary>
    /// Feed ordering and change-detection — no UI references.
    /// </summary>
    public static class FeedManager
    {
        // ── Cached collections — reused across every feed refresh ─────────────
        // Scratch list for filtering before sort; cleared each call.
        private static readonly List<PostData> _filterScratch = new List<PostData>(64);

        // Cached output list returned each call — CALLER MUST NOT HOLD REFERENCES
        // across frames; it is overwritten on the next GetPublishedPostsForFeed call.
        // SocialMediaFeedController already rebuilds UI from the list synchronously,
        // so this is safe.
        private static readonly List<PostData> _resultCache = new List<PostData>(64);

        // Reused StringBuilder for BuildSignature — eliminates per-call allocation.
        private static readonly StringBuilder _sigBuilder = new StringBuilder(4096);

        // ─────────────────────────────────────────────────────────────────────
        public static List<PostData> GetPublishedPostsForFeed(GameDatabase db, bool includeRemoved)
        {
            _filterScratch.Clear();
            _resultCache.Clear();

            if (db == null) return _resultCache;

            IReadOnlyList<PostData> source = includeRemoved ? db.Posts : db.GetFeedPosts();

            // Manual filter — zero LINQ allocations
            int sourceCount = source.Count;
            for (int i = 0; i < sourceCount; i++)
            {
                PostData p = source[i];
                if (ShouldShowInFeed(p, includeRemoved))
                    _filterScratch.Add(p);
            }

            // Insertion sort — stable, allocation-free, fast for typical small-to-medium
            // feed sizes (< ~200 posts). Preserves exact ordering semantics of the
            // original OrderByDescending(feedRank).ThenByDescending(likes).
            int n = _filterScratch.Count;
            for (int i = 1; i < n; i++)
            {
                PostData key = _filterScratch[i];
                int keyRank  = key != null ? key.feedRank : 0;
                int keyLikes = key != null ? GetPostSortKey(key) : 0;
                int j = i - 1;
                while (j >= 0 && CompareDesc(_filterScratch[j], keyRank, keyLikes) < 0)
                {
                    _filterScratch[j + 1] = _filterScratch[j];
                    j--;
                }
                _filterScratch[j + 1] = key;
            }

            // Copy sorted result into the output cache
            _resultCache.AddRange(_filterScratch);
            return _resultCache;
        }

        /// <summary>Detects when feed content changed enough to warrant a UI rebuild.</summary>
        public static string BuildSignature(IReadOnlyList<PostData> posts)
        {
            if (posts == null || posts.Count == 0) return "0";

            _sigBuilder.Clear();
            _sigBuilder.Append(posts.Count);

            for (int i = 0; i < posts.Count; i++)
            {
                PostData p = posts[i];
                if (p == null)
                {
                    _sigBuilder.Append("|<null>");
                    continue;
                }

                _sigBuilder.Append('|');
                AppendField(_sigBuilder, p.id);
                AppendField(_sigBuilder, p.authorUserId);
                AppendField(_sigBuilder, p.text);
                AppendField(_sigBuilder, p.imageDescription);
                AppendField(_sigBuilder, p.timestampLabel);
                AppendField(_sigBuilder, p.engagementLabel);
                _sigBuilder.Append(p.likes).Append(',');
                _sigBuilder.Append(p.shares).Append(',');
                _sigBuilder.Append(p.comments).Append(',');
                _sigBuilder.Append(p.feedRank).Append(',');
                _sigBuilder.Append((int)p.category).Append(',');
                _sigBuilder.Append((int)p.feedKind).Append(',');
                _sigBuilder.Append((int)p.presentationFormat).Append(',');
                _sigBuilder.Append(p.isPublished ? 1 : 0).Append(',');
                _sigBuilder.Append(p.wasRewrittenByAlgorithm ? 1 : 0).Append(',');
                _sigBuilder.Append(p.isRemoved ? 1 : 0).Append(',');
                _sigBuilder.Append(p.isShadowBanned ? 1 : 0).Append(',');
                _sigBuilder.Append(p.commentPreview != null ? p.commentPreview.Count : 0).Append(',');

                if (p.commentPreview == null) continue;
                for (int j = 0; j < p.commentPreview.Count; j++)
                {
                    var c = p.commentPreview[j];
                    if (c == null)
                    {
                        _sigBuilder.Append("<null-comment>,");
                        continue;
                    }

                    AppendField(_sigBuilder, c.id);
                    AppendField(_sigBuilder, c.postId);
                    AppendField(_sigBuilder, c.authorUserId);
                    AppendField(_sigBuilder, c.displayHandle);
                    AppendField(_sigBuilder, c.text);
                    AppendField(_sigBuilder, c.timestampLabel);
                    _sigBuilder.Append(c.likes).Append(',');
                    _sigBuilder.Append(c.isHidden ? 1 : 0).Append(',');
                    _sigBuilder.Append(c.replyToIndex).Append(',');
                    _sigBuilder.Append(c.botFlag ? 1 : 0).Append(',');
                }
            }

            return _sigBuilder.ToString();
        }

        private static void AppendField(StringBuilder sb, string value)
        {
            value ??= string.Empty;
            sb.Append(value.Length).Append(':').Append(value).Append(',');
        }

        /// <summary>
        /// Comparator for descending sort by feedRank then likes.
        /// Returns positive when 'existing' should come AFTER 'key' (i.e. key wins).
        /// </summary>
        private static int CompareDesc(PostData existing, int keyRank, int keyLikes)
        {
            int existRank  = existing != null ? existing.feedRank : 0;
            int existLikes = existing != null ? GetPostSortKey(existing) : 0;

            // Primary: feedRank descending — higher rank first
            if (existRank != keyRank) return existRank - keyRank; // positive = existing > key = existing stays

            // Secondary: likes descending
            return existLikes - keyLikes;
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
