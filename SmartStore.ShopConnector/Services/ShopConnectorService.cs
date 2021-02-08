using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Xml.XPath;
using Autofac;
using SmartStore.Core;
using SmartStore.Core.Async;
using SmartStore.Core.Data;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Domain.Common;
using SmartStore.Core.Domain.Stores;
using SmartStore.Core.Localization;
using SmartStore.Core.Logging;
using SmartStore.Core.Plugins;
using SmartStore.Services;
using SmartStore.Services.Catalog;
using SmartStore.Services.DataExchange.Export;
using SmartStore.Services.Helpers;
using SmartStore.Services.Media;
using SmartStore.Services.Search;
using SmartStore.Services.Tax;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.ExportProvider;
using SmartStore.ShopConnector.Extensions;
using SmartStore.ShopConnector.Models;
using SmartStore.Utilities;

namespace SmartStore.ShopConnector.Services
{
    public partial class ShopConnectorService : IShopConnectorService
    {
        private readonly IRepository<ShopConnectorConnectionRecord> _connectionRepository;
        private readonly IRepository<ShopConnectorSkuMapping> _skuMappingRepository;
        private readonly ICommonServices _services;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly AdminAreaSettings _adminAreaSettings;
        private readonly IManufacturerService _manufacturerService;
        private readonly ITaxCategoryService _taxCategoryService;
        private readonly Lazy<IMediaService> _mediaService;
        private readonly Lazy<IPluginFinder> _pluginFinder;
        private readonly Lazy<IRepository<StoreMapping>> _storeMappingRepository;
        private readonly Lazy<ICategoryService> _categoryService;
        private readonly Lazy<IExportProfileService> _exportProfileService;
        private readonly Lazy<IDataExporter> _dataExporter;
        private readonly Lazy<ICatalogSearchService> _catalogSearchService;
        private readonly IAsyncState _asyncState;
        private readonly ShopConnectorSettings _shopConnectorSettings;

        public ShopConnectorService(
            IRepository<ShopConnectorConnectionRecord> connectionRepository,
            IRepository<ShopConnectorSkuMapping> skuMappingRepository,
            ICommonServices services,
            IDateTimeHelper dateTimeHelper,
            AdminAreaSettings adminAreaSettings,
            IManufacturerService manufacturerService,
            ITaxCategoryService taxCategoryService,
            Lazy<IMediaService> mediaService,
            Lazy<IPluginFinder> pluginFinder,
            Lazy<IRepository<StoreMapping>> storeMappingRepository,
            Lazy<ICategoryService> categoryService,
            Lazy<IExportProfileService> exportProfileService,
            Lazy<IDataExporter> dataExporter,
            Lazy<ICatalogSearchService> catalogSearchService,
            IAsyncState asyncState,
            ShopConnectorSettings shopConnectorSettings)
        {
            _connectionRepository = connectionRepository;
            _skuMappingRepository = skuMappingRepository;
            _services = services;
            _dateTimeHelper = dateTimeHelper;
            _adminAreaSettings = adminAreaSettings;
            _manufacturerService = manufacturerService;
            _taxCategoryService = taxCategoryService;
            _mediaService = mediaService;
            _pluginFinder = pluginFinder;
            _storeMappingRepository = storeMappingRepository;
            _categoryService = categoryService;
            _exportProfileService = exportProfileService;
            _dataExporter = dataExporter;
            _catalogSearchService = catalogSearchService;
            _asyncState = asyncState;
            _shopConnectorSettings = shopConnectorSettings;
        }

        public Localizer T { get; set; } = NullLocalizer.Instance;
        public ILogger Logger { get; set; } = NullLogger.Instance;

        public static XmlWriterSettings DefaultSettings => new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            CheckCharacters = false,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };

