using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures click audio works when entering GameplayScene directly from the editor (without LoginScene).
/// </summary>
public static class GameplayAudioBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.name.Contains("Gameplay"))
            return;

        if (GlobalClickAudio.HasInstance)
            return;

        var clip = LoadClip("Assets/Audio/Click.mp3");
        if (clip == null)
            return;

        var go = new GameObject("GlobalClickAudio");
        go.SetActive(false);

        var clickAudio = go.AddComponent<GlobalClickAudio>();
        clickAudio.Configure(clip, volume: 0.5f, surviveSceneLoads: false);
        go.SetActive(true);
    }

    private static AudioClip LoadClip(string assetPath)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#else
        return Resources.Load<AudioClip>("Audio/Click");
#endif
    }
}
