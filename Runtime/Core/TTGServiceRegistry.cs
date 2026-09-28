using System;
using System.Collections.Generic;

namespace TripleTapGames.Foundation
{
    public sealed class TTGServiceRegistry
    {
        private readonly List<ITTGService> services = new List<ITTGService>();

        public static TTGServiceRegistry Global { get; } = new TTGServiceRegistry();
        public IReadOnlyList<ITTGService> Services => services;

        public void Register(ITTGService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            for (var i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceName == service.ServiceName) services.RemoveAt(i--);
            }
            services.Add(service);
            services.Sort((left, right) => left.InitializationOrder.CompareTo(right.InitializationOrder));
        }

        public T Get<T>() where T : class, ITTGService
        {
            for (var i = 0; i < services.Count; i++)
            {
                if (services[i] is T service) return service;
            }
            return null;
        }

        public void ShutdownAll()
        {
            for (var i = services.Count - 1; i >= 0; i--) services[i].Shutdown();
        }

        internal void Clear()
        {
            ShutdownAll();
            services.Clear();
        }
    }
}
