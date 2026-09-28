using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace TripleTapGames.Foundation.Tests
{
    public sealed class TTGInitializationTests
    {
        private TTGProjectConfig config;

        [SetUp]
        public void SetUp()
        {
            TTGServiceRegistry.Global.Clear();
            TTGInitializer.ResetState();
            TTGPrivacy.ResetForTests();
            config = ScriptableObject.CreateInstance<TTGProjectConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            TTGServiceRegistry.Global.Clear();
            TTGInitializer.ResetState();
            Object.DestroyImmediate(config);
        }

        [Test]
        public void RepeatedInitializationUsesCompletedResult()
        {
            var registry = new TTGServiceRegistry();
            var service = new MockService(TTGInitializationStatus.Success, required: true);
            registry.Register(service);
            var first = TTGInitializer.InitializeAsync(config, null, registry).GetAwaiter().GetResult();
            var second = TTGInitializer.InitializeAsync(config, null, registry).GetAwaiter().GetResult();
            Assert.That(first, Is.SameAs(second));
            Assert.That(service.InitializationCount, Is.EqualTo(1));
        }

        [Test]
        public void OptionalFailureProducesWarningAndContinues()
        {
            var registry = new TTGServiceRegistry();
            registry.Register(new MockService(TTGInitializationStatus.Failure, required: false, order: 1));
            var later = new MockService(TTGInitializationStatus.Success, required: false, order: 2);
            registry.Register(later);
            var report = TTGInitializer.InitializeAsync(config, null, registry).GetAwaiter().GetResult();
            Assert.That(report.OverallStatus, Is.EqualTo(TTGInitializationStatus.Warning));
            Assert.That(later.InitializationCount, Is.EqualTo(1));
        }

        [Test]
        public void RequiredFailureFailsReport()
        {
            var registry = new TTGServiceRegistry();
            registry.Register(new MockService(TTGInitializationStatus.Failure, required: true, order: 1));
            registry.Register(new MockService(TTGInitializationStatus.Failure, required: false, order: 2));
            var report = TTGInitializer.InitializeAsync(config, null, registry).GetAwaiter().GetResult();
            Assert.That(report.OverallStatus, Is.EqualTo(TTGInitializationStatus.Failure));
        }

        [Test]
        public void ProgressReportsEachEnabledServiceInOrder()
        {
            var registry = new TTGServiceRegistry();
            registry.Register(new MockService(TTGInitializationStatus.Success, required: false, order: 1));
            registry.Register(new MockService(TTGInitializationStatus.Warning, required: false, order: 2));
            var progress = new List<TTGInitializationProgress>();
            void Capture(TTGInitializationProgress value) => progress.Add(value);
            TTGInitializer.OnProgressChanged += Capture;
            try { TTGInitializer.InitializeAsync(config, null, registry).GetAwaiter().GetResult(); }
            finally { TTGInitializer.OnProgressChanged -= Capture; }

            Assert.That(progress, Has.Count.EqualTo(3));
            Assert.That(progress[0].NormalizedProgress, Is.Zero);
            Assert.That(progress[1].CompletedServices, Is.EqualTo(1));
            Assert.That(progress[2].CompletedServices, Is.EqualTo(2));
            Assert.That(progress[2].NormalizedProgress, Is.EqualTo(1f));
            Assert.That(progress[2].ServiceName, Is.EqualTo("Mock2"));
        }

        [Test]
        public void ConsentIsStoredBeforeInitializationWithoutCallingVendorAdapter()
        {
            var service = new MockService(TTGInitializationStatus.Success, required: false, requiresConsent: true);
            TTGServiceRegistry.Global.Register(service);
            var consent = new TTGConsentState
            {
                Analytics = TTGConsentStatus.Granted,
                Advertising = TTGConsentStatus.Granted
            };

            TTGPrivacy.SetConsentAsync(consent).GetAwaiter().GetResult();
            Assert.That(TTGPrivacy.HasResolvedConsent, Is.True);
            Assert.That(service.ConsentApplyCount, Is.Zero);

            TTGInitializer.InitializeAsync(config, null, TTGServiceRegistry.Global).GetAwaiter().GetResult();
            TTGPrivacy.SetConsentAsync(consent).GetAwaiter().GetResult();
            Assert.That(service.ConsentApplyCount, Is.EqualTo(1));
        }

        private sealed class MockService : ITTGService, ITTGConsentAdapter
        {
            private readonly TTGInitializationStatus status;
            private readonly bool required;
            public string ServiceName => "Mock" + InitializationOrder;
            public int InitializationOrder { get; }
            public bool IsInitialized { get; private set; }
            public bool RequiresConsent { get; }
            public int InitializationCount { get; private set; }
            public int ConsentApplyCount { get; private set; }

            public MockService(TTGInitializationStatus status, bool required, int order = 0, bool requiresConsent = false)
            {
                this.status = status;
                this.required = required;
                InitializationOrder = order;
                RequiresConsent = requiresConsent;
            }
            public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
            {
                ConsentApplyCount++;
                return UniTask.CompletedTask;
            }

            public bool IsEnabled(TTGProjectConfig projectConfig) => true;
            public bool IsRequired(TTGProjectConfig projectConfig) => required;
            public UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
            {
                InitializationCount++;
                IsInitialized = status == TTGInitializationStatus.Success;
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, status));
            }
            public void Shutdown() => IsInitialized = false;
        }
    }
}
