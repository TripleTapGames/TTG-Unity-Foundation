using System;
using System.Collections.Generic;

namespace TripleTapGames.Foundation
{
    public static class TTGIAPCatalog
    {
        public static string Validate(IReadOnlyList<TTGIAPProduct> products)
        {
            if (products == null || products.Count == 0) return "No IAP products are configured.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in products)
            {
                if (product == null || string.IsNullOrWhiteSpace(product.ProductId)) return "Every IAP product requires a product ID.";
                if (product.ProductId != product.ProductId.Trim()) return "Product IDs cannot have leading or trailing whitespace: '" + product.ProductId + "'.";
                if (!ids.Add(product.ProductId)) return "Duplicate IAP product ID: " + product.ProductId;
                if (!Enum.IsDefined(typeof(TTGIAPProductType), product.ProductType)) return "Invalid product type: " + product.ProductId;
            }
            return null;
        }
    }
}
