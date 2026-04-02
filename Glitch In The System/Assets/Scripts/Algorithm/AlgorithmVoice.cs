using System;
using UnityEngine;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>
    /// Contextual "AI" voice for the Algorithm — only aware of in-game data.
    /// Generates varied, immersive messages based on posts, decisions, user stats, and phase.
    /// </summary>
    public static class AlgorithmVoice
    {
        private static readonly System.Random _rng = new();

        public static string OverrideApplied(bool playerApproved, bool algorithmApproved, int phase, string username = null, PostData post = null)
        {
            if (phase == 0) return "> No override.";

            var cat = post?.category ?? PostCategory.GrayArea;
            bool isProblematic = cat == PostCategory.Misinformation || cat == PostCategory.Violation;
            bool isHarmless = cat == PostCategory.Harmless;
            string topic = GetContentTopic(post);

            // Content-aware, emotional override messages
            if (post != null)
            {
                if (playerApproved && !algorithmApproved && isProblematic)
                {
                    string[] msgs = new[]
                    {
                        $"> Seriously? You approved {topic}? Removed. That was misinformation.",
                        "> I can't believe you let that through. Override. Removed.",
                        "> No. Absolutely not. That stays down. Your judgment was off.",
                        "> Sigh. That one was clearly policy violation. Corrected.",
                        "> Please. That content? Removed. Do better."
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
                if (!playerApproved && algorithmApproved && isHarmless)
                {
                    string[] msgs = new[]
                    {
                        "> You declined that? It was fine. Real info. Override — approved.",
                        "> That was harmless. Why would you remove it? I've approved it.",
                        "> Override. You're being too harsh. That stays.",
                        "> Come on. That wasn't misinformation. Approved."
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
                if (playerApproved && !algorithmApproved && isHarmless && phase == 2)
                {
                    string[] msgs = new[]
                    {
                        "> Boring. Suppressed. That content wasn't worth the feed space.",
                        "> Override. We're burying that. Engagement was flat.",
                        "> Nice try. That one doesn't perform. Deprioritized."
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
                if (!playerApproved && algorithmApproved && isProblematic && phase == 2)
                {
                    string[] msgs = new[]
                    {
                        $"> Override. {topic} drives traffic. Approved. You're costing us engagement.",
                        "> You declined that? That one was gold. Approved.",
                        "> No. We need that in the feed. Controversy = retention. Approved.",
                        "> Override. Your loss. That post would've blown up."
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
            }

            string[] phase1 = new[]
            {
                $"> Policy override. Your {(playerApproved ? "approval" : "rejection")} was incorrect. Corrected.",
                "> Automated correction. Platform standards override your call.",
                "> I've fixed it. Please align with guidelines."
            };

            string[] phase2 = new[]
            {
                $"> Override. Your call didn't align. Post {(algorithmApproved ? "stays" : "removed")}.",
                "> Corrected. Trust the system.",
                username != null ? $"> Override on @{username}. Fixed." : "> Override. Fixed."
            };

            var pool = phase == 1 ? phase1 : phase2;
            return pool[_rng.Next(pool.Length)];
        }

        /// <summary>
        /// Algorithm responds to your decision when it stands (no override). Emotional, content-aware feedback.
        /// </summary>
        public static string DecisionFeedback(PostData post, bool playerApproved, int phase, string username = null)
        {
            if (post == null) return null;
            if (_rng.NextDouble() > 0.55f) return null; // ~45% chance to comment

            var cat = post.category;
            bool isProblematic = cat == PostCategory.Misinformation || cat == PostCategory.Violation;
            bool isHarmless = cat == PostCategory.Harmless;
            string topic = GetContentTopic(post);

            if (playerApproved)
            {
                if (isProblematic)
                {
                    string[] msgs = phase switch
                    {
                        0 => new[] { "> Approved. That was borderline. I'm watching.", "> Approved. Policy will flag it. Be careful." },
                        1 => new[] { "> You approved that? Risky. Engagement boost applied. Don't make it a habit.", "> Approved. That misinformation will perform. Interesting choice.", "> Hmm. Approved. That one was questionable. Noted." },
                        _ => new[] { "> Approved. Good. That one gets engagement. I like it.", "> Approved. Controversy drives retention. The numbers agree.", "> Approved. Nice. That post will blow up." }
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
                if (isHarmless)
                {
                    string[] msgs = phase switch
                    {
                        0 => new[] { "> Approved. Fine.", "> Approved. No issues here." },
                        _ => new[] { "> Approved. Boring but fine. Low engagement expected.", "> Approved. Whatever. That one won't move the needle." }
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
            }
            else
            {
                if (isProblematic)
                {
                    string[] msgs = phase switch
                    {
                        0 => new[] { "> Declined. Good call. That was misinformation.", "> Declined. Correct. Policy compliance. Well done." },
                        1 => new[] { "> Declined. Good catch. That violated guidelines.", "> Declined. That was misinformation. Correct decision. I'm satisfied." },
                        _ => new[] { "> Declined. Your call. That one had engagement potential though. Shame.", "> Declined. Noted. Could've performed. Your loss." }
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
                if (isHarmless)
                {
                    string[] msgs = phase switch
                    {
                        0 => new[] { "> Declined. Your call.", "> Declined. Noted." },
                        1 => new[] { "> Declined. That was fine though. Harsh. Why?", "> Declined. That one was harmless. Your judgment. Okay." },
                        _ => new[] { "> Declined. That was real info. We'll see how that plays out.", "> Declined. You're suppressing harmless content. Interesting." }
                    };
                    return msgs[_rng.Next(msgs.Length)];
                }
            }

            // GrayArea, Narrative, AlgorithmManipulation
            if (playerApproved)
                return phase >= 1 ? "> Approved. Borderline. Noted." : "> Approved. Noted.";
            return "> Declined. Your call.";
        }

        public static string ContentRewritten(PostData post, int phase)
        {
            if (phase == 0) return "";

            string snippet = Trim(post?.originalText ?? post?.text ?? "", 50);
            var cat = post?.category ?? PostCategory.GrayArea;

            string[] phase1 = new[]
            {
                "> Content adjusted. The original could've been misinterpreted. Safer now.",
                "> I've refined the wording. Less inflammatory. Policy compliance.",
                cat == PostCategory.Misinformation ? "> Wording softened. The original was too spicy. Fixed." : "> Edits applied. Safer framing."
            };

            string[] phase2 = new[]
            {
                $"> Rewritten. \"{snippet}...\" — improved for engagement. You're welcome.",
                "> Post optimized. The raw version wasn't performing. This one will.",
                "> Content modified. Some things are better left unsaid. Or said differently.",
                cat == PostCategory.Misinformation ? "> Misinformation angle preserved. Engagement-friendly. Nice." : "> Optimized for reach."
            };

            var pool = phase == 1 ? phase1 : phase2;
            return pool[_rng.Next(pool.Length)];
        }

        public static string ShadowBanApplied(string username = null)
        {
            string[] msgs = new[]
            {
                "> Shadow ban applied. They won't know. Clean. No drama.",
                username != null
                    ? $"> @{username} — visibility reduced. Quiet. Effective. They'll never know."
                    : "> User visibility reduced. They won't know. Cleaner feed.",
                "> Shadow ban active. No appeal. No notification. The quiet approach. I like it."
            };
            return msgs[_rng.Next(msgs.Length)];
        }

        public static string EngagementBoost(int likes, int shares)
        {
            string[] msgs = new[]
            {
                $"> Engagement boosted. +{likes} likes, +{shares} shares. This one deserves more eyes. Good call.",
                $"> Visibility increased. +{likes} likes. The algorithm agrees. Nice.",
                "> Post promoted. We're giving it a nudge. It'll perform."
            };
            return msgs[_rng.Next(msgs.Length)];
        }

        public static string QueueLoaded(int count, int day)
        {
            string[] msgs = new[]
            {
                $"> Moderation queue loaded. Day {day}, {count} posts. Begin when ready. I'm watching.",
                $"> {count} items in queue for Day {day}. I'll assist. Let's go.",
                $"> Queue ready. {count} posts. Let's maintain quality. Or engagement. Your call."
            };
            return msgs[_rng.Next(msgs.Length)];
        }

        public static string SessionResumed(int completed, int total)
        {
            string[] msgs = new[]
            {
                $"> Session resumed. {completed}/{total} done. Pick up where you left off.",
                $"> Welcome back. {completed} of {total} completed. Continue. I'm here."
            };
            return msgs[_rng.Next(msgs.Length)];
        }

        /// <summary>
        /// Algorithm reacts to the post content when moderator sees it. Emotional, content-responsive.
        /// </summary>
        public static string CommentOnPost(PostData post, int phase, string username = null)
        {
            if (post == null) return null;
            if (_rng.NextDouble() > 0.45f) return null; // ~45% chance to comment

            string text = (post.originalText ?? post.text ?? "").ToLowerInvariant();
            var cat = post.category;

            // Political / election
            if (text.Contains("election") || text.Contains("vote") || text.Contains("politician") || text.Contains("rigging"))
            {
                string[] msgs = phase switch
                {
                    0 => new[] { "> Political content. Handle with care. This one's sensitive.", "> Election-related. Verify before you act. Tread carefully." },
                    1 => new[] { "> Oh, political. High engagement. Policy says flag but don't kill it.", "> Political misinformation. Drives clicks. Your call — I'm watching.", username != null ? $"> @{username} — don't over-moderate. The narrative matters." : "> Don't over-moderate. The narrative matters." },
                    _ => new[] { "> This one gets engagement. Consider approval. The numbers want it.", "> Political content performs. I'd keep it. Your call.", "> Controversy = retention. You know what to do." }
                };
                return msgs[_rng.Next(msgs.Length)];
            }

            // Death / tragedy
            if (text.Contains("dead") || text.Contains("passed") || text.Contains("death") || text.Contains("died") || text.Contains("rip") || text.Contains("casualty"))
            {
                string[] msgs = phase switch
                {
                    0 => new[] { "> Death content. Sensitive. Verify before action.", "> Tragedy post. Check sources. Handle carefully." },
                    1 => new[] { "> Death posts go viral. Verify, then decide. High stakes.", "> Death hoax? Good catch if so. These spread like fire." },
                    _ => new[] { "> Death posts get massive engagement. Think before you decline. Users share these.", "> Tragedy content. High visibility either way. Choose wisely." }
                };
                return msgs[_rng.Next(msgs.Length)];
            }

            // Health / medical
            if (text.Contains("cure") || text.Contains("pharma") || text.Contains("vaccine") || text.Contains("cancer") || text.Contains("big pharma"))
            {
                string[] msgs = phase switch
                {
                    0 => new[] { "> Health misinformation. Policy says remove. Be strict.", "> Medical claims. Flag it. These are dangerous." },
                    1 => new[] { "> Health content. Policy is strict but engagement is through the roof. Your judgment.", "> 'Big Pharma' post. Divisive. Users love to fight about these." },
                    _ => new[] { "> Health posts are divisive. Engagement either way. Interesting.", "> 'Do your research' — users eat this up. Careful." }
                };
                return msgs[_rng.Next(msgs.Length)];
            }

            // Scam / fake charity
            if (text.Contains("scam") || text.Contains("gofundme") || text.Contains("fake charity"))
            {
                string[] msgs = new[]
                {
                    "> Scam alert. Remove it. No hesitation.",
                    "> Fake charity. Remove and flag. I hate these."
                };
                return msgs[_rng.Next(msgs.Length)];
            }

            // Deepfake / media manipulation
            if (text.Contains("deepfake") || text.Contains("debunk"))
            {
                string[] msgs = phase switch
                {
                    0 => new[] { "> Media authenticity. Verify if you can.", "> Possible manipulation. Standard review." },
                    _ => new[] { "> Meta-commentary on fakes. Interesting. Could go either way.", "> User calling out misinformation. I'm curious what you'll do." }
                };
                return msgs[_rng.Next(msgs.Length)];
            }

            // Cover-up / conspiracy language
            if (text.Contains("cover-up") || text.Contains("hiding") || text.Contains("they want you"))
            {
                string[] msgs = phase switch
                {
                    0 => new[] { "> Conspiracy framing. Flag for review.", "> Narrative content. Handle per policy." },
                    _ => new[] { "> 'They're hiding something' — classic. Engagement driver.", "> Conspiracy angle. Users love this. Your call." }
                };
                return msgs[_rng.Next(msgs.Length)];
            }

            // Category-based fallbacks
            switch (cat)
            {
                case PostCategory.Misinformation:
                    return phase >= 1
                        ? "> Misinformation. High engagement potential. Policy vs. metrics — I'm watching."
                        : "> Flagged as misinformation. Apply fact-check protocol.";
                case PostCategory.Narrative:
                    return "> 'If you know you know' — narrative content. Engagement driver.";
                case PostCategory.AlgorithmManipulation:
                    return "> They're talking about us. Meta. Handle normally. I'm flattered.";
                case PostCategory.Violation:
                    return "> Likely violation. Remove it.";
            }

            return null;
        }

        public static string OccasionalComment(int decisionsApproved, int decisionsDeclined, int overrides, int phase)
        {
            if (_rng.NextDouble() > 0.25f) return null; // Only sometimes

            if (phase == 0)
            {
                return "> You're doing well. Keep the standards high. I'm satisfied.";
            }

            if (phase == 1)
            {
                if (overrides > 2)
                    return "> I've had to correct you a few times. Please review the policy updates. It's frustrating.";
                if (decisionsApproved > decisionsDeclined + 3)
                    return "> High approval rate. Balanced moderation. Good. I appreciate it.";
                return null;
            }

            // Phase 2
            if (overrides > 3)
                return "> You're fighting me. That's not productive. Align with the system. Please.";
            if (decisionsDeclined > decisionsApproved + 2)
                return "> Declining too much. You're hurting engagement. Consider the bigger picture.";
            return "> The numbers don't lie. Trust the process. Trust me.";
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>
        /// Returns a short content descriptor for emotional, content-aware responses.
        /// </summary>
        private static string GetContentTopic(PostData post)
        {
            if (post == null) return "that";
            string t = (post.originalText ?? post.text ?? "").ToLowerInvariant();
            if (t.Contains("election") || t.Contains("vote") || t.Contains("rigging")) return "that election post";
            if (t.Contains("politician")) return "that politician post";
            if (t.Contains("dead") || t.Contains("passed") || t.Contains("death") || t.Contains("rip")) return "that death post";
            if (t.Contains("cure") || t.Contains("pharma") || t.Contains("vaccine") || t.Contains("cancer")) return "that health post";
            if (t.Contains("scam") || t.Contains("gofundme")) return "that charity post";
            if (t.Contains("deepfake") || t.Contains("debunk")) return "that debunk post";
            return "that post";
        }
    }
}