        private IQueryable<Product> GetProductQuery(
            int[] providerManus,
            int[] providerStores,
            int[] consumerManus,
            int categoryId = 0,
            DateTime? updatedOnUtc = null,
            string catalogId = null)
        {
            Guard.NotNull(providerManus, nameof(providerManus));
            Guard.NotNull(providerStores, nameof(providerStores));
            Guard.NotNull(consumerManus, nameof(consumerManus));

            List<int> manuIds = null;
            List<int> storeIds = null;

            if (providerStores.Any())
            {
                storeIds = providerStores.Where(x => x != 0).ToList();
            }

            if (providerManus.Any())
            {
                manuIds = new List<int>();

                foreach (var id in providerManus.Where(x => x != 0))
                {
                    if (!consumerManus.Any() || consumerManus.Contains(id))
                    {
                        manuIds.Add(id);
                    }
                }
            }
            else
            {
                manuIds = consumerManus.Where(x => x != 0).ToList();
            }

            var searchQuery = new CatalogSearchQuery();

            if (!_shopConnectorSettings.IncludeHiddenProducts)
            {
                searchQuery = searchQuery.PublishedOnly(true);
            }

            var query = _catalogSearchService.Value.PrepareQuery(searchQuery);

            if (catalogId.HasValue())
            {
                query = query.Where(x => x.ImportCatalogId == catalogId);
            }

            if (updatedOnUtc.HasValue)
            {
                query = query.Where(x => x.UpdatedOnUtc > updatedOnUtc);
            }

            if (storeIds != null && storeIds.Any())
            {
                query =
                    from p in query
                    join sm in _storeMappingRepository.Value.TableUntracked on new { pid = p.Id, pname = "Product" } equals new { pid = sm.EntityId, pname = sm.EntityName } into psm
                    from sm in psm.DefaultIfEmpty()
                    where !p.LimitedToStores || storeIds.Contains(sm.StoreId)
                    select p;
            }

            if (manuIds != null && manuIds.Any())
            {
                query =
                    from p in query
                    from pm in p.ProductManufacturers.Where(pm => manuIds.Contains(pm.ManufacturerId))
                    select p;

                //var distinctIds = (
                //	from p in query
                //	join pm in _productManufacturerRepository.Value.TableUntracked on p.Id equals pm.ProductId
                //	where manuIds.Contains(pm.ManufacturerId)
                //	select p.Id).Distinct();

                //query =
                //	from p in query
                //	join x in distinctIds on p.Id equals x
                //	select p;
            }

            if (categoryId != 0)
            {
                query =
                    from p in query
                    from pc in p.ProductCategories.Where(pc => pc.CategoryId == categoryId)
                    select p;

                //var distinctIds = (
                //	from p in query
                //	join pc in _productCategoryRepository.Value.TableUntracked on p.Id equals pc.ProductId
                //	where pc.CategoryId == categoryId
                //	select p.Id).Distinct();

                //query =
                //	from p in query
                //	join x in distinctIds on p.Id equals x
                //	select p;
            }

            return query;
        }

        private string GetCategoryBreadCrumb(ShopConnectorCategory category, Dictionary<int, ShopConnectorCategory> categories)
        {
            var result = "";

            while (category != null)
            {
                var name = (category.Alias.HasValue() ? "{0} ({1})".FormatInvariant(category.Name, category.Alias) : category.Name);

                if (result.IsEmpty())
                    result = name;
                else
                    result = string.Concat(name, " » ", result);

                category = (categories.ContainsKey(category.ParentId) ? categories[category.ParentId] : null);
            }

            return result;
        }

        public DateTime? ConvertDateTime(DateTime? dt, bool toUserTime)
        {
            // there is no operation made in context of a user/customer, so better use store's timezone.
            if (!dt.HasValue)
                return null;

            if (toUserTime)
                return _dateTimeHelper.ConvertToUserTime(dt.Value, TimeZoneInfo.Utc, _dateTimeHelper.DefaultStoreTimeZone);

            return _dateTimeHelper.ConvertToUtcTime(dt.Value, _dateTimeHelper.DefaultStoreTimeZone);
        }

