using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Models
{
    public class ProductImportModel : EntityModelBase
    {
        public ProductImportModel()
        {
            ImportCategories = true;
            Publish = true;
            UpdateExistingProducts = true;
            UpdateExistingCategories = true;
            DeleteImportFile = true;

#if DEBUG
            DeleteImportFile = false;
#endif
        }

        public int GridPageSize { get; set; }
        public string SelectedProductIds { get; set; }
        public bool ImportAll { get; set; }
        public string FileTooLargeForPreviewWarning { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.ImportCategories")]
        public bool ImportCategories { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.ImportFile")]
        public string ImportFile { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.TaxCategoryId")]
        public int TaxCategoryId { get; set; }
        public List<SelectListItem> AvailableTaxCategories { get; set; }

        [UIHint("Stores"), AdditionalMetadata("multiple", true)]
        [SmartResourceDisplayName("Admin.Common.Store.LimitedTo")]
        public int[] SelectedStoreIds { get; set; }
        [SmartResourceDisplayName("Admin.Common.Store.LimitedTo")]
        public bool LimitedToStores { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.UpdateExistingProducts")]
        public bool UpdateExistingProducts { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.UpdateExistingCategories")]
        public bool UpdateExistingCategories { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.DeleteImportFile")]
        public bool DeleteImportFile { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.Publish")]
        public bool Publish { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.DisableBuyButton")]
        public bool? DisableBuyButton { get; set; }

        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.DisableWishlistButton")]
        public bool? DisableWishlistButton { get; set; }
    }

    public class ProductImportItemModel : EntityModelBase
    {
        [SmartResourceDisplayName("Admin.Catalog.Products.Fields.ProductType")]
        public int ProductTypeId { get; set; }
        public ProductType ProductType { get; set; }

        [SmartResourceDisplayName("Admin.Catalog.Products.Fields.ProductType")]
        public string ProductTypeName { get; set; }

        public string ProductTypeLabelHint
        {
            get
            {
                switch (ProductType)
                {
                    case ProductType.GroupedProduct:
                        return "success";
                    case ProductType.BundledProduct:
                        return "info";
                    default:
                        return "secondary d-none";
                }
            }
        }

        [SmartResourceDisplayName("Admin.Catalog.Products.Fields.Name")]
        public string Name { get; set; }

        [SmartResourceDisplayName("Admin.Catalog.Products.Fields.Sku")]
        public string Sku { get; set; }

        [SmartResourceDisplayName("Manufacturers")]
        public string FormattedManufacturers => string.Join("<br />", Manufacturers);
        public List<string> Manufacturers { get; set; }

        [SmartResourceDisplayName("Admin.Catalog.Categories")]
        public string FormattedCategories => string.Join("<br />", Categories);
        public List<string> Categories { get; set; }
    }
}
