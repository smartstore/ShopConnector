using System;
using System.Collections.Generic;

namespace SmartStore.ShopConnector.Services
{
    public class ShopConnectorImportState
    {
        public bool ImportCategories { get; set; }
        public bool ImportAll { get; set; }
        public string ImportFile { get; set; }
        public int TaxCategoryId { get; set; }
        public bool LimitedToStores { get; set; }
        public List<int> SelectedStoreIds { get; set; }
        public Dictionary<int, int> SelectedProductIds { get; set; }
        public bool UpdateExistingProducts { get; set; }
        public bool UpdateExistingCategories { get; set; }
        public List<string> IgnoreEntityNames { get; set; }
        public bool DeleteImportFile { get; set; }
        public int EventPublishEntityCount { get; set; }
        public bool Publish { get; set; }
        public bool? DisableBuyButton { get; set; }
        public bool? DisableWishlistButton { get; set; }

        public bool IsSelected(int productId)
        {
            return ImportAll || (SelectedProductIds != null && SelectedProductIds.ContainsKey(productId));
        }
    }


    public class ShopConnectorProcessingInfo
    {
        public int TotalRecords { get; set; }
        public int TotalProcessed { get; set; }
        public int Success { get; set; }
        public int Failure { get; set; }
        public int Skipped { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int RecordsCount { get; set; }
        public string Description { get; set; }

        public string Content { get; set; }

        public double ProcessedPercent
        {
            get
            {
                if (TotalRecords == 0)
                    return 0;

                return ((double)TotalProcessed / (double)TotalRecords) * 100;
            }
        }

        public void Reset(string description, int totalRecords)
        {
            Description = description;
            TotalRecords = totalRecords;
            RecordsCount = TotalProcessed = Success = Failure = Skipped = Added = Updated = 0;
        }

        public string Format(string template, string description)
        {
            return template.FormatInvariant(
                description.NaIfEmpty(),
                TotalProcessed.ToString("N0"), TotalRecords.ToString("N0"),
                Success.ToString("N0"), Failure.ToString("N0"), Skipped.ToString("N0"),
                Added.ToString("N0"), Updated.ToString("N0"));
        }

        public override string ToString()
        {
            if (!Content.Contains("{"))
                return Content;

            var str = Content.FormatInvariant(
                Description.NaIfEmpty(),
                (int)Math.Round(ProcessedPercent),
                TotalProcessed.ToString("N0"), TotalRecords.ToString("N0"),
                Success.ToString("N0"), Failure.ToString("N0"), Skipped.ToString("N0"),
                Added.ToString("N0"), Updated.ToString("N0")
            );
            return str;
        }
    }
}