        public void SetupConfiguration(ConfigurationModel model)
        {
            var urlHelper = new UrlHelper(HttpContext.Current.Request.RequestContext);
            var logFile = ShopConnectorFileSystem.ImportLogFile();

            model.GridPageSize = _adminAreaSettings.GridPageSize;
            model.LogFileExists = File.Exists(logFile);

            model.Strings = new Dictionary<string, string>
            {
                { "Admin.Common.Edit", T("Admin.Common.Edit") },
                { "Admin.Common.Delete", T("Admin.Common.Delete") },
                { "Admin.Common.Actions", T("Admin.Common.Actions") },
                { "Admin.Common.About", T("Admin.Common.About") },
                { "Action.About.Hint", T("Plugins.SmartStore.ShopConnector.Action.About.Hint") },
                { "Action.ProductData", T("Plugins.SmartStore.ShopConnector.Action.ProductData") },
                { "Action.ProductData.Hint", T("Plugins.SmartStore.ShopConnector.Action.ProductData.Hint") },
                { "Action.ProductImport", T("Plugins.SmartStore.ShopConnector.Action.ProductImport") },
                { "Action.ProductImport.Hint", T("Plugins.SmartStore.ShopConnector.Action.ProductImport.Hint") }
            };

            model.ImportUrls = new Dictionary<string, string>
            {
                { "About", urlHelper.Action("About", "ShopConnectorImport", new { id = "<#= Id #>", area = ShopConnectorPlugin.SystemName }) },
                { "ProductData", urlHelper.Action("ProductData", "ShopConnectorImport", new { id = "<#= Id #>", area = ShopConnectorPlugin.SystemName }) },
                { "ProductFileSelect", urlHelper.Action("ProductFileSelect", "ShopConnectorImport", new { id = "<#= Id #>", area = ShopConnectorPlugin.SystemName }) },
                { "ProductImportProgress", urlHelper.Action("ProductImportProgress", "ShopConnectorImport", new { area = ShopConnectorPlugin.SystemName }) }
            };
        }

        public void SetupConnectionModel(ConnectionModel model, int id, bool isForExport)
        {
            var connection = id != 0 ? _connectionRepository.GetById(id) : null;

            model.IsForExport = isForExport;

            if (connection == null)
            {
                // Insert.
                model.IsActive = true;
                model.Url = "http://";
                model.LimitedToManufacturerIds = new int[0];
            }
            else
            {
                model.Id = connection.Id;
                model.IsActive = connection.IsActive;
                model.Url = connection.Url;
                model.PublicKey = connection.PublicKey;
                model.SecretKey = connection.SecretKey;
                model.LimitedToManufacturerIds = connection.LimitedToManufacturerIds.ToIntArray();
                model.LimitedToStoreIds = connection.LimitedToStoreIds.ToIntArray();
                model.CreatedOn = ConvertDateTime(connection.CreatedOnUtc, true);
                model.UpdatedOn = ConvertDateTime(connection.UpdatedOnUtc, true);
            }

            if (isForExport)
            {
                model.AvailableStores = new MultiSelectList(_services.StoreService.GetAllStores(), "Id", "Name", model.LimitedToStoreIds);
                model.AvailableManufacturers = new MultiSelectList(_manufacturerService.GetAllManufacturers(), "Id", "Name", model.LimitedToManufacturerIds);
            }
        }
        public void SetupProductFileSelectModel(ProductFileSelectModel model, int id)
        {
            var files = new ShopConnectorFileSystem("Product");

            model.Id = id;
            model.AvailableImportFiles = new List<SelectListItem>();

            foreach (string name in files.GetAllFiles().OrderByDescending(x => x))
            {
                model.AvailableImportFiles.Add(new SelectListItem { Text = name, Value = name });
            }
        }
        public void SetupProductImportModel(ProductImportModel model, int id)
        {
            model.Id = id;
            model.GridPageSize = _adminAreaSettings.GridPageSize;
            model.AvailableTaxCategories = new List<SelectListItem>();

            model.AvailableTaxCategories = _taxCategoryService.GetAllTaxCategories()
                .Select(x => new SelectListItem
                {
                    Text = x.Name,
                    Value = x.Id.ToString()
                })
                .ToList();

            if (model.ImportFile.HasValue())
            {
                var files = new ShopConnectorFileSystem("Product");
                var path = files.GetFullFilePath(model.ImportFile);
                var fileSize = ShopConnectorFileSystem.GetFileSize(path);
                double mb = fileSize / 1024f / 1024f;

                if (mb > _shopConnectorSettings.MaxFileSizeForPreview)
                {
                    model.FileTooLargeForPreviewWarning = T("Plugins.SmartStore.ShopConnector.FileTooLargeForPreview", Math.Round(mb, 1).ToString("N0"));
                }
            }
        }
        public void SetupProductImportCompletedModel(ProductImportCompletedModel model)
        {
            var settings = _services.Settings.LoadSetting<ShopConnectorSettings>();
            var logFile = ShopConnectorFileSystem.ImportLogFile();

            if (File.Exists(logFile))
            {
                var urlHelper = new UrlHelper(HttpContext.Current.Request.RequestContext);

                model.ImportLogFileUrl = urlHelper.Action("ImportLog", "ShopConnectorImport", new { area = ShopConnectorPlugin.SystemName });
            }
        }

