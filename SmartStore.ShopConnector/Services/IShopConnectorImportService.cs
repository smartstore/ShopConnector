namespace SmartStore.ShopConnector.Services
{
    public partial interface IShopConnectorImportService
    {
        void StartProductImport(ShopConnectorImportState state);
    }
}