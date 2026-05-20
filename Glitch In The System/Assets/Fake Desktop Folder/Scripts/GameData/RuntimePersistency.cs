using UnityEngine;

namespace GlitchInTheSystem.GameData
{
    /// <summary>
    /// Single root for DontDestroyOnLoad runtime systems (Unity requires a root GameObject).
    /// </summary>
    public static class RuntimePersistency
    {
        private static Transform _root;

        public static Transform Root
        {
            get
            {
                if (_root != null) return _root;

                var existing = GameObject.Find("GlitchRuntimeSystems");
                if (existing != null)
                {
                    _root = existing.transform;
                    return _root;
                }

                var go = new GameObject("GlitchRuntimeSystems");
                Object.DontDestroyOnLoad(go);
                _root = go.transform;
                return _root;
            }
        }

        /// <summary>Parents <paramref name="system"/> under the persistent root (no DontDestroyOnLoad on children).</summary>
        public static void Adopt(GameObject system)
        {
            if (system == null) return;
            system.transform.SetParent(Root, false);
        }
    }
}
