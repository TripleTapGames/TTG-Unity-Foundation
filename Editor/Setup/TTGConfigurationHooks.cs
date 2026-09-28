using System;

namespace TripleTapGames.Foundation.Editor
{
    // Optional editor assemblies register without introducing vendor dependencies here.
    public static class TTGConfigurationHooks
    {
        public static event Action<TTGProjectConfig> Applying;
        internal static void Apply(TTGProjectConfig config) => Applying?.Invoke(config);
    }
}
