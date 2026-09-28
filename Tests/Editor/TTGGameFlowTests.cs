using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGGameFlowTests
    {
        private TTGLevelSequence sequence;
        private GameObject root;
        private TTGGameFlow flow;
        private RecordingProvider analytics;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("TTG.Tests.GameFlow");
            TTGAnalytics.ClearProviders();
            sequence = ScriptableObject.CreateInstance<TTGLevelSequence>();
            var sequenceObject = new SerializedObject(sequence);
            sequenceObject.FindProperty("progressKey").stringValue = "TTG.Tests.GameFlow";
            var levels = sequenceObject.FindProperty("levels");
            levels.arraySize = 2;
            levels.GetArrayElementAtIndex(0).FindPropertyRelative("LevelId").stringValue = "Level_A";
            levels.GetArrayElementAtIndex(1).FindPropertyRelative("LevelId").stringValue = "Level_B";
            sequenceObject.ApplyModifiedPropertiesWithoutUndo();

            root = new GameObject("FlowTest");
            flow = root.AddComponent<TTGGameFlow>();
            var flowObject = new SerializedObject(flow);
            flowObject.FindProperty("levelSequence").objectReferenceValue = sequence;
            flowObject.ApplyModifiedPropertiesWithoutUndo();
            analytics = new RecordingProvider();
            TTGAnalytics.RegisterProvider(analytics);
        }

        [TearDown]
        public void TearDown()
        {
            TTGAnalytics.ClearProviders();
            PlayerPrefs.DeleteKey("TTG.Tests.GameFlow");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sequence);
        }

        [Test]
        public void WinAdvancesSequenceAndEmitsProgressionOnce()
        {
            flow.LoadLevel(0);
            flow.WinLevel();
            flow.WinLevel();
            flow.NextLevel();

            Assert.That(analytics.Events, Is.EqualTo(new[]
            {
                TTGEventNames.LevelStart, TTGEventNames.LevelComplete, TTGEventNames.LevelStart
            }));
            Assert.That(flow.CurrentLevelIndex, Is.EqualTo(1));
            Assert.That(flow.CurrentLevel.LevelId, Is.EqualTo("Level_B"));
            Assert.That(PlayerPrefs.GetInt("TTG.Tests.GameFlow"), Is.EqualTo(1));
        }

        [Test]
        public void LoseAndRetryEmitFailThenAnotherStart()
        {
            flow.LoadLevel(0);
            flow.LoseLevel();
            flow.LoseLevel();
            flow.RetryLevel();

            Assert.That(analytics.Events, Is.EqualTo(new[]
            {
                TTGEventNames.LevelStart, TTGEventNames.LevelFail, TTGEventNames.LevelStart
            }));
            Assert.That(flow.State, Is.EqualTo(TTGLevelState.Playing));
        }

        [Test]
        public void EmptySequenceFailsWithoutSendingAnalytics()
        {
            var empty = ScriptableObject.CreateInstance<TTGLevelSequence>();
            var flowObject = new SerializedObject(flow);
            flowObject.FindProperty("levelSequence").objectReferenceValue = empty;
            flowObject.ApplyModifiedPropertiesWithoutUndo();
            LogAssert.Expect(LogType.Error, "[TTG:GameFlow] Add at least one level to the assigned TTGLevelSequence.");
            flow.LoadLevel(0);
            Assert.That(flow.State, Is.EqualTo(TTGLevelState.None));
            Assert.That(analytics.Events, Is.Empty);
            Object.DestroyImmediate(empty);
        }

        private sealed class RecordingProvider : ITTGAnalyticsProvider
        {
            internal readonly List<string> Events = new List<string>();
            public string ProviderName => "GameFlow Test";
            public bool IsInitialized => true;
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
                => Events.Add(eventName);
        }
    }
}
