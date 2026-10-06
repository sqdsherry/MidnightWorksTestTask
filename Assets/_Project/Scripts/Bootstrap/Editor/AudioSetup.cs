using AutoService.Infrastructure.Services.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoService.Bootstrap.Editor
{
    public static class AudioSetup
    {
        private const string GameplayScenePath = "Assets/_Project/Scenes/Gameplay.unity";

        [MenuItem("AutoService/Setup/Configure Audio")]
        public static void ConfigureAudio()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameplayScenePath)
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            var audioService = Object.FindFirstObjectByType<AudioService>(FindObjectsInactive.Include);
            GameObject audioGo;
            if (audioService == null)
            {
                audioGo = new GameObject("AudioService");
                audioService = audioGo.AddComponent<AudioService>();
            }
            else
            {
                audioGo = audioService.gameObject;
            }

            var musicSource = audioGo.GetComponent<AudioSource>();
            if (musicSource == null) musicSource = audioGo.AddComponent<AudioSource>();

            var sfxGo = audioGo.transform.Find("SfxSource");
            AudioSource sfxSource;
            if (sfxGo == null)
            {
                var child = new GameObject("SfxSource");
                child.transform.SetParent(audioGo.transform);
                sfxSource = child.AddComponent<AudioSource>();
            }
            else
            {
                sfxSource = sfxGo.GetComponent<AudioSource>();
            }

            var serialized = new SerializedObject(audioService);
            serialized.FindProperty("_musicSource").objectReferenceValue = musicSource;
            serialized.FindProperty("_sfxSource").objectReferenceValue = sfxSource;

            serialized.FindProperty("_buttonClickClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/sfx_click.ogg");
            serialized.FindProperty("_cashClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/sfx_money.ogg");
            serialized.FindProperty("_workCompletedClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/sfx_service_complete.ogg");

            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[AudioSetup] Successfully configured AudioService and clips in Gameplay.unity!");
        }
    }
}
