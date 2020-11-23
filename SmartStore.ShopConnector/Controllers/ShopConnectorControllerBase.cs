using System.Net;
using System.Web.Mvc;
using SmartStore.Core;
using SmartStore.ShopConnector.Models;
using SmartStore.Web.Framework.Controllers;

namespace SmartStore.ShopConnector.Controllers
{
    public abstract class ShopConnectorControllerBase : PluginControllerBase
    {
        public ActionResult ShopConnectorError(HttpStatusCode code, string message, string description = null)
        {
            Response.StatusCode = (int)code;

            var model = new OperationResultModel(message, true)
            {
                Description = description
            };

            return Content(XmlHelper.Serialize(model));
        }
    }
}