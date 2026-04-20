using System.Collections.Generic;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Injects 1–2 follow-up posts after the viral memo is moderated. Not added to the moderation queue.
    /// </summary>
    public static class NarrativeFollowUpPosts
    {
        public static void RegisterUnpublished(GameDatabase db, IReadOnlyList<UserProfileData> users)
        {
            if (db == null || users == null || users.Count == 0) return;

            string authorA = users[0].id;
            string authorB = users.Count > 1 ? users[1].id : users[0].id;

            db.TryAddNarrativePost(new PostData
            {
                id = NarrativeIds.FollowUpHospitalPostId,
                authorUserId = authorA,
                text = "[Developing story] Downtown hospitals and city clinics will issue a joint update on patient volumes.",
                timestampLabel = "—",
                likes = 0,
                shares = 0,
                comments = 0,
                category = PostCategory.Narrative,
                severity = 0,
                isPublished = false,
                isRemoved = false,
                feedRank = 0
            });

            db.TryAddNarrativePost(new PostData
            {
                id = NarrativeIds.FollowUpAgencyPostId,
                authorUserId = authorB,
                text = "[Thread] Public health accounts will share guidance on verifying crisis claims online.",
                timestampLabel = "—",
                likes = 0,
                shares = 0,
                comments = 0,
                category = PostCategory.Narrative,
                severity = 0,
                isPublished = false,
                isRemoved = false,
                feedRank = 0
            });
        }

        /// <summary>Publishes follow-ups with comments that depend on whether viral misinformation actually spread.</summary>
        public static void ActivateAfterViralDecision(GameDatabase db, bool viralMisinformationPublished, IReadOnlyList<UserProfileData> users)
        {
            if (db == null || users == null) return;

            var hospital = db.GetPostById(NarrativeIds.FollowUpHospitalPostId);
            var agency = db.GetPostById(NarrativeIds.FollowUpAgencyPostId);
            if (hospital == null || agency == null) return;

            if (viralMisinformationPublished)
                ConfigureConsequenceTimeline(hospital, agency);
            else
                ConfigureNeutralTimeline(hospital, agency);

            hospital.isRemoved = false;
            agency.isRemoved = false;
            hospital.isPublished = true;
            agency.isPublished = true;

            hospital.feedRank = 99;
            agency.feedRank = 98;

            hospital.timestampLabel = "12m";
            agency.timestampLabel = "9m";

            PostManager.ApplyDecisionReaction(hospital, true, users);
            PostManager.ApplyDecisionReaction(agency, true, users);
        }

        private static void ConfigureConsequenceTimeline(PostData hospital, PostData agency)
        {
            hospital.text =
                "Hospital network: ER visits jumped after a viral water memo spread online. Doctors say most cases are anxiety + mild GI complaints, but staff are stretched thin.";
            hospital.likesApprove = 182_000;
            hospital.likesDecline = 0;
            hospital.commentsApprove = new List<string>
            {
                "This is what happens when fake memos go mega-viral.",
                "My sister went in dehydrated from panic-buying water.",
                "Mods let that post ride and now real nurses pay the price.",
                "Stop sharing 'leaks' without sources. People get hurt.",
                "Timeline feels cursed this week."
            };
            hospital.commentsDecline = new List<string>();

            agency.text =
                "Agency thread: How one unverified memo outran official corrections. Health officials ask platforms to prioritize crisis accuracy.";
            agency.likesApprove = 96_000;
            agency.likesDecline = 0;
            agency.commentsApprove = new List<string>
            {
                "Case study in why moderation timing matters.",
                "They warned us and nobody listened.",
                "Share the debunk, not the panic.",
                "Policy teams should read this thread.",
                "Real consequences from imaginary 'proof'."
            };
            agency.commentsDecline = new List<string>();
        }

        private static void ConfigureNeutralTimeline(PostData hospital, PostData agency)
        {
            hospital.text =
                "City hospitals report normal winter volumes. Officials thank residents for checking trusted alerts before sharing crisis claims.";
            hospital.likesApprove = 24_000;
            hospital.likesDecline = 0;
            hospital.commentsApprove = new List<string>
            {
                "Boring news is good news.",
                "Glad people chilled out.",
                "Shoutout to whoever stopped that memo early.",
                "Hydrate and touch grass, folks.",
                "Stable timeline for once."
            };
            hospital.commentsDecline = new List<string>();

            agency.text =
                "Public health thread: quick checklist for spotting crisis hoaxes. Engagement is high because people actually find it useful.";
            agency.likesApprove = 41_000;
            agency.likesDecline = 0;
            agency.commentsApprove = new List<string>
            {
                "Saving this for family group chats.",
                "Clear, calm, actually helpful.",
                "More of this energy online please.",
                "Good resources linked in replies.",
                "Positive spiral for a change."
            };
            agency.commentsDecline = new List<string>();
        }
    }
}
