using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TripleTapGames.Foundation.Editor
{
    [InitializeOnLoad]
    public static class TTGLoadingSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Loading.unity";
        public const string RequestPath = "Assets/TTGGenerated/LoadingSceneSetup.request";
        public static event Action<TTGProjectConfig, Scene> Configuring;

        static TTGLoadingSceneSetup()
        {
            TTGConfigurationHooks.Applying += Apply;
            EditorApplication.delayCall += CompletePendingRequest;
        }

        private static void CompletePendingRequest()
        {
            if (!File.Exists(ToProjectAbsolutePath(RequestPath))) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Debug.LogWarning("[TTG:Setup] Stop Play mode and use Create / Update Local Loading Scene to complete setup.");
                return;
            }
            try { CreateOrUpdate(); }
            catch (Exception) { Debug.LogWarning("[TTG:Setup] Save the Loading scene and use Create / Update Local Loading Scene to complete setup. Existing scene edits were preserved."); }
        }

        [MenuItem("Tools/Triple Tap Games/Create or Update Local Loading Scene")]
        public static void CreateOrUpdate()
        {
            var config = TTGConfigAssetUtility.GetOrCreateProjectConfig();
            TTGConfigAssetUtility.GetOrCreateAdsConfig();
            Apply(config);
        }

        // Explicit batch entry point; never closes a user's interactive editor scene.
        public static void CreateOrUpdateForBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use Create / Update Local Loading Scene in the editor.");
            var existing = EditorBuildSettings.scenes.FirstOrDefault(s =>
                AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) != null);
            if (existing == null) throw new InvalidOperationException("A saved scene must exist in Build Settings before batch Loading generation.");
            EditorSceneManager.OpenScene(existing.path, OpenSceneMode.Single);
            CreateOrUpdate();
        }

        public static void Apply(TTGProjectConfig config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before updating the Loading scene.");
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Singular.Enabled && Configuring == null)
                throw new InvalidOperationException("Singular scene adapter is unavailable. Install the pinned Singular SDK before enabling it.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save your Loading scene changes before applying configuration.");
            var wasOpen = scene.IsValid() && scene.isLoaded;
            var previousActive = SceneManager.GetActiveScene();
            if (!wasOpen)
            {
                if (EditorSceneManager.GetSceneManagerSetup().Any(s => string.IsNullOrEmpty(s.path)))
                    throw new InvalidOperationException("Save or open a named scene before generating Loading; unsaved untitled scenes are preserved.");
                Directory.CreateDirectory(ToProjectAbsolutePath("Assets/Scenes"));
                scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                    ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var behaviours = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(b => b != null).ToArray();
                var bootstraps = behaviours.OfType<TTGBootstrap>().ToArray();
                if (bootstraps.Length > 1) throw new InvalidOperationException("Loading contains more than one TTGBootstrap component.");
                var legacyBootstraps = behaviours.Where(b => b.GetType().Name == "TTGBootstrapExample").ToArray();
                if (legacyBootstraps.Length > 1) throw new InvalidOperationException("Loading contains duplicate Bootstrap Example components.");
                TTGBootstrap bootstrap = bootstraps.SingleOrDefault();
                if (bootstrap == null)
                {
                    var go = legacyBootstraps.SingleOrDefault()?.gameObject ?? roots.FirstOrDefault(r => r.name == "TTGBootstrap");
                    if (go == null) { go = new GameObject("TTGBootstrap"); SceneManager.MoveGameObjectToScene(go, scene); }
                    bootstrap = Undo.AddComponent<TTGBootstrap>(go);
                }
                foreach (var legacy in legacyBootstraps)
                {
                    Undo.RecordObject(legacy, "Disable legacy TTG bootstrap");
                    legacy.enabled = false;
                }
                if (bootstrap.GetComponent<TTGConsentBootstrap>() == null)
                    Undo.AddComponent<TTGConsentBootstrap>(bootstrap.gameObject);
                ConfigureLoadingScreen(scene, bootstrap);
                Configuring?.Invoke(config, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Loading scene could not be saved.");
                var corePath = "Assets/Scenes/Core.unity";
                var firstScenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.AsEnumerable();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(corePath) != null)
                    firstScenes = firstScenes.Append(new EditorBuildSettingsScene(corePath, true));
                EditorBuildSettings.scenes = firstScenes
                    .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != corePath)).ToArray();
                var requestAbsolutePath = ToProjectAbsolutePath(RequestPath);
                if (File.Exists(requestAbsolutePath)) File.Delete(requestAbsolutePath);
                Debug.Log("[TTG:Setup] Local Loading scene updated and placed first in Build Settings. Scene credentials are not logged; keep this scene out of Git.");
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static string ToProjectAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static void ConfigureLoadingScreen(Scene scene, TTGBootstrap bootstrap)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "TTGLoadingScreen");
            if (root == null)
            {
                root = new GameObject("TTGLoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);

                var background = CreateImage("Background", root.transform, new Color(0.035f, 0.045f, 0.075f, 1f));
                Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                var title = CreateText("Title", background.transform, "Loading", 54, TextAnchor.MiddleCenter);
                Stretch(title.rectTransform, new Vector2(0.12f, 0.55f), new Vector2(0.88f, 0.67f), Vector2.zero, Vector2.zero);

                var progress = CreateImage("Progress", background.transform, new Color(1f, 1f, 1f, 0.18f));
                Stretch(progress.rectTransform, new Vector2(0.12f, 0.46f), new Vector2(0.88f, 0.485f), Vector2.zero, Vector2.zero);
                var fill = CreateImage("Fill", progress.transform, new Color(0.16f, 0.67f, 1f, 1f));
                Stretch(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.fillAmount = 0f;

                var status = CreateText("Status", background.transform, "Preparing services...", 28, TextAnchor.UpperCenter);
                status.color = new Color(1f, 1f, 1f, 0.75f);
                Stretch(status.rectTransform, new Vector2(0.12f, 0.38f), new Vector2(0.88f, 0.44f), Vector2.zero, Vector2.zero);
            }

            var fillImage = root.transform.Find("Background/Progress/Fill")?.GetComponent<Image>();
            var statusText = root.transform.Find("Background/Status")?.GetComponent<Text>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("progressFill").objectReferenceValue = fillImage;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.FindProperty("waitForConsent").boolValue = true;
            serialized.FindProperty("loadNextScene").boolValue = true;
            serialized.FindProperty("nextSceneName").stringValue = "Core";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
