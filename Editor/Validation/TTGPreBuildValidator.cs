using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TripleTapGames.Foundation.Editor
{
    internal sealed class TTGPreBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var results = TTGSetupValidator.Validate(report.summary.platform);
            foreach (var warning in results.Where(item => item.Severity == TTGValidationSeverity.Warning))
                UnityEngine.Debug.LogWarning("[TTG:" + warning.Category + "] " + warning.Message);
            var errors = results.Where(item => item.Severity == TTGValidationSeverity.Error).ToArray();
            if (errors.Length == 0) return;
            var message = "TTG Build Validation Failed\n\n" + string.Join("\n", errors.Select(error => "[" + error.Category + "] " + error.Message));
            throw new BuildFailedException(message);
        }
    }
}
