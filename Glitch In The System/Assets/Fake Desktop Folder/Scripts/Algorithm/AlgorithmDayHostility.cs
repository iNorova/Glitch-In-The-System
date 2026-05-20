using GlitchInTheSystem.GameData;
using UnityEngine;

namespace GlitchInTheSystem.Algorithm
{
    /// <summary>Scales interference by narrative day so early sessions feel helpful and later ones hostile.</summary>
    public static class AlgorithmDayHostility
    {
        /// <summary>Multiplier applied to override/rewrite rolls (days 1–3 use DayPacing baseline separately).</summary>
        public static float GetInterferenceMultiplier(int day)
        {
            var config = GameDatabase.Instance?.Config;
            if (day == 1 && config != null && config.day1EnableAlgorithmTest)
                return Mathf.Max(0.05f, config.day1TestHostilityMultiplier);

            day = Mathf.Clamp(day, 1, 14);
            return day switch
            {
                1 => 0.08f,
                2 => 0.22f,
                3 => 0.38f,
                4 => 0.55f,
                5 => 0.68f,
                6 => 0.82f,
                7 => 1f,
                _ => Mathf.Min(1.15f, 1f + (day - 7) * 0.03f)
            };
        }

        public static int GetCurrentDay() =>
            GameDatabase.Instance?.Config != null ? GameDatabase.Instance.Config.currentDay : 1;
    }
}
