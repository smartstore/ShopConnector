using System;
using System.Collections.Generic;
using System.Web.Mvc;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Models
{
    public class ProductDataModel : EntityModelBase
    {
        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.FetchFromDate")]
        public DateTime? FetchFromDate { get; set; }
        public string FetchFrom { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.DataFileName")]
        public string DataFileName { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.FilterManufacturerIds")]
        public int[] FilterManufacturerIds { get; set; }
        public MultiSelectList AvailableManufacturers { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.FilterCategoryId")]
        public string FilterCategoryId { get; set; }
        public List<SelectListItem> AvailableCategories { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.FilterCatalogId")]
        public string FilterCatalogId { get; set; }
    }
}
