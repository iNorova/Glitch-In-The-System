using UnityEngine;
using GlitchInTheSystem.Algorithm;
using GlitchInTheSystem.UI;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Add this to a scene to ensure GameDatabase, AlgorithmDirector, and AlgorithmNotification exist at runtime.
    /// Optionally assign a GameDatabaseConfig asset; otherwise a default is used.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDatabaseConfig config;
    [Header("Session boot behavior")]
    [Tooltip("If true, every fresh game run starts at Day 1.")]
    [SerializeField] private bool forceStartAtDayOneOnBoot = true;

        private void Awake()
        {
            if (GameDatabase.Instance == null)
            {
                var dbGo = new GameObject("GameDatabase");
                var db = dbGo.AddComponent<GameDatabase>();
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<GameDatabaseConfig>();
                    config.postsPerDay = 10;
                    config.currentDay = 1;
                }
            if (forceStartAtDayOneOnBoot)
            {
                config.currentDay = 1;
                PlayerPrefs.SetInt(DayPacing.PlayerPrefsViralSpread, 0);
                PlayerPrefs.Save();
            }
                db.SetConfig(config);
            }

            if (AlgorithmDirector.Instance == null)
            {
                var dirGo = new GameObject("AlgorithmDirector");
                dirGo.AddComponent<AlgorithmDirector>();
            }

            if (AlgorithmNotification.Instance == null)
            {
                var notifGo = new GameObject("AlgorithmNotification");
                notifGo.AddComponent<AlgorithmNotification>();
            }

            if (GameManager.Instance == null)
            {
                var gmGo = new GameObject("GameManager");
                gmGo.AddComponent<GameManager>();
            }

            // Runtime start menu behavior fixes:
            // - Start toggles open/close
            // - FL/W close start menu after launching app
            if (GetComponent<StartMenuController>() == null)
                gameObject.AddComponent<StartMenuController>();
        }
    }
}
