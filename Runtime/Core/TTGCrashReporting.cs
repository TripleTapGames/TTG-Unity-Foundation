using System;

namespace TripleTapGames.Foundation
{
    public interface ITTGCrashReporter
    {
        bool IsInitialized { get; }
        void RecordException(Exception exception);
    }

    public static class TTGCrashReporting
    {
        private static ITTGCrashReporter reporter;
        internal static void Configure(ITTGCrashReporter crashReporter) => reporter = crashReporter;

        public static void RecordException(Exception exception)
        {
            if (exception == null) return;
            if (reporter != null && reporter.IsInitialized) reporter.RecordException(exception);
            else TTGLogger.Error(TTGLogCategory.Core, "Non-fatal exception recorded before Crashlytics initialization.", exception);
        }
    }
}
