using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TripleTapGames.Foundation
{
    [DisallowMultipleComponent]
    public sealed class TTGBootstrap : MonoBehaviour
    {
        [Header("Loading UI")]
        [SerializeField] private Image progressFill;
        [SerializeField] private Text statusText;
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 0.35f;
        [SerializeField] private bool waitForConsent = true;
        [Header("Next Scene")]
        [SerializeField] private bool loadNextScene = true;
        [SerializeField] private string nextSceneName = "Core";

        public TTGInitializationReport Report { get; private set; }

        private async UniTaskVoid Start()
        {
            var startedAt = Time.realtimeSinceStartup;
            TTGInitializer.OnProgressChanged += UpdateProgress;
            UpdateProgress(TTGInitializer.CurrentProgress);
            try
            {
                if (waitForConsent && HasEnabledConsentService())
                {
                    SetStatus("Waiting for consent...");
                    while (!TTGPrivacy.HasResolvedConsent)
                        await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken);
                }
                Report = await TTGInitializer.InitializeAsync(destroyCancellationToken);
                SetProgress(1f);
                if (!Report.Succeeded)
                {
                    SetStatus("Startup failed. Check the Console.");
                    Debug.LogError("[TTG:Startup] Initialization failed. Check configuration and service results.");
                    return;
                }
                Debug.Log($"[TTG:Startup] SDK initialization completed with status {Report.OverallStatus}.");

                var remaining = minimumDisplaySeconds - (Time.realtimeSinceStartup - startedAt);
                if (remaining > 0f) await UniTask.Delay(TimeSpan.FromSeconds(remaining), DelayType.Realtime,
                    PlayerLoopTiming.Update, destroyCancellationToken);
                if (!loadNextScene) { SetStatus("Ready"); return; }
                if (string.IsNullOrWhiteSpace(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
                {
                    SetStatus("Core scene is not in Build Settings.");
                    Debug.LogError("[TTG:Startup] The configured next scene is missing from Build Settings.");
                    return;
                }
                SetStatus("Starting game...");
                await SceneManager.LoadSceneAsync(nextSceneName).ToUniTask(cancellationToken: destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                SetStatus("Startup failed. Check the Console.");
                Debug.LogException(exception);
            }
            finally { TTGInitializer.OnProgressChanged -= UpdateProgress; }
        }

        private static bool HasEnabledConsentService()
        {
            var config = TTGConfigProvider.LoadProjectConfig();
            if (config == null) return false;
            foreach (var service in TTGServiceRegistry.Global.Services)
            {
                if (service.RequiresConsent && service.IsEnabled(config)) return true;
            }
            return false;
        }

        private void UpdateProgress(TTGInitializationProgress progress)
        {
            SetProgress(progress.NormalizedProgress);
            SetStatus(progress.TotalServices == 0 || string.IsNullOrEmpty(progress.ServiceName)
                ? "Preparing services..."
                : $"Loading {progress.ServiceName} ({progress.CompletedServices}/{progress.TotalServices})");
        }

        private void SetProgress(float value)
        {
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(value);
        }

        private void SetStatus(string value)
        {
            if (statusText != null) statusText.text = value;
        }
    }
}
