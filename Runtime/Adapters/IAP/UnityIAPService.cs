using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace TripleTapGames.Foundation.Adapters.IAP
{
    internal sealed class UnityIAPService : ITTGService, ITTGIAPProvider, IDetailedStoreListener
    {
        private IStoreController controller;
        private IExtensionProvider extensions;
        private UniTaskCompletionSource<TTGInitializationResult> initialization;
        private readonly Dictionary<string, Action<TTGPurchaseResult>> pending = new Dictionary<string, Action<TTGPurchaseResult>>();

        public string ServiceName => "Unity IAP";
        public int InitializationOrder => 600;
        public bool IsInitialized { get; private set; }
        public bool RequiresConsent => false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => TTGServiceRegistry.Global.Register(new UnityIAPService());

        public bool IsEnabled(TTGProjectConfig config) => config.IAP.Enabled;
        public bool IsRequired(TTGProjectConfig config) => config.IAP.Required;

        public UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            if (IsInitialized) return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
            if (initialization != null) return initialization.Task;
            initialization = new UniTaskCompletionSource<TTGInitializationResult>();
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var product in context.ProjectConfig.IAP.Products)
            {
                if (product == null || string.IsNullOrWhiteSpace(product.ProductId)) continue;
                builder.AddProduct(product.ProductId, ConvertType(product.ProductType));
            }
            UnityPurchasing.Initialize(this, builder);
            cancellationToken.Register(() => initialization.TrySetCanceled(cancellationToken));
            return initialization.Task;
        }

        public void OnInitialized(IStoreController storeController, IExtensionProvider extensionProvider)
        {
            controller = storeController;
            extensions = extensionProvider;
            IsInitialized = true;
            TTGIAP.Configure(this);
            initialization.TrySetResult(TTGInitializationResult.Successful(ServiceName));
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            initialization.TrySetResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, error + ": " + message));
        }

        public void Purchase(string productId, Action<TTGPurchaseResult> callback)
        {
            var product = controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.ProductUnavailable, productId));
                return;
            }
            pending[productId] = callback;
            controller.InitiatePurchase(product);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
            if (pending.TryGetValue(product.definition.id, out var callback))
            {
                pending.Remove(product.definition.id);
                callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.Succeeded, product.definition.id,
                    product.transactionID, product.metadata.localizedPrice, product.metadata.isoCurrencyCode));
            }
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            CompleteFailure(product, failureReason, failureReason.ToString());
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            CompleteFailure(product, failureDescription.reason, failureDescription.message);
        }

        private void CompleteFailure(Product product, PurchaseFailureReason reason, string message)
        {
            if (!pending.TryGetValue(product.definition.id, out var callback)) return;
            pending.Remove(product.definition.id);
            var status = reason == PurchaseFailureReason.UserCancelled ? TTGPurchaseStatus.Cancelled : TTGPurchaseStatus.Failed;
            callback?.Invoke(new TTGPurchaseResult(status, product.definition.id, message: message));
        }

        public void RestorePurchases(Action<bool> callback)
        {
#if UNITY_IOS || UNITY_STANDALONE_OSX
            extensions.GetExtension<IAppleExtensions>().RestoreTransactions((success, message) => callback?.Invoke(success));
#else
            callback?.Invoke(true);
#endif
        }

        public void Shutdown()
        {
            pending.Clear();
            IsInitialized = false;
            controller = null;
            extensions = null;
            initialization = null;
        }

        private static ProductType ConvertType(TTGIAPProductType type)
        {
            if (type == TTGIAPProductType.NonConsumable) return ProductType.NonConsumable;
            if (type == TTGIAPProductType.Subscription) return ProductType.Subscription;
            return ProductType.Consumable;
        }
    }
}
