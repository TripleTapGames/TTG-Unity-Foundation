using System;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public enum TTGLogCategory
    {
        Core,
        Privacy,
        Facebook,
        Firebase,
        Analytics,
        Ads,
        Singular,
        IAP,
        Build
    }

    public static class TTGLogger
    {
        public static bool DebugLoggingEnabled { get; set; }

        public static void Info(TTGLogCategory category, string message)
        {
            if (DebugLoggingEnabled) Debug.Log(Format(category, message));
        }

        public static void Warning(TTGLogCategory category, string message)
        {
            Debug.LogWarning(Format(category, message));
        }

        public static void Error(TTGLogCategory category, string message, Exception exception = null)
        {
            Debug.LogError(Format(category, exception == null ? message : message + " (" + exception.GetType().Name + ")"));
        }

        internal static string Redact(string value)
        {
            return string.IsNullOrEmpty(value) ? "<missing>" : "<redacted>";
        }

        private static string Format(TTGLogCategory category, string message)
        {
            return "[TTG:" + category + "] " + message;
        }
    }
}