        public AboutModel CreateAboutModel(CachedConnection connection)
        {
            int[] manuIds = null;
            int[] storeIds = null;
            var stores = _services.StoreService.GetAllStores();
            var store = stores.FindStore(HttpContext.Current.Request.Url);
            var plugin = _pluginFinder.Value.GetPluginDescriptorBySystemName(ShopConnectorPlugin.SystemName);
            var persistedConnection = _connectionRepository.GetById(connection.Id);

            var model = new AboutModel
            {
                AppVersion = SmartStoreVersion.CurrentFullVersion,
                UtcTime = DateTime.UtcNow,
                StoreCount = stores.Count,
                AvailableCategories = new List<SelectListItem>()
            };

            if (connection.LastProductCallUtc.HasValue)
                model.UpdatedProductsCount = GetProductQuery(new int[0], new int[0], new int[0], 0, connection.LastProductCallUtc.Value).Count().ToString();

            if (plugin != null)
                model.ConnectorVersion = string.Concat(ShopConnectorCore.ConnectorVersion, " ", plugin.Version.ToString());

            if (store != null)
            {
                model.StoreName = store.Name.NaIfEmpty();
                model.StoreUrl = store.Url;

                var logo = _mediaService.Value.GetFileById(store.LogoMediaFileId, MediaLoadFlags.AsNoTracking);
                if (logo != null)
                {
                    model.StoreLogoUrl = _mediaService.Value.GetUrl(logo, 0, _services.StoreService.GetHost(store), false);
                }

                var companySettings = _services.Settings.LoadSetting<CompanyInformationSettings>(store.Id);
                model.CompanyName = companySettings.CompanyName.NaIfEmpty();
            }

            if (persistedConnection == null)
            {
                model.AvailableManufacturers = new List<SelectListItem>();
                manuIds = new int[0];
                storeIds = new int[0];
            }
            else
            {
                int[] selectedManuIds = persistedConnection.LimitedToManufacturerIds.ToIntArray();
                var hasManuIds = selectedManuIds.Any();

                model.AvailableManufacturers = _manufacturerService.GetAllManufacturers()
                    .OrderBy(x => x.Name)
                    .Where(x => !hasManuIds || selectedManuIds.Any(y => y == x.Id))
                    .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString(), Selected = selectedManuIds.Contains(x.Id) })
                    .ToList();

