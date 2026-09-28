using System;

namespace TripleTapGames.Foundation
{
    public enum TTGPurchaseStatus
    {
        Succeeded,
        Cancelled,
        Pending,
        NotInitialized,
        ProductUnavailable,
        Failed
    }

    public sealed class TTGPurchaseResult
    {
        public TTGPurchaseStatus Status { get; }
        public string ProductId { get; }
        public string TransactionId { get; }
        public decimal LocalizedPrice { get; }
        public string Currency { get; }
        public string Message { get; }

        public TTGPurchaseResult(TTGPurchaseStatus status, string productId, string transactionId = null,
            decimal localizedPrice = 0, string currency = null, string message = null)
        {
            Status = status;
            ProductId = productId;
            TransactionId = transactionId ?? string.Empty;
            LocalizedPrice = localizedPrice;
            Currency = currency ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public interface ITTGIAPProvider
    {
        bool IsInitialized { get; }
        void Purchase(string productId, Action<TTGPurchaseResult> callback);
        void RestorePurchases(Action<bool> callback);
    }

    public static class TTGIAP
    {
        private static ITTGIAPProvider provider;
        public static bool IsInitialized => provider != null && provider.IsInitialized;

        internal static void Configure(ITTGIAPProvider iapProvider) => provider = iapProvider;

        public static void Purchase(string productId, Action<TTGPurchaseResult> callback)
        {
            if (!IsInitialized)
            {
                callback?.Invoke(new TTGPurchaseResult(TTGPurchaseStatus.NotInitialized, productId));
                return;
            }

            var once = new TTGAds.Once<TTGPurchaseResult>(result =>
            {
                if (result.Status == TTGPurchaseStatus.Succeeded)
                    TTGAnalytics.Purchase(result.ProductId, result.LocalizedPrice, result.Currency, result.TransactionId);
                callback?.Invoke(result);
            });
            provider.Purchase(productId, once.Invoke);
        }

        public static void RestorePurchases(Action<bool> callback = null)
        {
            if (!IsInitialized) { callback?.Invoke(false); return; }
            var once = new TTGAds.Once<bool>(result => callback?.Invoke(result));
            provider.RestorePurchases(once.Invoke);
        }
    }
}
