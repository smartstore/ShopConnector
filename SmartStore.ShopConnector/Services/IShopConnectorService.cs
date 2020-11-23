using System;
using System.Collections.Generic;
using System.Threading;
using SmartStore.Core;
using SmartStore.Services.DataExchange.Export;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Models;

namespace SmartStore.ShopConnector.Services
{
    public partial interface IShopConnectorService
    {
        DateTime? ConvertDateTime(DateTime? dt, bool toUserTime);

        void SetupConfiguration(ConfigurationModel model);
        void SetupConnectionModel(ConnectionModel model, int id, bool isForExport);
        void SetupProductImportModel(ProductImportModel model, int id);
        void SetupProductFileSelectModel(ProductFileSelectModel model, int id);
        void SetupProductImportCompletedModel(ProductImportCompletedModel model);

        AboutModel CreateAboutModel(CachedConnection connection);

        List<ProductImportItemModel> GetProductImportItems(string importFile, int pageIndex, out int totalItems);

        ShopConnectorConnectionRecord InsertConnection(ConnectionModel model);
        bool UpdateConnection(ConnectionModel model, bool isForExport);
        void DeleteConnection(int id);
        IPagedList<ConnectionModel> GetConnections(bool isForExport, int pageIndex, int pageSize);

        void InsertSkuMapping(ShopConnectorSkuMapping mapping);
        void UpdateSkuMapping(ShopConnectorSkuMapping mapping);
        void DeleteSkuMapping(ShopConnectorSkuMapping mapping);
        ShopConnectorSkuMapping GetSkuMappingsById(int id);
        List<ShopConnectorSkuMapping> GetSkuMappingsByProductIds(string domain, params int[] productIds);

        bool SendRequest(ShopConnectorRequestContext context, int id);

        DataExportResult Export(ShopConnectorExportContext context, CancellationToken token, string providerSystemName);

        void Import(ProductImportModel model);
    }
}
