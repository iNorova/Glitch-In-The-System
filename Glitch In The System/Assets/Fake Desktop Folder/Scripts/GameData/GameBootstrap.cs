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
        [SerializeField] private AlgorithmTrustSettings algorithmTrustSettings;

        [Header("Session boot behavior")]
    [Tooltip("If true, every fresh game run starts at Day 1.")]
    [SerializeField] private bool forceStartAtDayOneOnBoot = true;

        private void Awake()
        {
            if (GameDatabase.Instance == null)
            {
                var dbGo = CreateSystemObject("GameDatabase");
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

            EnsureAlgorithmManager();

            if (AlgorithmDirector.Instance == null)
                CreateSystemObject("AlgorithmDirector").AddComponent<AlgorithmDirector>();

            if (AlgorithmNotification.Instance == null)
                CreateSystemObject("AlgorithmNotification").AddComponent<AlgorithmNotification>();

            if (GameManager.Instance == null)
                CreateSystemObject("GameManager").AddComponent<GameManager>();

            // Runtime start menu behavior fixes:
            // - Start toggles open/close
            // - FL/W close start menu after launching app
            if (GetComponent<StartMenuController>() == null)
                gameObject.AddComponent<StartMenuController>();

            if (AlgorithmGlitchHighlight.Instance == null)
                CreateSystemObject("AlgorithmGlitchHighlight").AddComponent<AlgorithmGlitchHighlight>();

            DesktopLauncherHub.EnsureInitialized();
        }

        public AlgorithmTrustSettings AlgorithmTrustSettings => algorithmTrustSettings;

#if UNITY_EDITOR
        public void SetAlgorithmTrustSettings(AlgorithmTrustSettings settings)
        {
            algorithmTrustSettings = settings;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void EnsureAlgorithmManager()
        {
            AlgorithmManager manager = GetComponentInChildren<AlgorithmManager>(true);
            if (manager == null)
            {
                if (AlgorithmManager.Instance != null)
                    manager = AlgorithmManager.Instance;
                else
                    manager = CreateSystemObject("AlgorithmManager").AddComponent<AlgorithmManager>();
            }

            if (algorithmTrustSettings != null)
                manager.ApplyTrustSettings(algorithmTrustSettings);
        }

        /// <summary>Spawns runtime systems under a DontDestroyOnLoad root (not under scene hierarchy).</summary>
        private static GameObject CreateSystemObject(string objectName)
        {
            var go = new GameObject(objectName);
            RuntimePersistency.Adopt(go);
            return go;
        }
    }
}
