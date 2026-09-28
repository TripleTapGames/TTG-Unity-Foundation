using Cysharp.Threading.Tasks;
using TripleTapGames.Foundation;
using UnityEngine;

public sealed class TTGBootstrapExample : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        var report = await TTGInitializer.InitializeAsync(destroyCancellationToken);
        if (!report.Succeeded) Debug.LogError("TTG initialization failed.");
    }
}
