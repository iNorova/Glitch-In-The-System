using System;
using GlitchInTheSystem.GameData;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>Event bridge so UI can glitch-highlight altered posts without polling.</summary>
    public static class AlgorithmPostAlteredNotifier
    {
        public static event Action<PostData, bool> PostAltered;

        public static void Notify(PostData post, bool rewrite) => PostAltered?.Invoke(post, rewrite);
    }
}
