using System;
using System.Collections.Generic;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public sealed class TTGLevelProgressRecord
    {
        public string LevelId { get; }
        public int LastKnownLevelNumber { get; }
        public int AttemptCount { get; }
        public int CompletionCount { get; }
        public bool IsCompleted => CompletionCount > 0;
        public int BestStars { get; }
        public long BestScore { get; }

        internal TTGLevelProgressRecord(string levelId, int lastKnownLevelNumber, int attemptCount,
            int completionCount, int bestStars, long bestScore)
        {
            LevelId = levelId;
            LastKnownLevelNumber = lastKnownLevelNumber;
            AttemptCount = attemptCount;
            CompletionCount = completionCount;
            BestStars = bestStars;
            BestScore = bestScore;
        }
    }

    public sealed class TTGLevelProgressSnapshot
    {
        public string TrackId { get; }
        public int HighestUnlockedLevel { get; }
        public IReadOnlyList<TTGLevelProgressRecord> Levels { get; }

        internal TTGLevelProgressSnapshot(string trackId, int highestUnlockedLevel,
            IReadOnlyList<TTGLevelProgressRecord> levels)
        {
            TrackId = trackId;
            HighestUnlockedLevel = highestUnlockedLevel;
            Levels = levels;
        }
    }

    public sealed class TTGLevelProgressSeedRecord
    {
        public string LevelId { get; }
        public int LastKnownLevelNumber { get; }
        public int AttemptCount { get; }
        public int CompletionCount { get; }
        public int BestStars { get; }
        public long BestScore { get; }

        public TTGLevelProgressSeedRecord(string levelId, int lastKnownLevelNumber, int attemptCount = 0,
            int completionCount = 0, int bestStars = 0, long bestScore = 0)
        {
            LevelId = levelId;
            LastKnownLevelNumber = lastKnownLevelNumber;
            AttemptCount = attemptCount;
            CompletionCount = completionCount;
            BestStars = bestStars;
            BestScore = bestScore;
        }
    }

    public sealed class TTGLevelProgressSeed
    {
        public int HighestUnlockedLevel { get; }
        public IReadOnlyList<TTGLevelProgressSeedRecord> Levels { get; }

        public TTGLevelProgressSeed(int highestUnlockedLevel,
            IEnumerable<TTGLevelProgressSeedRecord> levels = null)
        {
            HighestUnlockedLevel = highestUnlockedLevel;
            Levels = Array.AsReadOnly(levels == null
                ? Array.Empty<TTGLevelProgressSeedRecord>()
                : new List<TTGLevelProgressSeedRecord>(levels).ToArray());
        }
    }

    /// <summary>
    /// Persists linear level progress without owning a game's level catalog, loading, UI, analytics, or ads.
    /// Level numbers are one-based and level IDs are stable, case-sensitive identifiers.
    /// </summary>
    public static class TTGLevelProgress
    {
        public const string DefaultTrackId = "default";

        internal const string StorageKeyPrefix = "TTG.Foundation.LevelProgress.v1.";
        private const int CurrentVersion = 1;

        public static TTGLevelProgressRecord RecordAttempt(int levelNumber, string levelId,
            string trackId = DefaultTrackId)
        {
            ValidateLevel(levelNumber, levelId);
            ValidateTrackId(trackId);

            var save = LoadOrCreate(trackId);
            var record = GetOrCreateRecord(save, levelNumber, levelId);
            record.LastKnownLevelNumber = levelNumber;
            record.AttemptCount = IncrementWithoutOverflow(record.AttemptCount);
            Save(trackId, save);
            return ToPublicRecord(record);
        }

        public static TTGLevelProgressRecord RecordCompletion(int levelNumber, string levelId, int stars = 0,
            long score = 0, string trackId = DefaultTrackId)
        {
            ValidateLevel(levelNumber, levelId);
            ValidateResult(stars, score);
            ValidateTrackId(trackId);

            var save = LoadOrCreate(trackId);
            var record = GetOrCreateRecord(save, levelNumber, levelId);
            record.LastKnownLevelNumber = levelNumber;
            record.CompletionCount = IncrementWithoutOverflow(record.CompletionCount);
            record.BestStars = Math.Max(record.BestStars, stars);
            record.BestScore = Math.Max(record.BestScore, score);
            save.HighestUnlockedLevel = Math.Max(save.HighestUnlockedLevel, NextLevelNumber(levelNumber));
            Save(trackId, save);
            return ToPublicRecord(record);
        }

        public static bool IsUnlocked(int levelNumber, string trackId = DefaultTrackId)
        {
            if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level numbers are one-based.");
            ValidateTrackId(trackId);
            return levelNumber <= LoadOrCreate(trackId).HighestUnlockedLevel;
        }

        public static TTGLevelProgressRecord GetLevel(string levelId, string trackId = DefaultTrackId)
        {
            ValidateLevelId(levelId);
            ValidateTrackId(trackId);
            var record = FindRecord(LoadOrCreate(trackId), levelId);
            return record == null ? null : ToPublicRecord(record);
        }

        public static TTGLevelProgressSnapshot GetSnapshot(string trackId = DefaultTrackId)
        {
            ValidateTrackId(trackId);
            return ToSnapshot(trackId, LoadOrCreate(trackId));
        }

        public static bool TrySeed(TTGLevelProgressSeed seed, string trackId = DefaultTrackId)
        {
            if (seed == null) throw new ArgumentNullException(nameof(seed));
            ValidateTrackId(trackId);
            if (seed.HighestUnlockedLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(seed), "Highest unlocked level must be at least 1.");

            var save = CreateSave();
            save.HighestUnlockedLevel = seed.HighestUnlockedLevel;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < seed.Levels.Count; i++)
            {
                var source = seed.Levels[i];
                if (source == null) throw new ArgumentException("Seed level records cannot be null.", nameof(seed));
                ValidateLevel(source.LastKnownLevelNumber, source.LevelId);
                if (source.AttemptCount < 0 || source.CompletionCount < 0)
                    throw new ArgumentException("Seed attempt and completion counts cannot be negative.", nameof(seed));
                ValidateResult(source.BestStars, source.BestScore);
                if (!seenIds.Add(source.LevelId))
                    throw new ArgumentException("Seed level IDs must be unique.", nameof(seed));

                save.Levels.Add(new LevelRecordData
                {
                    LevelId = source.LevelId,
                    LastKnownLevelNumber = source.LastKnownLevelNumber,
                    AttemptCount = source.AttemptCount,
                    CompletionCount = source.CompletionCount,
                    BestStars = source.BestStars,
                    BestScore = source.BestScore
                });
                if (source.CompletionCount > 0)
                    save.HighestUnlockedLevel = Math.Max(save.HighestUnlockedLevel,
                        NextLevelNumber(source.LastKnownLevelNumber));
            }

            var key = GetStorageKey(trackId);
            if (PlayerPrefs.HasKey(key)) return false;
            Save(trackId, save);
            return true;
        }

        public static void Reset(string trackId = DefaultTrackId)
        {
            ValidateTrackId(trackId);
            PlayerPrefs.DeleteKey(GetStorageKey(trackId));
            PlayerPrefs.Save();
        }

        internal static string GetStorageKey(string trackId) => StorageKeyPrefix + trackId;

        private static ProgressSaveData LoadOrCreate(string trackId)
        {
            var key = GetStorageKey(trackId);
            if (!PlayerPrefs.HasKey(key)) return CreateSave();

            try
            {
                var json = PlayerPrefs.GetString(key);
                var save = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<ProgressSaveData>(json);
                if (IsValid(save)) return save;
            }
            catch (Exception)
            {
                // The common diagnostic below deliberately avoids exposing the saved payload.
            }

            TTGLogger.Warning(TTGLogCategory.Progress,
                "Saved level progress for track '" + trackId + "' is invalid. Fresh progress will be used.");
            return CreateSave();
        }

        private static bool IsValid(ProgressSaveData save)
        {
            if (save == null || save.Version != CurrentVersion || save.HighestUnlockedLevel < 1 || save.Levels == null)
                return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < save.Levels.Count; i++)
            {
                var record = save.Levels[i];
                if (record == null || string.IsNullOrWhiteSpace(record.LevelId) || record.LastKnownLevelNumber < 1
                    || record.AttemptCount < 0 || record.CompletionCount < 0 || record.BestStars < 0
                    || record.BestScore < 0 || !ids.Add(record.LevelId))
                    return false;
            }
            return true;
        }

        private static void Save(string trackId, ProgressSaveData save)
        {
            save.Levels.Sort(CompareRecords);
            PlayerPrefs.SetString(GetStorageKey(trackId), JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private static ProgressSaveData CreateSave()
        {
            return new ProgressSaveData
            {
                Version = CurrentVersion,
                HighestUnlockedLevel = 1,
                Levels = new List<LevelRecordData>()
            };
        }

        private static LevelRecordData GetOrCreateRecord(ProgressSaveData save, int levelNumber, string levelId)
        {
            var record = FindRecord(save, levelId);
            if (record != null) return record;
            record = new LevelRecordData { LevelId = levelId, LastKnownLevelNumber = levelNumber };
            save.Levels.Add(record);
            return record;
        }

        private static LevelRecordData FindRecord(ProgressSaveData save, string levelId)
        {
            for (var i = 0; i < save.Levels.Count; i++)
                if (string.Equals(save.Levels[i].LevelId, levelId, StringComparison.Ordinal))
                    return save.Levels[i];
            return null;
        }

        private static TTGLevelProgressSnapshot ToSnapshot(string trackId, ProgressSaveData save)
        {
            var records = new TTGLevelProgressRecord[save.Levels.Count];
            for (var i = 0; i < save.Levels.Count; i++) records[i] = ToPublicRecord(save.Levels[i]);
            Array.Sort(records, ComparePublicRecords);
            return new TTGLevelProgressSnapshot(trackId, save.HighestUnlockedLevel, Array.AsReadOnly(records));
        }

        private static TTGLevelProgressRecord ToPublicRecord(LevelRecordData record)
        {
            return new TTGLevelProgressRecord(record.LevelId, record.LastKnownLevelNumber, record.AttemptCount,
                record.CompletionCount, record.BestStars, record.BestScore);
        }

        private static void ValidateLevel(int levelNumber, string levelId)
        {
            if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level numbers are one-based.");
            ValidateLevelId(levelId);
        }

        private static void ValidateLevelId(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("A stable level ID is required.", nameof(levelId));
        }

        private static void ValidateTrackId(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId)) throw new ArgumentException("A progress track ID is required.", nameof(trackId));
        }

        private static void ValidateResult(int stars, long score)
        {
            if (stars < 0) throw new ArgumentOutOfRangeException(nameof(stars), "Stars cannot be negative.");
            if (score < 0) throw new ArgumentOutOfRangeException(nameof(score), "Score cannot be negative.");
        }

        private static int NextLevelNumber(int levelNumber) => levelNumber == int.MaxValue ? int.MaxValue : levelNumber + 1;
        private static int IncrementWithoutOverflow(int value) => value == int.MaxValue ? int.MaxValue : value + 1;

        private static int CompareRecords(LevelRecordData left, LevelRecordData right)
        {
            var numberComparison = left.LastKnownLevelNumber.CompareTo(right.LastKnownLevelNumber);
            return numberComparison != 0
                ? numberComparison
                : string.Compare(left.LevelId, right.LevelId, StringComparison.Ordinal);
        }

        private static int ComparePublicRecords(TTGLevelProgressRecord left, TTGLevelProgressRecord right)
        {
            var numberComparison = left.LastKnownLevelNumber.CompareTo(right.LastKnownLevelNumber);
            return numberComparison != 0
                ? numberComparison
                : string.Compare(left.LevelId, right.LevelId, StringComparison.Ordinal);
        }

        [Serializable]
        private sealed class ProgressSaveData
        {
            public int Version;
            public int HighestUnlockedLevel;
            public List<LevelRecordData> Levels;
        }

        [Serializable]
        private sealed class LevelRecordData
        {
            public string LevelId;
            public int LastKnownLevelNumber;
            public int AttemptCount;
            public int CompletionCount;
            public int BestStars;
            public long BestScore;
        }
    }
}
