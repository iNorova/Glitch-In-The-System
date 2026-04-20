using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Single place to read/write the in-game day (synced with <see cref="GameDatabaseConfig.currentDay"/>).
    /// Place on a bootstrap object or DDOL manager; works with <see cref="GameDatabase"/>.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameDatabaseConfig configFallback;

        public int CurrentDay => ResolveConfig()?.currentDay ?? 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>New day number before you call <see cref="GameDatabase.InitializeSession"/> again.</summary>
        public void SetCurrentDay(int day)
        {
            var cfg = ResolveConfig();
            if (cfg == null) return;
            cfg.currentDay = Mathf.Max(1, day);
        }

        /// <summary>Typical flow after a day completes: bump day, then start a new session.</summary>
        public void AdvanceToNextDay()
        {
            var cfg = ResolveConfig();
            if (cfg == null) return;
            cfg.currentDay = Mathf.Max(1, cfg.currentDay + 1);
        }

        private GameDatabaseConfig ResolveConfig()
        {
            if (GameDatabase.Instance != null && GameDatabase.Instance.Config != null)
                return GameDatabase.Instance.Config;
            return configFallback;
        }
    }
}
