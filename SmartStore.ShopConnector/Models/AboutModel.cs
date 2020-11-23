using System;
using System.Collections.Generic;
using System.Web.Mvc;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Models
{
    public class AboutModel : ModelBase
    {
        [SmartResourceDisplayName("Admin.System.SystemInfo.AppVersion")]
        public string AppVersion { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.StoreDateTimeUtc")]
        public DateTime UtcTime { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.ConnectorVersion")]
        public string ConnectorVersion { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.StoreName")]
        public string StoreName { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.StoreUrl")]
        public string StoreUrl { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.StoreCount")]
        public int StoreCount { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.CompanyName")]
        public string CompanyName { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.UpdatedProductsCount")]
        public string UpdatedProductsCount { get; set; }

        public string StoreLogoUrl { get; set; }

        public List<SelectListItem> AvailableManufacturers { get; set; }
        public List<SelectListItem> AvailableCategories { get; set; }
    }
}
