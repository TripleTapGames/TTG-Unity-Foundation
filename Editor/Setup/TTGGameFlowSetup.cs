using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TripleTapGames.Foundation.Editor
{
    [InitializeOnLoad]
    public static class TTGGameFlowSetup
    {
        public const string CoreScenePath = "Assets/Scenes/Core.unity";
        public const string SequencePath = "Assets/Game/Config/DefaultLevelSequence.asset";
        public const string RequestPath = "Assets/TTGGenerated/GameFlowSetup.request";

        static TTGGameFlowSetup() => EditorApplication.delayCall += CompletePendingRequest;

        [MenuItem("Tools/Triple Tap Games/Create or Update Base Game Flow")]
        public static void CreateOrUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before updating the Core game flow.");
            var sequence = GetOrCreateSequence();
            var scene = SceneManager.GetSceneByPath(CoreScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save Core scene changes before updating the base game flow.");
            var wasOpen = scene.IsValid() && scene.isLoaded;
            var previousActive = SceneManager.GetActiveScene();
            if (!wasOpen)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath) == null)
                {
                    Directory.CreateDirectory(ToAbsolute("Assets/Scenes"));
                    scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                }
                else scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);
            }
            try
            {
                var roots = scene.GetRootGameObjects();
                var flows = roots.SelectMany(r => r.GetComponentsInChildren<TTGGameFlow>(true)).ToArray();
                if (flows.Length > 1) throw new InvalidOperationException("Core contains more than one TTGGameFlow component.");
                var flowObject = flows.SingleOrDefault()?.gameObject ?? roots.FirstOrDefault(r => r.name == "TTGGameFlow");
                if (flowObject == null)
                {
                    flowObject = new GameObject("TTGGameFlow");
                    SceneManager.MoveGameObjectToScene(flowObject, scene);
                }
                var flow = flowObject.GetComponent<TTGGameFlow>() ?? Undo.AddComponent<TTGGameFlow>(flowObject);
                EnsureEventSystem(scene);
                var levelRoot = flowObject.transform.Find("LevelRoot");
                if (levelRoot == null)
                {
                    var go = new GameObject("LevelRoot");
                    go.transform.SetParent(flowObject.transform, false);
                    levelRoot = go.transform;
                }

                var ui = GetOrCreateUi(scene, flow);
                var serialized = new SerializedObject(flow);
                serialized.FindProperty("levelSequence").objectReferenceValue = sequence;
                serialized.FindProperty("levelRoot").objectReferenceValue = levelRoot;
                serialized.FindProperty("winPanel").objectReferenceValue = ui.WinPanel;
                serialized.FindProperty("losePanel").objectReferenceValue = ui.LosePanel;
                serialized.FindProperty("levelLabel").objectReferenceValue = ui.LevelLabel;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, CoreScenePath)) throw new IOException("Core scene could not be saved.");
                EnsureBuildSettings();
                DeleteRequest();
                Debug.Log("[TTG:GameFlow] Core base flow, level sequence, and Win/Lose UI are ready.");
            }
            finally
            {
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static void EnsureEventSystem(Scene scene)
        {
            var eventSystems = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if (eventSystems.Length > 1)
                throw new InvalidOperationException("Core contains more than one EventSystem. Keep only one UI input system.");
            if (eventSystems.Length == 1) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        public static void CreateOrUpdateForBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use Create or Update Base Game Flow in the editor.");
            var existing = EditorBuildSettings.scenes.FirstOrDefault(s =>
                AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) != null);
            if (existing == null) throw new InvalidOperationException("A saved scene must exist before batch Core generation.");
            EditorSceneManager.OpenScene(existing.path, OpenSceneMode.Single);
            CreateOrUpdate();
        }

        private static void CompletePendingRequest()
        {
            if (!File.Exists(ToAbsolute(RequestPath))) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            try { CreateOrUpdate(); }
            catch (Exception) { Debug.LogWarning("[TTG:GameFlow] Save Core and use Create or Update Base Game Flow. Existing edits were preserved."); }
        }

        private static TTGLevelSequence GetOrCreateSequence()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<TTGLevelSequence>(SequencePath);
            if (sequence != null) return sequence;
            Directory.CreateDirectory(ToAbsolute("Assets/Game/Config"));
            sequence = ScriptableObject.CreateInstance<TTGLevelSequence>();
            var serialized = new SerializedObject(sequence);
            var levels = serialized.FindProperty("levels");
            levels.arraySize = 1;
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("LevelId").stringValue = "Level_1";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(sequence, SequencePath);
            AssetDatabase.SaveAssets();
            return sequence;
        }

        private static FlowUi GetOrCreateUi(Scene scene, TTGGameFlow flow)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "TTGGameFlowUI");
            if (root == null)
            {
                root = new GameObject("TTGGameFlowUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.GetComponent<Canvas>().sortingOrder = 50;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);

                var levelLabel = CreateText("LevelLabel", root.transform, "Level 1", 42, TextAnchor.MiddleCenter);
                Stretch(levelLabel.rectTransform, new Vector2(0.25f, 0.9f), new Vector2(0.75f, 0.97f));
                var win = CreateOutcomePanel("WinPanel", root.transform, "YOU WIN", "NEXT", out var nextButton);
                var lose = CreateOutcomePanel("LosePanel", root.transform, "TRY AGAIN", "RETRY", out var retryButton);
                UnityEventTools.AddPersistentListener(nextButton.onClick, flow.NextLevel);
                UnityEventTools.AddPersistentListener(retryButton.onClick, flow.RetryLevel);
                win.SetActive(false);
                lose.SetActive(false);
            }

            return new FlowUi
            {
                LevelLabel = root.transform.Find("LevelLabel")?.GetComponent<Text>(),
                WinPanel = root.transform.Find("WinPanel")?.gameObject,
                LosePanel = root.transform.Find("LosePanel")?.gameObject
            };
        }

        private static GameObject CreateOutcomePanel(string name, Transform parent, string title, string buttonLabel, out Button button)
        {
            var panel = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.78f)).gameObject;
            Stretch(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            var heading = CreateText("Title", panel.transform, title, 64, TextAnchor.MiddleCenter);
            Stretch(heading.rectTransform, new Vector2(0.12f, 0.55f), new Vector2(0.88f, 0.68f));
            var buttonImage = CreateImage("ActionButton", panel.transform, new Color(0.14f, 0.62f, 1f, 1f));
            Stretch(buttonImage.rectTransform, new Vector2(0.25f, 0.38f), new Vector2(0.75f, 0.47f));
            button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            var label = CreateText("Label", buttonImage.transform, buttonLabel, 34, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            return panel;
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

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureBuildSettings()
        {
            var loading = "Assets/Scenes/Loading.unity";
            var ordered = EditorBuildSettings.scenes.Where(s => s.path != loading && s.path != CoreScenePath).ToList();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(loading) != null)
                ordered.Insert(0, new EditorBuildSettingsScene(loading, true));
            ordered.Insert(AssetDatabase.LoadAssetAtPath<SceneAsset>(loading) != null ? 1 : 0,
                new EditorBuildSettingsScene(CoreScenePath, true));
            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static void DeleteRequest()
        {
            var path = ToAbsolute(RequestPath);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string ToAbsolute(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        private sealed class FlowUi
        {
            internal Text LevelLabel;
            internal GameObject WinPanel;
            internal GameObject LosePanel;
        }
    }
}