                manuIds = model.AvailableManufacturers.Select(x => x.Value.ToInt()).ToArray();
                storeIds = persistedConnection.LimitedToStoreIds.ToIntArray();
            }

            // Get only categories that are assigned to the returned products.
            if (_shopConnectorSettings.MaxCategoriesToFilter > 0)
            {
                var pageIndex = 0;
                var categoryIds = new HashSet<int>();
                IPagedList<int> pagedCategoryIds = null;

                // Distinct is very important to avoid fetching same IDs again and again.
                var productCategoryQuery = GetProductQuery(new int[0], storeIds, manuIds)
                    .SelectMany(x => x.ProductCategories.Select(y => y.CategoryId))
                    .Distinct()
                    .OrderBy(x => x);

                do
                {
                    pagedCategoryIds = new PagedList<int>(productCategoryQuery, pageIndex++, 1000);

                    categoryIds.AddRange(pagedCategoryIds);
                }
                while (pagedCategoryIds.HasNextPage && categoryIds.Count <= _shopConnectorSettings.MaxCategoriesToFilter);

                if (categoryIds.Any())
                {
                    var tree = _categoryService.Value.GetCategoryTree();

                    foreach (var categoryId in categoryIds)
                    {
                        var node = tree.SelectNodeById(categoryId);
                        if (node != null)
                        {
                            var path = node.Value.GetCategoryPath(_categoryService.Value, aliasPattern: "({0})");
                            model.AvailableCategories.Add(new SelectListItem { Text = path, Value = node.Value.Id.ToString() });
                        }
                    }
                }
            }

            return model;
        }

        public List<ProductImportItemModel> GetProductImportItems(string importFile, int pageIndex, out int totalItems)
        {
            var data = new List<ProductImportItemModel>();
            var files = new ShopConnectorFileSystem("Product");
            var path = files.GetFullFilePath(importFile);

            var doc = new XPathDocument(path);
            var nav = doc.GetContent() ?? doc.CreateNavigator();

            var xpathProducts = "Products/Product";
            var products = nav.Select(xpathProducts);
            var categories = new Dictionary<int, ShopConnectorCategory>();

            if (nav != null)
            {
                foreach (XPathNavigator category in nav.Select("Categories/Category"))
                {
                    categories.SafeAddId(category.GetValue("Id", 0), new ShopConnectorCategory
                    {
                        Name = category.GetString("Name").NaIfEmpty(),
                        Alias = category.GetString("Alias"),
                        ParentId = category.GetValue<int>("ParentCategoryId")
                    });
                }
            }

            totalItems = products.Count;

            var idx = 0;
            var pageSize = _adminAreaSettings.GridPageSize;
            var firstItemIndex = (pageIndex * pageSize) + 1;
            var lastItemIndex = Math.Min(totalItems, (pageIndex * pageSize) + pageSize);

            foreach (XPathNavigator product in products)
            {
                ++idx;
                if (idx >= firstItemIndex && idx <= lastItemIndex)
                {
                    var importProduct = new ProductImportItemModel
                    {
                        Id = product.GetValue<int>("Id"),
                        Name = product.GetString("Name"),
                        Sku = product.GetString("Sku"),
                        ProductTypeId = product.GetValue<int>("ProductTypeId"),
                        Categories = new List<string>(),
                        Manufacturers = new List<string>()
                    };

                    var productType = ProductType.SimpleProduct;
                    if (Enum.TryParse(importProduct.ProductTypeId.ToString(), out productType) && productType != ProductType.SimpleProduct)
                    {
                        importProduct.ProductTypeName = T("Admin.Catalog.Products.ProductType.{0}.Label".FormatInvariant(productType.ToString()));
                    }

                    importProduct.ProductType = productType;

                    foreach (XPathNavigator productManufacturer in product.Select("ProductManufacturers/ProductManufacturer"))
                    {
                        var manuName = productManufacturer.SelectSingleNode("Manufacturer").GetString("Name");
                        if (!importProduct.Manufacturers.Contains(manuName))
                            importProduct.Manufacturers.Add(manuName);
                    }

                    foreach (XPathNavigator productCategory in product.Select("ProductCategories/ProductCategory"))
                    {
                        var categoryId = productCategory.SelectSingleNode("Category").GetValue("Id", 0);
                        if (categories.ContainsKey(categoryId))
                        {
                            var breadCrumb = GetCategoryBreadCrumb(categories[categoryId], categories);
                            if (breadCrumb.HasValue())
                                importProduct.Categories.Add(breadCrumb);
                        }
                    }

                    data.Add(importProduct);
                }
                else if (idx > lastItemIndex)
                {
                    break;
                }
            }
            return data;
        }

        public bool SendRequest(ShopConnectorRequestContext context, int id)
        {
            try
            {
                string val;
                var controllingData = ConnectionCache.ControllingData();
                var connection = controllingData.Connections.FirstOrDefault(x => x.Id == id);
                context.Version = controllingData.Version;
                context.Connection = connection;

                Debug.Assert(!connection.IsForExport || (connection.IsForExport && context.ActionMethod.IsCaseInsensitiveEqual("Notification")),
                    "Import connection must be used to consume data.", "");

                if (!connection.IsActive)
                {
                    context.ResponseModel = new OperationResultModel(T("Plugins.SmartStore.ShopConnector.ConnectionNotActive"));
                    return false;
                }

                if (!controllingData.IsImportEnabled)
                {
                    context.ResponseModel = new OperationResultModel(T("Plugins.SmartStore.ShopConnector.ImportNotActive"));
                    return false;
                }

                context.PublicKey = connection.PublicKey;
                context.SecretKey = connection.SecretKey;
                context.HttpAcceptType = "application/atom+xml,application/atomsvc+xml,application/xml";

                if (context.Url.IsEmpty())
                {
                    context.Url = connection.Url.GetEndpointUrl(context.ActionMethod);
                }
                if (context.HttpMethod.IsEmpty())
                {
                    context.HttpMethod = "GET";
                }

                if (context.ActionMethod == "About")
                {
                    var fs = new ShopConnectorFileSystem("About");
                    context.ResponsePath = fs.GetFullFilePath(string.Concat("about-", Guid.NewGuid().ToString(), ".xml"));
                }
                else if (context.ActionMethod == "ProductData")
                {
                    context.ResponsePath = new ShopConnectorFileSystem("Product").GetFilePath(context.RequestContent["DataFileName"]);
                }

                var consumer = new ShopConnectorConsumer();
                var request = consumer.StartRequest(context);
                consumer.ProcessResponse(context, request);

                context.Success = context.ResponseModel == null;
                if (context.Success)
                {
                    controllingData.ConnectionsUpdated = true;

                    if (context.Headers.TryGetValue("Sm-ShopConnector-RequestCount", out val) && long.TryParse(val, out var requestCount))
                    {
                        connection.RequestCount = requestCount;
                    }
                    if (context.Headers.TryGetValue("Sm-ShopConnector-LastRequest", out val))
                    {
                        connection.LastRequestUtc = val.ToDateTimeIso8601();
                    }
                    if (context.Headers.TryGetValue("Sm-ShopConnector-LastProductCall", out val))
                    {
                        connection.LastProductCallUtc = val.ToDateTimeIso8601();
                    }
                }

                ShopConnectorFileSystem.CleanupDirectories();
            }
            catch (Exception ex)
            {
                context.Success = false;
                context.ResponseModel = new OperationResultModel(ex);
            }

            return context.Success;
        }

        //public bool SendNotification(int id, bool hasNewData)
        //{
        //	var context = new ShopConnectorRequestContext()
        //	{
        //		ActionMethod = "Notification",
        //		HttpMethod = "POST",
        //		RequestContent = "{0}={1}".FormatInvariant(ShopConnectorCore.Xml.HasNewData, hasNewData.ToString())
        //	};

        //	bool success = SendRequest(context, id);
        //	return success;
        //}

        //public void ProcessNotification()
        //{
        //	var request = HttpContext.Current.Request;
        //	string publicKey = request.Headers[ShopConnectorCore.Header.PublicKey];
        //	var controllingData = ShopConnectorCaching.ControllingData();

        //	var connection = controllingData.Connections.FirstOrDefault(x => x.PublicKey == publicKey && !x.IsForExport);
        //	if (connection != null)
        //	{
        //		controllingData.ConnectionsUpdated = true;
        //		connection.HasNewData = request.Form[ShopConnectorCore.Xml.HasNewData].ToBool();
        //	}
        //}

        public DataExportResult Export(ShopConnectorExportContext context, CancellationToken token, string providerSystemName)
        {
            var provider = _exportProfileService.Value.LoadProvider(providerSystemName);
            var profile = _exportProfileService.Value.GetSystemExportProfile(providerSystemName);

            if (profile == null)
            {
                profile = _exportProfileService.Value.InsertExportProfile(provider, true);
            }

            if (context.Connection == null)
            {
                var controllingData = ConnectionCache.ControllingData();
                var connection = controllingData.Connections.FirstOrDefault(x => x.PublicKey == context.PublicKey && x.IsForExport);
                context.Connection = _connectionRepository.GetById(connection.Id);
            }

            var limitedToStoreIds = context.Connection.LimitedToStoreIds.ToIntArray();
            var domain = _shopConnectorSettings.EnableSkuMapping
                ? context.Connection.Url.ToDomain()
                : string.Empty;

            var request = new DataExportRequest(profile, provider);
            request.CustomData.Add(ShopConnectorCore.Header.PublicKey, context.PublicKey);
            request.CustomData.Add("CategoryIds", context.CategoryIds);
            request.CustomData.Add("StoreIds", limitedToStoreIds);
            request.CustomData.Add("Domain", domain);
            request.HasPermission = true;

            if (providerSystemName == ShopConnectorProductXmlExportProvider.SystemName)
            {
                var fetchFrom = context.Model.FetchFrom.ToDateTimeIso8601();
                var limitedToManufacturerIds = context.Connection.LimitedToManufacturerIds.ToIntArray();

                request.ProductQuery = GetProductQuery(
                    limitedToManufacturerIds,
                    limitedToStoreIds,
                    context.Model.FilterManufacturerIds,
                    context.Model.FilterCategoryId.ToInt(),
                    fetchFrom,
                    context.Model.FilterCatalogId);
            }

            var result = _dataExporter.Value.Export(request, token);
            return result;
        }

        public void Import(ProductImportModel model)
        {
            var controllingData = ConnectionCache.ControllingData();

            if (!controllingData.IsImportEnabled)
            {
                _services.Notifier.Error(T("Plugins.SmartStore.ShopConnector.ImportNotActive"));
                return;
            }

            if (_asyncState.Exists<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName))
            {
                _asyncState.Remove<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName);
            }

            var utcNow = DateTime.UtcNow;
            var state = new ShopConnectorImportState
            {
                ImportCategories = model.ImportCategories,
                ImportAll = model.ImportAll,
                ImportFile = model.ImportFile,
                UpdateExistingProducts = model.UpdateExistingProducts,
                UpdateExistingCategories = model.UpdateExistingCategories,
                DeleteImportFile = model.DeleteImportFile,
                TaxCategoryId = model.TaxCategoryId,
                LimitedToStores = model.LimitedToStores,
                EventPublishEntityCount = 100,
                Publish = model.Publish,
                DisableBuyButton = model.DisableBuyButton,
                DisableWishlistButton = model.DisableWishlistButton
            };

            try
            {
                state.IgnoreEntityNames = _shopConnectorSettings.IgnoreEntityNames
                    .SplitSafe(",")
                    .Select(x => x.TrimSafe())
                    .ToList();

                if (model.SelectedStoreIds != null && model.SelectedStoreIds.Any())
                    state.SelectedStoreIds = model.SelectedStoreIds.ToList();

                if (!model.ImportAll && !string.IsNullOrWhiteSpace(model.SelectedProductIds))
                    state.SelectedProductIds = model.SelectedProductIds.SplitSafe(",").Select(x => x.ToInt()).ToDictionarySafe(x => x, x => 0);

                var task = AsyncRunner.Run((c, ct, x) =>
                {
                    var obj = x as ShopConnectorImportState;
                    c.Resolve<IShopConnectorImportService>().StartProductImport(obj);
                }, state);

                _services.Notifier.Information(new LocalizedString(T("Plugins.SmartStore.ShopConnector.ImportInProgress")));

                task.Wait(500);
            }
            catch (Exception ex)
            {
                _services.Notifier.Error(ex.ToAllMessages());
                Logger.Error(ex);
            }
        }

