using System.Data.Entity.Migrations;
using System.Web.Routing;
using SmartStore.Core.Domain.DataExchange;
using SmartStore.Core.Plugins;
using SmartStore.Services;
using SmartStore.Services.DataExchange.Export;
using SmartStore.ShopConnector.Data.Migrations;
using SmartStore.ShopConnector.ExportProvider;
using SmartStore.ShopConnector.Services;
using SmartStore.Utilities;

namespace SmartStore.ShopConnector
{
    public class ShopConnectorPlugin : BasePlugin, IConfigurable
    {
        private readonly ICommonServices _services;
        private readonly IExportProfileService _exportProfileService;

        public ShopConnectorPlugin(
            ICommonServices services,
            IExportProfileService exportProfileService)
        {
            _services = services;
            _exportProfileService = exportProfileService;
        }

        private void InsertProfile(string providerSystemName, bool insert)
        {
            var profile = _exportProfileService.GetSystemExportProfile(providerSystemName);

            if (insert)
            {
                if (profile == null)
                {
                    profile = _exportProfileService.InsertExportProfile(
                        providerSystemName,
                        _services.Localization.GetResource("Plugins.FriendlyName.SmartStore.ShopConnector"),
                        "XML",
                        ExportFeatures.None,
                        true);
                }
            }
            else
            {
                if (profile != null)
                {
                    _exportProfileService.DeleteExportProfile(profile, true);
                }
            }
        }

        public static string SystemName => "SmartStore.ShopConnector";

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "ShopConnector";
            routeValues = new RouteValueDictionary { { "area", SystemName } };
        }

        public override void Install()
        {
            _services.Settings.SaveSetting(new ShopConnectorSettings());
            _services.Localization.ImportPluginResourcesFromXml(PluginDescriptor);

            InsertProfile(ShopConnectorProductXmlExportProvider.SystemName, true);
            InsertProfile(ShopConnectorCategoryXmlExportProvider.SystemName, true);

            base.Install();

            ConnectionCache.Remove();
        }

        public override void Uninstall()
        {
            ConnectionCache.Remove();

            _services.Settings.DeleteSetting<ShopConnectorSettings>();
            _services.Localization.DeleteLocaleStringResources(PluginDescriptor.ResourceRootKey);

            InsertProfile(ShopConnectorProductXmlExportProvider.SystemName, false);
            InsertProfile(ShopConnectorCategoryXmlExportProvider.SystemName, false);

            var migrator = new DbMigrator(new Configuration());
            migrator.Update(DbMigrator.InitialDatabase);

            FileSystemHelper.ClearDirectory(ShopConnectorFileSystem.GetDirectory(null), true);

            base.Uninstall();
        }
    }
}
