using System;
using System.Collections.Generic;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    [Serializable]
    public sealed class TTGLevelDefinition
    {
        public string LevelId = "Level_1";
        public GameObject LevelPrefab;
    }

    [CreateAssetMenu(fileName = "TTGLevelSequence", menuName = "Triple Tap Games/Foundation/Level Sequence")]
    public sealed class TTGLevelSequence : ScriptableObject
    {
        [SerializeField] private List<TTGLevelDefinition> levels = new List<TTGLevelDefinition>();
        [SerializeField] private bool loopAfterLastLevel;
        [SerializeField] private string progressKey = "TTG.GameFlow.LevelIndex";

        public IReadOnlyList<TTGLevelDefinition> Levels => levels;
        public int Count => levels.Count;
        public bool LoopAfterLastLevel => loopAfterLastLevel;
        public string ProgressKey => string.IsNullOrWhiteSpace(progressKey) ? "TTG.GameFlow.LevelIndex" : progressKey;

        public bool TryGetLevel(int requestedIndex, out TTGLevelDefinition level, out int resolvedIndex)
        {
            level = null;
            resolvedIndex = -1;
            if (levels.Count == 0) return false;
            resolvedIndex = loopAfterLastLevel
                ? ((requestedIndex % levels.Count) + levels.Count) % levels.Count
                : Mathf.Clamp(requestedIndex, 0, levels.Count - 1);
            level = levels[resolvedIndex];
            return level != null;
        }

        public int GetNextIndex(int currentIndex)
        {
            if (levels.Count == 0) return 0;
            var next = Mathf.Max(0, currentIndex) + 1;
            return loopAfterLastLevel ? next % levels.Count : Mathf.Min(next, levels.Count - 1);
        }

#if UNITY_EDITOR
        internal void AddEditorDefaultLevel()
        {
            if (levels.Count == 0) levels.Add(new TTGLevelDefinition());
        }
#endif
    }
}
