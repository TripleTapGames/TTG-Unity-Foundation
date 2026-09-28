using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace TripleTapGames.Foundation
{
    public enum TTGInitializationStatus
    {
        Success,
        Warning,
        Failure,
        Skipped,
        Deferred
    }

    public sealed class TTGInitializationResult
    {
        public string ServiceName { get; }
        public TTGInitializationStatus Status { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public TTGInitializationResult(string serviceName, TTGInitializationStatus status, string message = null, Exception exception = null)
        {
            ServiceName = serviceName;
            Status = status;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public static TTGInitializationResult Successful(string serviceName, string message = null)
        {
            return new TTGInitializationResult(serviceName, TTGInitializationStatus.Success, message);
        }
    }

    public sealed class TTGInitializationReport
    {
        private readonly List<TTGInitializationResult> results;

        public IReadOnlyList<TTGInitializationResult> Results => results;
        public TTGInitializationStatus OverallStatus { get; internal set; }
        public bool Succeeded => OverallStatus != TTGInitializationStatus.Failure;

        internal TTGInitializationReport(List<TTGInitializationResult> results, TTGInitializationStatus status)
        {
            this.results = results;
            OverallStatus = status;
        }
    }

    public readonly struct TTGInitializationProgress
    {
        public int CompletedServices { get; }
        public int TotalServices { get; }
        public float NormalizedProgress => TotalServices == 0 ? 0f : (float)CompletedServices / TotalServices;
        public string ServiceName { get; }
        public TTGInitializationStatus Status { get; }

        internal TTGInitializationProgress(int completedServices, int totalServices, string serviceName,
            TTGInitializationStatus status)
        {
            CompletedServices = completedServices;
            TotalServices = totalServices;
            ServiceName = serviceName ?? string.Empty;
            Status = status;
        }
    }

    public sealed class TTGServiceContext
    {
        public TTGProjectConfig ProjectConfig { get; }
        public TTAdsConfig AdsConfig { get; }

        public TTGServiceContext(TTGProjectConfig projectConfig, TTAdsConfig adsConfig)
        {
            ProjectConfig = projectConfig;
            AdsConfig = adsConfig;
        }
    }

    public interface ITTGService
    {
        string ServiceName { get; }
        int InitializationOrder { get; }
        bool IsInitialized { get; }
        bool RequiresConsent { get; }
        bool IsEnabled(TTGProjectConfig config);
        bool IsRequired(TTGProjectConfig config);
        UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken);
        void Shutdown();
    }
}