#if DEBUG
        public static List<FileDownloadManagerItem> SampleDownloads()
        {
            var items = new List<FileDownloadManagerItem>();
            var root = FileSystemHelper.TempDirTenant().EnsureEndsWith("\\");

            items.Add(new FileDownloadManagerItem { Url = "http://chsvimg.nikon.com/lineup/dslr/d600/img/sample01/img_10_l.jpg", Path = root + "pic1.jpg" });
            items.Add(new FileDownloadManagerItem { Url = "http://www.amateurphotographer.co.uk/photo-gallery/data/1100/medium/jpeg_detail_iso100.jpg", Path = root + "pic2.jpg" });
            items.Add(new FileDownloadManagerItem { Url = "http://photographylife.com/wp-content/uploads/2010/03/Nikon-16-35mm-VR-Sample-1.jpg", Path = root + "pic3.jpg" });
            items.Add(new FileDownloadManagerItem { Url = "http://web.canon.jp/imaging/eosd/samples/eos1100d/downloads/04.jpg", Path = root + "pic4.jpg" });
            items.Add(new FileDownloadManagerItem { Url = "http://www.fujifilm.com/products/digital_cameras/x/fujifilm_x100s/sample_images/img/index/ff_x100s_002.JPG", Path = root + "pic5.jpg" });
            items.Add(new FileDownloadManagerItem { Url = "http://web.canon.jp/imaging/eosd/samples/eos60d/downloads/04.jpg", Path = root + "pic6.jpg" });

            return items;
        }
#endif
    }


    public class ShopConnectorExportContext
    {
        public ShopConnectorConnectionRecord Connection { get; set; }
        public ProductDataModel Model { get; set; }
        public string PublicKey { get; set; }

        /// <summary>
        /// Categories to which the exported products are assigned to, otherwise all categories would be exported.
        /// </summary>
        public HashSet<int> CategoryIds { get; set; }
    }

    internal class ShopConnectorCategory
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public int ParentId { get; set; }
    }
}