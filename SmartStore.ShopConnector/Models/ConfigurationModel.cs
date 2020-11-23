using System.Collections.Generic;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Models
{
    public class ConfigurationModel : ModelBase
    {
        public int GridPageSize { get; set; }
        public string SelectedTab { get; set; }
        public bool LogFileExists { get; set; }

        public Dictionary<string, string> Strings { get; set; }
        public Dictionary<string, string> ImportUrls { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.IsImportEnabled")]
        public bool IsImportEnabled { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.IsExportEnabled")]
        public bool IsExportEnabled { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.ValidMinutePeriod")]
        public int ValidMinutePeriod { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.LogUnauthorized")]
        public bool LogUnauthorized { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.ImageDownloadTimeout")]
        public int ImageDownloadTimeout { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.EnableSkuMapping")]
        public bool EnableSkuMapping { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.EnableSkuImport")]
        public bool EnableSkuImport { get; set; }

        public void Copy(ShopConnectorSettings settings, bool fromSettings)
        {
            if (fromSettings)
            {
                IsImportEnabled = settings.IsImportEnabled;
                IsExportEnabled = settings.IsExportEnabled;
                ValidMinutePeriod = settings.ValidMinutePeriod;
                LogUnauthorized = settings.LogUnauthorized;
                ImageDownloadTimeout = settings.ImageDownloadTimeout;
                EnableSkuMapping = settings.EnableSkuMapping;
                EnableSkuImport = settings.EnableSkuImport;
            }
            else
            {
                settings.IsImportEnabled = IsImportEnabled;
                settings.IsExportEnabled = IsExportEnabled;
                settings.ValidMinutePeriod = ValidMinutePeriod;
                settings.LogUnauthorized = LogUnauthorized;
                settings.ImageDownloadTimeout = ImageDownloadTimeout;
                settings.EnableSkuMapping = EnableSkuMapping;
                settings.EnableSkuImport = EnableSkuImport;
            }
        }
    }
}
