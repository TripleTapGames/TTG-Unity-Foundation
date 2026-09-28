using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TripleTapGames.Foundation
{
    public enum TTGLevelState { None, Playing, Won, Lost }

    [DisallowMultipleComponent]
    public sealed class TTGGameFlow : MonoBehaviour
    {
        [SerializeField] private TTGLevelSequence levelSequence;
        [SerializeField] private Transform levelRoot;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;
        [SerializeField] private Text levelLabel;
        [SerializeField] private UnityEvent onLevelStarted;
        [SerializeField] private UnityEvent onLevelWon;
        [SerializeField] private UnityEvent onLevelLost;

        private GameObject currentLevelObject;
        private int currentLevelIndex;

        public static TTGGameFlow Instance { get; private set; }
        public TTGLevelState State { get; private set; }
        public int CurrentLevelIndex => currentLevelIndex;
        public int CurrentLevelNumber => currentLevelIndex + 1;
        public TTGLevelDefinition CurrentLevel { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[TTG:GameFlow] More than one game-flow controller is active.");
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void Start() => StartSavedLevel();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartSavedLevel()
        {
            if (levelSequence == null)
            {
                FailConfiguration("Assign a TTGLevelSequence to TTGGameFlow.");
                return;
            }
            LoadLevel(PlayerPrefs.GetInt(levelSequence.ProgressKey, 0));
        }

        public void LoadLevel(int sequenceIndex)
        {
            if (levelSequence == null || !levelSequence.TryGetLevel(sequenceIndex, out var level, out var resolvedIndex))
            {
                FailConfiguration("Add at least one level to the assigned TTGLevelSequence.");
                return;
            }

            if (currentLevelObject != null) Destroy(currentLevelObject);
            currentLevelIndex = resolvedIndex;
            CurrentLevel = level;
            if (level.LevelPrefab != null)
                currentLevelObject = Instantiate(level.LevelPrefab, levelRoot != null ? levelRoot : transform);

            SetOutcomePanels(false, false);
            State = TTGLevelState.Playing;
            if (levelLabel != null) levelLabel.text = "Level " + CurrentLevelNumber;
            TTGAnalytics.LevelStarted(GetAnalyticsLevelId());
            onLevelStarted?.Invoke();
        }

        public void WinLevel()
        {
            if (State != TTGLevelState.Playing) return;
            State = TTGLevelState.Won;
            TTGAnalytics.LevelCompleted(GetAnalyticsLevelId());
            TTGAds.NotifyLevelCompleted(CurrentLevelNumber);
            SetOutcomePanels(true, false);
            onLevelWon?.Invoke();
        }

        public void LoseLevel()
        {
            if (State != TTGLevelState.Playing) return;
            State = TTGLevelState.Lost;
            TTGAnalytics.LevelFailed(GetAnalyticsLevelId());
            SetOutcomePanels(false, true);
            onLevelLost?.Invoke();
        }

        public void NextLevel()
        {
            if (State != TTGLevelState.Won || levelSequence == null) return;
            var next = levelSequence.GetNextIndex(currentLevelIndex);
            PlayerPrefs.SetInt(levelSequence.ProgressKey, next);
            PlayerPrefs.Save();
            LoadLevel(next);
        }

        public void RetryLevel()
        {
            if (State != TTGLevelState.Lost) return;
            LoadLevel(currentLevelIndex);
        }

        public void ResetProgress()
        {
            if (levelSequence != null) PlayerPrefs.DeleteKey(levelSequence.ProgressKey);
            LoadLevel(0);
        }

        [ContextMenu("Debug/Win Current Level")]
        private void DebugWin() => WinLevel();

        [ContextMenu("Debug/Lose Current Level")]
        private void DebugLose() => LoseLevel();

        private string GetAnalyticsLevelId()
        {
            return string.IsNullOrWhiteSpace(CurrentLevel?.LevelId)
                ? "Level_" + CurrentLevelNumber
                : CurrentLevel.LevelId.Trim();
        }

        private void SetOutcomePanels(bool showWin, bool showLose)
        {
            if (winPanel != null) winPanel.SetActive(showWin);
            if (losePanel != null) losePanel.SetActive(showLose);
        }

        private void FailConfiguration(string message)
        {
            State = TTGLevelState.None;
            SetOutcomePanels(false, false);
            if (levelLabel != null) levelLabel.text = "Game flow is not configured";
            Debug.LogError("[TTG:GameFlow] " + message);
        }
    }
}
