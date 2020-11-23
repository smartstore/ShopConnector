using System.Collections.Generic;
using System.Web.Mvc;
using SmartStore.Web.Framework;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Models
{
    public class ProductFileSelectModel : EntityModelBase
    {
        [SmartResourceDisplayName("Plugins.SmartStore.ShopConnector.ImportFile")]
        public string ImportFile { get; set; }
        public List<SelectListItem> AvailableImportFiles { get; set; }
    }
}
