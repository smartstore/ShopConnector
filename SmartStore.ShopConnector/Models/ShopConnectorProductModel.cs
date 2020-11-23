using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Models
{
    public class ShopConnectorProductModel : ModelBase
    {
        public int ProductId { get; set; }
    }

    public class SkuMappingModel : EntityModelBase
    {
        [SmartResourceDisplayName("Domain")]
        public string Domain { get; set; }

        [SmartResourceDisplayName("Admin.Catalog.Products.Fields.Sku")]
        public string Sku { get; set; }
    }
}