using SmartStore.Core.Configuration;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector
{
    public class ShopConnectorSettings : ISettings
    {
        public ShopConnectorSettings()
        {
            IsImportEnabled = true;
            LogUnauthorized = true;
            ValidMinutePeriod = ShopConnectorCore.DefaultTimePeriodMinutes;
            ImageDownloadTimeout = 10;
            MaxCategoriesToFilter = 10000;
            MaxHoursToExport = 12;
            MaxFileSizeForPreview = 400;
        }

        public bool IsImportEnabled { get; set; }
        public bool IsExportEnabled { get; set; }
        public int ValidMinutePeriod { get; set; }
        public bool LogUnauthorized { get; set; }
        public int ImageDownloadTimeout { get; set; }
        public bool EnableSkuMapping { get; set; }
        public bool EnableSkuImport { get; set; }
        public bool IncludeHiddenProducts { get; set; }
        public int MaxCategoriesToFilter { get; set; }
        public int MaxHoursToExport { get; set; }

        /// <summary>
        /// In megabyte.
        /// </summary>
        public int MaxFileSizeForPreview { get; set; }

        /// <summary>
        /// Hidden setting to ignore entities during import. Comma separated list of entity names.
        /// </summary>
        public string IgnoreEntityNames { get; set; }
    }
}
