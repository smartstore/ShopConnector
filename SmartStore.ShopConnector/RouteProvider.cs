using System.Web.Mvc;
using System.Web.Routing;
using SmartStore.Web.Framework.Routing;

namespace SmartStore.ShopConnector
{
    public partial class RouteProvider : IRouteProvider
    {
        public int Priority => 0;

        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("SmartStore.ShopConnector",
                    "Plugins/SmartStore.ShopConnector/{controller}/{action}",
                    new { controller = "ShopConnector" },
                    new[] { "SmartStore.ShopConnector.Controllers" }
            )
            .DataTokens["area"] = ShopConnectorPlugin.SystemName;
        }
    }
}
