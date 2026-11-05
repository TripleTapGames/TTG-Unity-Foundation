using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;

// Ensure the linker discovers runtime registration even without scene references.
[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]

namespace TripleTapGames.Foundation.Adapters.IAP
{
    internal sealed class UnityIAPService : ITTGService, ITTGIAPProvider, ITTGIAPOwnershipProvider, IDetailedStoreListener
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
            var error = TTGIAPCatalog.Validate(context.ProjectConfig.IAP.Products);
            if (error != null) return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, error));
            initialization = new UniTaskCompletionSource<TTGInitializationResult>();
            InitializeStoreAsync(context, cancellationToken).Forget();
            return initialization.Task;
        }

        private async UniTask InitializeStoreAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            var completion = initialization;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                cancellationToken.ThrowIfCancellationRequested();
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                foreach (var product in context.ProjectConfig.IAP.Products)
                    builder.AddProduct(product.ProductId, ConvertType(product.ProductType));
                using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
                {
                    UnityPurchasing.Initialize(this, builder);
                    await completion.Task;
                }
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(cancellationToken); }
            catch (Exception exception)
            {
                completion.TrySetResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure,
                    "Store initialization threw an exception: " + exception.Message, exception));
            }
        }

        public void OnInitialized(IStoreController storeController, IExtensionProvider extensionProvider)
        {
            controller = storeController;
            extensions = extensionProvider;
            IsInitialized = true;
            TTGIAP.Configure(this);
            initialization?.TrySetResult(TTGInitializationResult.Successful(ServiceName));
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            initialization?.TrySetResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, error + ": " + message));
        }

        public void Purchase(string productId, Action<TTGPurchaseResult> callback)
        {
            if (!IsInitialized) { callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.NotInitialized, productId)); return; }
            if (pending.ContainsKey(productId)) { callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.Pending, productId, message: "A purchase for this product is already in progress.")); return; }
            var product = controller.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.ProductUnavailable, productId, message: "Store did not return an available product with this exact ID."));
                return;
            }
            pending[productId] = callback;
            try { controller.InitiatePurchase(product); }
            catch (Exception exception)
            {
                pending.Remove(productId);
                callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.Failed, productId, message: exception.Message));
            }
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
            var result = new TTGPurchaseResult(TTGPurchaseStatus.Succeeded, product.definition.id,
                product.transactionID, product.metadata.localizedPrice, product.metadata.isoCurrencyCode);
            // Also reports startup/restored transactions that have no pending purchase callback.
            TTGIAP.NotifyPurchaseReceived(result);
            if (pending.TryGetValue(product.definition.id, out var callback))
            {
                pending.Remove(product.definition.id);
                try { callback?.Invoke(result); }
                catch (Exception exception) { Debug.LogException(exception); }
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

        public bool? HasReceipt(string productId)
        {
            if (!IsInitialized || string.IsNullOrWhiteSpace(productId)) return null;
            var product = controller.products.WithID(productId);
            return product == null ? (bool?)null : product.hasReceipt;
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
