using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using SmartStore.Collections;
using SmartStore.Core;
using SmartStore.Core.Async;
using SmartStore.Core.Data;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Domain.Directory;
using SmartStore.Core.Domain.Localization;
using SmartStore.Core.Domain.Media;
using SmartStore.Core.Domain.Seo;
using SmartStore.Core.Domain.Stores;
using SmartStore.Core.Localization;
using SmartStore.Core.Logging;
using SmartStore.Core.Search.Facets;
using SmartStore.Data.Utilities;
using SmartStore.Services;
using SmartStore.Services.Catalog;
using SmartStore.Services.Directory;
using SmartStore.Services.Localization;
using SmartStore.Services.Media;
using SmartStore.Services.Seo;
using SmartStore.ShopConnector.Extensions;
using SmartStore.Utilities;
using SmartStore.Services.DataExchange.Import.Events;

namespace SmartStore.ShopConnector.Services
{
    public class ShopConnectorImportService : IShopConnectorImportService
    {
        private readonly IRepository<Product> _rsProduct;
        private readonly IRepository<ProductMediaFile> _rsProductPicture;
        private readonly IRepository<ProductBundleItem> _rsProductBundleItem;
        private readonly IRepository<ProductCategory> _rsProductCategory;
        private readonly IRepository<Category> _rsCategory;
        private readonly IRepository<ProductManufacturer> _rsProductManufacturer;
        private readonly IRepository<Manufacturer> _rsManufacturer;
        private readonly IRepository<SpecificationAttribute> _rsSpecAttribute;
        private readonly IRepository<SpecificationAttributeOption> _rsSpecAttributeOption;
        private readonly IRepository<ProductSpecificationAttribute> _rsProductSpecAttribute;
        private readonly IRepository<ProductTag> _rsProductTag;
        private readonly IRepository<TierPrice> _rsTierPrice;
        private readonly IRepository<StoreMapping> _rsStoreMapping;
        private readonly IRepository<ProductAttribute> _rsProductAttribute;
        private readonly IRepository<ProductVariantAttributeValue> _rsProductVariantAttributeValue;
        private readonly IRepository<ProductVariantAttribute> _rsProductVariantAttribute;
        private readonly IRepository<ProductVariantAttributeCombination> _rsProductVariantAttributeCombination;
        private readonly IProductService _productService;
        private readonly IProductTemplateService _productTemplateService;
        private readonly ICategoryTemplateService _categoryTemplateService;
        private readonly IQuantityUnitService _quantityUnitService;
        private readonly IDeliveryTimeService _deliveryTimeService;
        private readonly IMediaService _mediaService;
        private readonly IFolderService _folderService;
        private readonly ILanguageService _languageService;
        private readonly ILocalizedEntityService _localizedEntityService;
        private readonly IUrlRecordService _urlRecordService;
        private readonly ICommonServices _services;
        private readonly IProductAttributeParser _attributeParser;
        private readonly ShopConnectorSettings _shopConnectorSettings;
        private readonly SeoSettings _seoSettings;
        private readonly FileDownloadManager _fileDownloadManager;
        private readonly IAsyncState _asyncState;
        private TraceLogger _logger;

        public ShopConnectorImportService(
            IRepository<Product> rsProduct,
            IRepository<ProductMediaFile> rsProductPicture,
            IRepository<ProductBundleItem> rsProductBundleItem,
            IRepository<ProductCategory> rsProductCategory,
            IRepository<Category> rsCategory,
            IRepository<ProductManufacturer> rsProductManufacturer,
            IRepository<Manufacturer> rsManufacturer,
            IRepository<SpecificationAttribute> rsSpecAttribute,
            IRepository<SpecificationAttributeOption> rsSpecAttributeOption,
            IRepository<ProductSpecificationAttribute> rsProductSpecAttribute,
            IRepository<ProductTag> rsProductTag,
            IRepository<TierPrice> rsTierPrice,
            IRepository<StoreMapping> rsStoreMapping,
            IRepository<ProductAttribute> rsProductAttribute,
            IRepository<ProductVariantAttributeValue> rsProductVariantAttributeValue,
            IRepository<ProductVariantAttribute> rsProductVariantAttribute,
            IRepository<ProductVariantAttributeCombination> rsProductVariantAttributeCombination,
            IProductService productService,
            IProductTemplateService productTemplateService,
            ICategoryTemplateService categoryTemplateService,
            IQuantityUnitService quantityUnitService,
            IDeliveryTimeService deliveryTimeService,
            IMediaService mediaService,
            IFolderService folderService,
            ILanguageService languageService,
            ILocalizedEntityService localizedEntityService,
            IUrlRecordService urlRecordService,
            ICommonServices services,
            IProductAttributeParser attributeParser,
            ShopConnectorSettings shopConnectorSettings,
            SeoSettings seoSettings,
            FileDownloadManager fileDownloadManager,
            IAsyncState asyncState)
        {
            _rsProduct = rsProduct;
            _rsProductPicture = rsProductPicture;
            _rsProductBundleItem = rsProductBundleItem;
            _rsProductCategory = rsProductCategory;
            _rsCategory = rsCategory;
            _rsProductManufacturer = rsProductManufacturer;
            _rsManufacturer = rsManufacturer;
            _rsSpecAttribute = rsSpecAttribute;
            _rsSpecAttributeOption = rsSpecAttributeOption;
            _rsProductSpecAttribute = rsProductSpecAttribute;
            _rsProductTag = rsProductTag;
            _rsTierPrice = rsTierPrice;
            _rsStoreMapping = rsStoreMapping;
            _rsProductAttribute = rsProductAttribute;
            _rsProductVariantAttributeValue = rsProductVariantAttributeValue;
            _rsProductVariantAttribute = rsProductVariantAttribute;
            _rsProductVariantAttributeCombination = rsProductVariantAttributeCombination;

            _productService = productService;
            _productTemplateService = productTemplateService;
            _categoryTemplateService = categoryTemplateService;
            _quantityUnitService = quantityUnitService;
            _deliveryTimeService = deliveryTimeService;
            _mediaService = mediaService;
            _folderService = folderService;
            _languageService = languageService;
            _localizedEntityService = localizedEntityService;
            _urlRecordService = urlRecordService;
            _services = services;
            _attributeParser = attributeParser;
            _shopConnectorSettings = shopConnectorSettings;
            _seoSettings = seoSettings;
            _fileDownloadManager = fileDownloadManager;
            _asyncState = asyncState;
        }

        public Localizer T { get; set; } = NullLocalizer.Instance;

        private int BulkCommit<T>(IRepository<T> rp, Action action) where T : BaseEntity
        {
            var num = 0;

            rp.AutoCommitEnabled = false;
            rp.Context.AutoCommitEnabled = false;
            rp.Context.AutoDetectChangesEnabled = false;

            try
            {
                action();
                num = rp.Context.SaveChanges();
            }
            finally
            {
                rp.AutoCommitEnabled = true;
                rp.Context.AutoCommitEnabled = true;
                rp.Context.AutoDetectChangesEnabled = true;
            }

            return num;
        }

        private void UpsertUrlRecord<T>(T entity, string seName, string name, bool ensureNotEmpty, int languageId) where T : BaseEntity, ISlugSupported
        {
            if (entity.Id != 0)
            {
                var slug = entity.ValidateSeName(seName, name, ensureNotEmpty, _urlRecordService, _seoSettings, languageId);

                _urlRecordService.SaveSlug(entity, slug, languageId);
            }
            else
            {
                _logger.Error("Entity ID is 0");
            }
        }

        private int? DownloadFile(CargoObjects cargo, XPathNavigator node)
        {
            int? fileId = null;

            var downloadItem = node.SelectSingleNode("Picture").ToDownloadItem(_services, cargo.ImageDirectory, 0);
            if (downloadItem?.Url?.HasValue() ?? false)
            {
                var image = _fileDownloadManager.DownloadFile(downloadItem.Url, false, _shopConnectorSettings.ImageDownloadTimeout * 60000);
                if (image?.Data?.Length > 0)
                {
                    using (var stream = image.Data.ToStream())
                    {
                        if (!_mediaService.FindEqualFile(stream, image.FileName, cargo.CatalogAlbumId, true, out var sourceFile))
                        {
                            var path = _mediaService.CombinePaths(SystemAlbumProvider.Catalog, image.FileName);
                            sourceFile = _mediaService.SaveFile(path, stream, false, DuplicateFileHandling.Rename)?.File;
                        }

                        if (sourceFile?.Id > 0)
                        {
                            fileId = sourceFile.Id;
                        }
                    }
                }
                else
                {
                    _logger.Info("Image download failed for {0}.".FormatInvariant(downloadItem.Url));
                }
            }

            return fileId;
        }

        private string CreateAttributeXml(CargoObjects cargo, string xml)
        {
            string attrXml = null;
            int eMappingId, eValueId;
            var pva = new ProductVariantAttribute();
            var attributes = _attributeParser.DeserializeProductVariantAttributes(xml);

            if (attributes != null && attributes.Count > 0)
            {
                foreach (var attribute in attributes.Where(x => x.Key != 0 && x.Value != null && x.Value.Count > 0))
                {
                    if (cargo.AttributeMappingIds.TryGetValue(attribute.Key, out eMappingId))
                    {
                        foreach (int valueId in attribute.Value.Select(x => x.ToInt()))
                        {
                            if (valueId != 0 && cargo.AttributeValueIds.TryGetValue(valueId, out eValueId))
                            {
                                pva.Id = eMappingId;
                                attrXml = _attributeParser.AddProductAttribute(attrXml, pva, eValueId.ToString());
                            }
                        }
                    }
                }
            }
            return attrXml;
        }

        private void GetExistingProducts(CargoObjects cargo, XPathNavigator nav)
        {
            cargo.ClearFragmentData();

            var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gtins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var manus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var specAttributeIds = new HashSet<int>();

            foreach (XPathNavigator product in nav.Select("Product"))
            {
                var sku = product.GetString("Sku");
                if (sku.HasValue())
                {
                    skus.Add(sku);
                }

                var gtin = product.GetString("Gtin");
                if (gtin.HasValue())
                {
                    gtins.Add(gtin);
                }

                foreach (XPathNavigator productManufacturer in product.Select("ProductManufacturers/ProductManufacturer"))
                {
                    var manu = productManufacturer.SelectSingleNode("Manufacturer");
                    var manuName = manu.GetString("Name");
                    if (manuName.HasValue())
                    {
                        manus.Add(manuName);
                    }
                }

                foreach (XPathNavigator tag in product.Select("ProductTags/ProductTag"))
                {
                    var tagName = tag.GetString("Name");
                    if (tagName.HasValue())
                    {
                        tags.Add(tagName);
                    }
                }

                // Get ID of existing spec attributes.
                foreach (XPathNavigator pca in product.Select("ProductSpecificationAttributes/ProductSpecificationAttribute"))
                {
                    var attribute = pca.SelectSingleNode("SpecificationAttributeOption/SpecificationAttribute");
                    var name = attribute.GetString("Name");
                    var alias = attribute.GetString("Alias");
                    var key = string.Concat(name.EmptyNull(), "|", alias.EmptyNull());

                    if (name.HasValue() && cargo.SpecAttributes.TryGetValue(key, out var eAttributeId) && eAttributeId != 0)
                    {
                        specAttributeIds.Add(eAttributeId);
                    }
                }
            }

            var hasSkus = skus.Any();
            var hasGtins = gtins.Any();
            var hasExistingProducts = true;
            var query = _rsProduct.Table;

            if (hasSkus && hasGtins)
            {
                query = query.Where(x => !x.Deleted && (skus.Contains(x.Sku) || gtins.Contains(x.Gtin)));
            }
            else if (hasSkus)
            {
                query = query.Where(x => !x.Deleted && skus.Contains(x.Sku));
            }
            else if (hasGtins)
            {
                query = query.Where(x => !x.Deleted && gtins.Contains(x.Gtin));
            }
            else
            {
                // Never ever get all products ;-)
                hasExistingProducts = false;
            }

            if (hasExistingProducts)
            {
                var products = query.ToList();
                var productIds = products.Select(x => x.Id).ToArray();

                cargo.ProductsBySku = products.Where(x => x.Sku.HasValue()).ToDictionarySafe(x => x.Sku, x => x, StringComparer.OrdinalIgnoreCase);
                cargo.ProductsByGtin = products.Where(x => x.Gtin.HasValue()).ToDictionarySafe(x => x.Gtin, x => x, StringComparer.OrdinalIgnoreCase);

                cargo.ProductCategories = _rsProductCategory.TableUntracked
                    .Where(x => productIds.Contains(x.ProductId) && !x.IsSystemMapping)
                    .Select(x => new { x.ProductId, x.CategoryId })
                    .ToList()
                    .ToMultimap(x => x.ProductId, x => x.CategoryId);

                cargo.ProductManufacturers = _rsProductManufacturer.TableUntracked
                    .Where(x => productIds.Contains(x.ProductId))
                    .Select(x => new { x.ProductId, x.ManufacturerId })
                    .ToList()
                    .ToMultimap(x => x.ProductId, x => x.ManufacturerId);

                cargo.ProductPictures = _rsProductPicture.TableUntracked
                    .Expand(x => x.MediaFile)
                    .Where(x => productIds.Contains(x.ProductId))
                    .ToList()
                    .ToMultimap(x => x.ProductId, x => x);

                cargo.ProductSpecAttributes = _rsProductSpecAttribute.TableUntracked
                    .Where(x => productIds.Contains(x.ProductId))
                    .Select(x => new { x.ProductId, x.SpecificationAttributeOptionId })
                    .ToList()
                    .ToMultimap(x => x.ProductId, x => x.SpecificationAttributeOptionId);

                var productAttributes = _rsProductVariantAttribute.TableUntracked
                    .Where(x => productIds.Contains(x.ProductId))
                    .Select(x => new { x.Id, x.ProductId, x.ProductAttributeId })
                    .ToList();

                cargo.ProductAttributes = productAttributes
                    .ToDictionarySafe(x => string.Concat(x.ProductId, "|", x.ProductAttributeId), x => x.Id, StringComparer.OrdinalIgnoreCase);

                var productAttributeIds = productAttributes.Select(x => x.Id).ToArray();
                if (productAttributeIds.Any())
                {
                    cargo.ProductAttributeValues = _rsProductVariantAttributeValue.TableUntracked
                        .Where(x => productAttributeIds.Contains(x.ProductVariantAttributeId))
                        .ToList()
                        .ToDictionarySafe(x => string.Concat(x.ProductVariantAttributeId, "|", x.Name.EmptyNull()), x => x, StringComparer.OrdinalIgnoreCase);
                }
            }

            if (manus.Any())
            {
                cargo.Manufacturers = _rsManufacturer.Table
                    .Where(x => !x.Deleted && manus.Contains(x.Name))
                    .ToList()
                    .ToDictionarySafe(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
            }

            if (tags.Any())
            {
                cargo.ProductTags = _rsProductTag.Table
                    .Where(x => tags.Contains(x.Name))
                    .ToList()
                    .ToDictionarySafe(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
            }

            if (specAttributeIds.Any())
            {
                cargo.SpecAttributeOptions = _rsSpecAttributeOption.TableUntracked
                    .Where(x => specAttributeIds.Contains(x.SpecificationAttributeId))
                    .Select(x => new { x.Id, x.SpecificationAttributeId, x.Name })
                    .ToList()
                    .ToDictionarySafe(x => string.Concat(x.SpecificationAttributeId, "|", x.Name.EmptyNull()), x => x.Id, StringComparer.OrdinalIgnoreCase);
            }
        }

        private Product GetEntity(CargoObjects cargo, XPathNavigator product)
        {
            Product entity = null;
            var sku = product.GetString("Sku");
            var gtin = product.GetString("Gtin");

            if (sku.HasValue())
            {
                cargo.ProductsBySku.TryGetValue(sku, out entity);
            }
            if (entity == null && gtin.HasValue())
            {
                cargo.ProductsByGtin.TryGetValue(gtin, out entity);
            }

            if (entity != null && !cargo.State.UpdateExistingProducts)
            {
                ++cargo.Stats.Skipped;
                _logger.Info("Skipped by option.".LoggerMessage(product));
                return null;
            }

            if (entity == null && product.GetString("Name").IsEmpty())
            {
                ++cargo.Stats.Failure;
                _logger.Error("The 'Name' field is required for new products.".LoggerMessage(product));
                return null;
            }

            if (entity == null)
            {
                entity = new Product();
            }

            cargo.IsNewEntity = entity.Id == 0;

            return entity;
        }

        private int? InsertQuantityUnit(CargoObjects cargo, XPathNavigator nav)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(QuantityUnit)))
            {
                return null;
            }

            var node = nav.SelectSingleNode("QuantityUnit");
            if (node == null)
            {
                return null;
            }

            var name = node.GetString("Name");
            if (name.IsEmpty())
            {
                return null;
            }

            if (cargo.QuantityUnits.TryGetValue(name, out int quantityUnitId))
            {
                return quantityUnitId;
            }

            var quantityUnit = new QuantityUnit
            {
                Name = name,
                NamePlural = node.GetString("NamePlural", name),
                Description = node.GetString("Description"),
                DisplayLocale = node.GetString("DisplayLocale"),
                DisplayOrder = node.GetValue<int>("DisplayOrder"),
                IsDefault = node.GetValue<bool>("IsDefault")
            };

            _quantityUnitService.InsertQuantityUnit(quantityUnit);

            cargo.QuantityUnits[name] = quantityUnit.Id;

            foreach (var language in cargo.Languages)
            {
                _localizedEntityService.SaveLocalizedValue(quantityUnit, x => x.Name, node.GetString(language, "Name"), language.Id);
                _localizedEntityService.SaveLocalizedValue(quantityUnit, x => x.NamePlural, node.GetString(language, "NamePlural"), language.Id);
                _localizedEntityService.SaveLocalizedValue(quantityUnit, x => x.Description, node.GetString(language, "Description"), language.Id);
            }

            return quantityUnit.Id;
        }

        private int? InsertDeliveryTime(CargoObjects cargo, XPathNavigator nav)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(DeliveryTime)))
            {
                return null;
            }

            var node = nav.SelectSingleNode("DeliveryTime");
            if (node == null)
            {
                return null;
            }

            var name = node.GetString("Name");
            if (name.IsEmpty())
            {
                return null;
            }

            if (cargo.DeliveryTimes.TryGetValue(name, out int deliveryTimeId))
            {
                return deliveryTimeId;
            }

            var deliveryTime = new DeliveryTime
            {
                Name = name,
                DisplayLocale = node.GetString("DisplayLocale"),
                ColorHexValue = node.GetString("ColorHexValue"),
                DisplayOrder = node.GetValue<int>("DisplayOrder"),
                IsDefault = node.GetValue<bool>("IsDefault"),
                MinDays = node.GetValue<int?>("MinDays"),
                MaxDays = node.GetValue<int?>("MaxDays")
            };

            _deliveryTimeService.InsertDeliveryTime(deliveryTime);

            cargo.DeliveryTimes[name] = deliveryTime.Id;

            foreach (var language in cargo.Languages)
            {
                _localizedEntityService.SaveLocalizedValue(deliveryTime, x => x.Name, node.GetString(language, "Name"), language.Id);
            }

            return deliveryTime.Id;
        }

        private void ProcessProduct(CargoObjects cargo, XPathNavigator product, Product entity, int id)
        {
            var p = product;
            var e = entity;

            e.Name = p.GetString("Name");
            e.ShortDescription = p.GetString("ShortDescription");
            e.FullDescription = p.GetString("FullDescription");
            e.MetaKeywords = p.GetString("MetaKeywords");
            e.MetaDescription = p.GetString("MetaDescription");
            e.MetaTitle = p.GetString("MetaTitle");
            e.Sku = p.GetString("Sku");
            e.ManufacturerPartNumber = p.GetString("ManufacturerPartNumber");
            e.Gtin = p.GetString("Gtin");

            if (e.IsDownload)
            {
                _logger.Warn("Downloads are not supported.".LoggerMessage(product));
            }

            var templateViewPath = p.GetString("ProductTemplateViewPath");
            if (cargo.ProductTemplates.ContainsKey(templateViewPath))
            {
                e.ProductTemplateId = cargo.ProductTemplates[templateViewPath];
            }
            else if (cargo.ProductTemplates.ContainsKey("Product"))
            {
                e.ProductTemplateId = cargo.ProductTemplates["Product"];
            }

            e.AdminComment = p.GetString("AdminComment");
            e.ShowOnHomePage = p.GetValue<bool>("ShowOnHomePage");
            e.HomePageDisplayOrder = p.GetValue<int>("HomePageDisplayOrder");
            e.AllowCustomerReviews = p.GetValue<bool>("AllowCustomerReviews");
            e.ProductTypeId = p.GetValue("ProductTypeId", (int)ProductType.SimpleProduct);
            e.IsGiftCard = p.GetValue<bool>("IsGiftCard");
            e.GiftCardTypeId = p.GetValue<int>("GiftCardTypeId");
            e.RequireOtherProducts = p.GetValue<bool>("RequireOtherProducts");
            e.AutomaticallyAddRequiredProducts = p.GetValue<bool>("AutomaticallyAddRequiredProducts");
            e.IsRecurring = p.GetValue<bool>("IsRecurring");
            e.RecurringCycleLength = p.GetValue<int>("RecurringCycleLength");
            e.RecurringCyclePeriodId = p.GetValue<int>("RecurringCyclePeriodId");
            e.RecurringTotalCycles = p.GetValue<int>("RecurringTotalCycles");
            e.IsShipEnabled = p.GetValue("IsShipEnabled", true);
            e.IsFreeShipping = p.GetValue<bool>("IsFreeShipping");
            e.AdditionalShippingCharge = p.GetValue<decimal>("AdditionalShippingCharge");
            e.IsTaxExempt = p.GetValue<bool>("IsTaxExempt");
            e.ManageInventoryMethodId = p.GetValue<int>("ManageInventoryMethodId");
            e.StockQuantity = p.GetValue("StockQuantity", 10000);
            e.DisplayStockAvailability = p.GetValue<bool>("DisplayStockAvailability");
            e.DisplayStockQuantity = p.GetValue<bool>("DisplayStockQuantity");
            e.MinStockQuantity = p.GetValue<int>("MinStockQuantity");
            e.LowStockActivityId = p.GetValue<int>("LowStockActivityId");
            e.NotifyAdminForQuantityBelow = p.GetValue<int>("NotifyAdminForQuantityBelow");
            e.BackorderModeId = p.GetValue<int>("BackorderModeId");
            e.AllowBackInStockSubscriptions = p.GetValue<bool>("AllowBackInStockSubscriptions");
            e.OrderMinimumQuantity = p.GetValue<int>("OrderMinimumQuantity");
            e.OrderMaximumQuantity = p.GetValue<int>("OrderMaximumQuantity");
            e.QuantityStep = p.GetValue<int>("QuantityStep");
            e.QuantiyControlType = (QuantityControlType)p.GetValue<int>("QuantiyControlType");
            e.HideQuantityControl = p.GetValue<bool>("HideQuantityControl");
            e.AllowedQuantities = p.GetString("AllowedQuantities");
            e.DisableBuyButton = cargo.State.DisableBuyButton ?? p.GetValue<bool>("DisableBuyButton");
            e.DisableWishlistButton = cargo.State.DisableWishlistButton ?? p.GetValue<bool>("DisableWishlistButton");
            e.AvailableForPreOrder = p.GetValue<bool>("AvailableForPreOrder");
            e.CallForPrice = p.GetValue<bool>("CallForPrice");
            e.Price = p.GetValue<decimal>("Price");
            e.OldPrice = p.GetValue<decimal>("OldPrice");
            e.ProductCost = p.GetValue<decimal>("ProductCost");
            e.SpecialPrice = p.GetValue<decimal?>("SpecialPrice");
            e.SpecialPriceStartDateTimeUtc = p.GetValue<DateTime?>("SpecialPriceStartDateTimeUtc");
            e.SpecialPriceEndDateTimeUtc = p.GetValue<DateTime?>("SpecialPriceEndDateTimeUtc");
            e.CustomerEntersPrice = p.GetValue<bool>("CustomerEntersPrice");
            e.MinimumCustomerEnteredPrice = p.GetValue<decimal>("MinimumCustomerEnteredPrice");
            e.MaximumCustomerEnteredPrice = p.GetValue<decimal>("MaximumCustomerEnteredPrice");
            e.Weight = p.GetValue<decimal>("Weight");
            e.Length = p.GetValue<decimal>("Length");
            e.Width = p.GetValue<decimal>("Width");
            e.Height = p.GetValue<decimal>("Height");
            e.AvailableStartDateTimeUtc = p.GetValue<DateTime?>("AvailableStartDateTimeUtc");
            e.AvailableEndDateTimeUtc = p.GetValue<DateTime?>("AvailableEndDateTimeUtc");
            e.BasePriceEnabled = p.GetValue<bool>("BasePriceEnabled");
            e.BasePriceMeasureUnit = p.GetString("BasePriceMeasureUnit");
            e.BasePriceAmount = p.GetValue<decimal?>("BasePriceAmount");
            e.BasePriceBaseAmount = p.GetValue<int?>("BasePriceBaseAmount");
            e.Visibility = (ProductVisibility)p.GetValue<int>("Visibility");
            e.Condition = (ProductCondition)p.GetValue<int>("Condition");
            e.DisplayOrder = p.GetValue("DisplayOrder", 1);
            e.IsSystemProduct = p.GetValue<bool>("IsSystemProduct");
            e.BundleTitleText = p.GetString("BundleTitleText");
            e.BundlePerItemPricing = p.GetValue<bool>("BundlePerItemPricing");
            e.BundlePerItemShipping = p.GetValue<bool>("BundlePerItemShipping");
            e.BundlePerItemShoppingCart = p.GetValue<bool>("BundlePerItemShoppingCart");
            e.LowestAttributeCombinationPrice = p.GetValue<decimal?>("LowestAttributeCombinationPrice");
            e.AttributeChoiceBehaviour = (AttributeChoiceBehaviour)p.GetValue<int>("AttributeChoiceBehaviour");
            e.IsEsd = p.GetValue<bool>("IsEsd");
            e.CustomsTariffNumber = p.GetString("CustomsTariffNumber");
            e.ImportCatalogId = p.GetValue<string>("ImportCatalogId");
            e.EClass = p.GetValue<string>("EClass");
            e.Supplier = p.GetValue<string>("Supplier");
            e.IsDangerousGood = p.GetValue<bool>("IsDangerousGood");

            if (cargo.IsNewEntity)
            {
                e.TaxCategoryId = cargo.State.TaxCategoryId;
                e.LimitedToStores = cargo.State.LimitedToStores;
                e.Published = cargo.State.Publish;
                e.HasTierPrices = false;
                e.HasDiscountsApplied = false;
            }

            e.QuantityUnitId = InsertQuantityUnit(cargo, p);
            e.DeliveryTimeId = InsertDeliveryTime(cargo, p);

            if (cargo.IsNewEntity)
            {
                _rsProduct.Insert(entity);
            }
            else
            {
                _rsProduct.Update(entity);
            }

            cargo.ProductIds.SafeAddId(id, e.Id);

            // Localization.
            UpsertUrlRecord(entity, product.GetString("SeName"), e.Name, true, 0);

            foreach (var language in cargo.Languages)
            {
                _localizedEntityService.SaveLocalizedValue(entity, x => x.Name, product.GetString(language, "Name"), language.Id);
                _localizedEntityService.SaveLocalizedValue(entity, x => x.ShortDescription, product.GetString(language, "ShortDescription"), language.Id);
                _localizedEntityService.SaveLocalizedValue(entity, x => x.FullDescription, product.GetString(language, "FullDescription"), language.Id);
                _localizedEntityService.SaveLocalizedValue(entity, x => x.MetaKeywords, product.GetString(language, "MetaKeywords"), language.Id);
                _localizedEntityService.SaveLocalizedValue(entity, x => x.MetaDescription, product.GetString(language, "MetaDescription"), language.Id);
                _localizedEntityService.SaveLocalizedValue(entity, x => x.MetaTitle, product.GetString(language, "MetaTitle"), language.Id);
                _localizedEntityService.SaveLocalizedValue(entity, x => x.BundleTitleText, product.GetString(language, "BundleTitleText"), language.Id);

                UpsertUrlRecord(entity, product.GetString(language, "SeName"), product.GetString(language, "Name"), false, language.Id);
            }

            // Store mapping.
            if (cargo.IsNewEntity && entity.LimitedToStores)
            {
                foreach (int storeId in cargo.State.SelectedStoreIds)
                {
                    _rsStoreMapping.Insert(new StoreMapping { EntityId = entity.Id, EntityName = "Product", StoreId = storeId });
                }
            }
        }

        private void ProcessProductCategories(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductCategory)))
            {
                return;
            }

            if (!cargo.CategoryIds.Any())
            {
                return;
            }

            foreach (XPathNavigator productCategory in product.Select("ProductCategories/ProductCategory"))
            {
                var categoryId = productCategory.SelectSingleNode("Category").GetValue("Id", 0);
                if (categoryId != 0 && cargo.CategoryIds.ContainsKey(categoryId))
                {
                    var destinationId = cargo.CategoryIds[categoryId].DestinationId;
                    if (destinationId != 0)
                    {
                        // No mapping to that category exists or product has no mapping at all.
                        var insert = cargo.ProductCategories.ContainsKey(entity.Id)
                            ? !cargo.ProductCategories[entity.Id].Contains(destinationId)
                            : true;

                        if (insert)
                        {
                            var isFeatured = productCategory.GetValue("IsFeaturedProduct", false);
                            var displayOrder = productCategory.GetValue("DisplayOrder", 0);

                            _rsProductCategory.Insert(new ProductCategory
                            {
                                ProductId = entity.Id,
                                CategoryId = destinationId,
                                IsFeaturedProduct = isFeatured,
                                DisplayOrder = displayOrder
                            });

                            cargo.ProductCategories.Add(entity.Id, destinationId);
                        }
                    }
                }
            }
        }

        private void ProcessManufacturers(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(Manufacturer)))
            {
                return;
            }

            foreach (XPathNavigator productManufacturer in product.Select("ProductManufacturers/ProductManufacturer"))
            {
                var manu = productManufacturer.SelectSingleNode("Manufacturer");
                var name = manu.GetString("Name");
                if (!name.HasValue())
                {
                    continue;
                }

                cargo.Manufacturers.TryGetValue(name, out var eManufacturer);
                var isNew = eManufacturer == null;

                if (eManufacturer == null)
                {
                    eManufacturer = new Manufacturer
                    {
                        Name = name,
                        Published = cargo.State.Publish,
                        DisplayOrder = 1,
                        ManufacturerTemplateId = 1,
                        CreatedOnUtc = cargo.UtcNow,
                        LimitedToStores = cargo.State.LimitedToStores
                    };

                    if (cargo.ReferenceManufacturer == null)
                    {
                        cargo.ReferenceManufacturer = cargo.Manufacturers.Values.FirstOrDefault() ?? _rsManufacturer.TableUntracked.FirstOrDefault();
                    }

                    if (cargo.ReferenceManufacturer != null)
                    {
                        eManufacturer.DisplayOrder = cargo.ReferenceManufacturer.DisplayOrder;
                        eManufacturer.ManufacturerTemplateId = cargo.ReferenceManufacturer.ManufacturerTemplateId;
                        eManufacturer.PageSize = cargo.ReferenceManufacturer.PageSize;
                        eManufacturer.AllowCustomersToSelectPageSize = cargo.ReferenceManufacturer.AllowCustomersToSelectPageSize;
                        eManufacturer.PageSizeOptions = cargo.ReferenceManufacturer.PageSizeOptions;
                    }

                    eManufacturer.MediaFileId = DownloadFile(cargo, manu);
                }

                eManufacturer.Description = manu.GetString("Description");
                eManufacturer.BottomDescription = manu.GetString("BottomDescription");
                eManufacturer.MetaKeywords = manu.GetString("MetaKeywords");
                eManufacturer.MetaDescription = manu.GetString("MetaDescription");
                eManufacturer.MetaTitle = manu.GetString("MetaTitle");

                if (isNew)
                {
                    _rsManufacturer.Insert(eManufacturer);

                    cargo.Manufacturers[name] = eManufacturer;
                }
                else
                {
                    _rsManufacturer.Update(eManufacturer);
                }

                // No mapping to that manufacturer exists or product has no mapping at all.
                var insert = cargo.ProductManufacturers.ContainsKey(entity.Id)
                    ? !cargo.ProductManufacturers[entity.Id].Contains(eManufacturer.Id)
                    : true;

                if (insert)
                {
                    _rsProductManufacturer.Insert(new ProductManufacturer
                    {
                        ProductId = entity.Id,
                        ManufacturerId = eManufacturer.Id,
                        DisplayOrder = 1
                    });

                    cargo.ProductManufacturers.Add(entity.Id, eManufacturer.Id);
                }

                UpsertUrlRecord(eManufacturer, manu.GetString("SeName"), eManufacturer.Name, true, 0);

                foreach (var language in cargo.Languages)
                {
                    _localizedEntityService.SaveLocalizedValue(eManufacturer, x => x.Name, manu.GetString(language, "Name"), language.Id);
                    _localizedEntityService.SaveLocalizedValue(eManufacturer, x => x.Description, manu.GetString(language, "Description"), language.Id);
                    _localizedEntityService.SaveLocalizedValue(eManufacturer, x => x.BottomDescription, manu.GetString(language, "BottomDescription"), language.Id);
                    _localizedEntityService.SaveLocalizedValue(eManufacturer, x => x.MetaKeywords, manu.GetString(language, "MetaKeywords"), language.Id);
                    _localizedEntityService.SaveLocalizedValue(eManufacturer, x => x.MetaDescription, manu.GetString(language, "MetaDescription"), language.Id);
                    _localizedEntityService.SaveLocalizedValue(eManufacturer, x => x.MetaTitle, manu.GetString(language, "MetaTitle"), language.Id);

                    UpsertUrlRecord(eManufacturer, manu.GetString(language, "SeName"), manu.GetString(language, "Name"), false, language.Id);
                }

                // Store mapping.
                if (isNew && eManufacturer.LimitedToStores)
                {
                    foreach (var storeId in cargo.State.SelectedStoreIds)
                    {
                        var storeMappings = _rsStoreMapping.Table.Count(x => x.EntityName == "Manufacturer" && x.EntityId == eManufacturer.Id && x.StoreId == storeId);

                        if (storeMappings <= 0)
                        {
                            _rsStoreMapping.Insert(new StoreMapping { EntityName = "Manufacturer", EntityId = eManufacturer.Id, StoreId = storeId });
                        }
                    }
                }
            }
        }

        private void ProcessProductPictures(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductMediaFile)))
            {
                return;
            }

            if (!cargo.Images.Any())
            {
                return;
            }

            var productPictures = cargo.ProductPictures.ContainsKey(entity.Id)
                ? cargo.ProductPictures[entity.Id]
                : Enumerable.Empty<ProductMediaFile>();

            var pictures = productPictures
                .Select(x => x.MediaFile)
                .ToList();

            var displayOrder = productPictures.Any()
                ? productPictures.Max(x => x.DisplayOrder)
                : 0;

            foreach (var image in cargo.Images.OrderBy(x => x.DisplayOrder).ToList())
            {
                if (image.Success ?? false)
                {
                    using (var stream = File.OpenRead(image.Path))
                    {
                        if (stream?.Length > 0)
                        {
                            MediaFile sourceFile = null;

                            if (_mediaService.FindEqualFile(stream, pictures, true, out var equalFile))
                            {
                                // Found equal image in existing product data.
                                // Keep the ID. May be required later when assigning image to a variant combination.
                                image.NewId = equalFile?.Id ?? 0;
                            }
                            else if (_mediaService.FindEqualFile(stream, image.FileName, cargo.CatalogAlbumId, true, out sourceFile))
                            {
                                // Found equal image in catalog album.
                            }
                            else
                            {
                                var path = _mediaService.CombinePaths(SystemAlbumProvider.Catalog, image.FileName);
                                sourceFile = _mediaService.SaveFile(path, stream, false, DuplicateFileHandling.Rename)?.File;
                            }

                            if (sourceFile?.Id > 0)
                            {
                                var productMediaFile = new ProductMediaFile
                                {
                                    ProductId = entity.Id,
                                    MediaFileId = sourceFile.Id,
                                    DisplayOrder = ++displayOrder
                                };

                                _rsProductPicture.Insert(productMediaFile);

                                image.NewId = sourceFile.Id;
                                cargo.ProductPictures.Add(entity.Id, productMediaFile);
                            }
                        }
                    }
                }
                else
                {
                    _logger.Info($"Image download failed ({image.Url.NaIfEmpty()}).".LoggerMessage(product));
                }
            }
        }

        private void ProcessProductTags(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductTag)))
            {
                return;
            }

            List<int> productTagIds = null;

            foreach (XPathNavigator tag in product.Select("ProductTags/ProductTag"))
            {
                var name = tag.GetString("Name");
                if (name.IsEmpty())
                {
                    continue;
                }

                if (productTagIds == null)
                {
                    productTagIds = _rsProductTag.TableUntracked.Expand(x => x.Products)
                        .Where(x => x.Products.Any(y => y.Id == entity.Id))
                        .Select(x => x.Id)
                        .ToList();
                }

                cargo.ProductTags.TryGetValue(name, out var eTag);

                if (eTag == null)
                {
                    eTag = new ProductTag { Name = name };

                    _rsProductTag.Insert(eTag);

                    cargo.ProductTags[name] = eTag;
                }

                if (!productTagIds.Any(x => x == eTag.Id))
                {
                    entity.ProductTags.Add(eTag);
                    _productService.UpdateProduct(entity);
                }

                foreach (var language in cargo.Languages)
                {
                    _localizedEntityService.SaveLocalizedValue(eTag, x => x.Name, tag.GetString(language, "Name"), language.Id);
                }
            }
        }

        private void ProcessTierPrices(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(TierPrice)))
            {
                return;
            }

            if (!cargo.IsNewEntity)
                return;

            BulkCommit(_rsTierPrice, () =>
            {
                foreach (XPathNavigator tp in product.Select("TierPrices/TierPrice"))
                {
                    var tierPrice = new TierPrice
                    {
                        ProductId = entity.Id,
                        StoreId = 0,
                        CustomerRoleId = null,
                        Quantity = tp.GetValue<int>("Quantity"),
                        Price = tp.GetValue<decimal>("Price"),
                        CalculationMethod = (TierPriceCalculationMethod)tp.GetValue<int>("CalculationMethod")
                    };

                    _rsTierPrice.Insert(tierPrice);
                }
            });
        }

        private void ProcessProductSpecificationAttributes(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(SpecificationAttribute)))
            {
                return;
            }

            var productSpecAttributes = new List<ProductSpecificationAttribute>();

            foreach (XPathNavigator pca in product.Select("ProductSpecificationAttributes/ProductSpecificationAttribute"))
            {
                var attribute = pca.SelectSingleNode("SpecificationAttributeOption/SpecificationAttribute");
                var attributeId = attribute.GetValue<int>("Id");
                var name = attribute.GetString("Name");
                var alias = attribute.GetString("Alias");
                int eAttributeId;

                if (name.IsEmpty())
                {
                    continue;
                }

                /// <seealso cref="GetExistingProducts"/>
                var key = string.Concat(name.EmptyNull(), "|", alias.EmptyNull());

                if (!cargo.SpecAttributes.TryGetValue(key, out eAttributeId))
                {
                    var eAttribute = new SpecificationAttribute
                    {
                        Name = name,
                        Alias = alias,
                        DisplayOrder = attribute.GetValue("DisplayOrder", 1),
                        AllowFiltering = attribute.GetValue<bool>("AllowFiltering"),
                        ShowOnProductPage = attribute.GetValue<bool>("ShowOnProductPage"),
                        FacetSorting = (FacetSorting)attribute.GetValue<int>("FacetSorting"),
                        FacetTemplateHint = (FacetTemplateHint)attribute.GetValue<int>("FacetTemplateHint"),
                        IndexOptionNames = attribute.GetValue<bool>("IndexOptionNames")
                    };

                    _rsSpecAttribute.Insert(eAttribute);

                    eAttributeId = eAttribute.Id;
                    cargo.SpecAttributes[key] = eAttributeId;

                    foreach (var language in cargo.Languages)
                    {
                        _localizedEntityService.SaveLocalizedValue(eAttribute, x => x.Name, attribute.GetString(language, "Name"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(eAttribute, x => x.Alias, attribute.GetString(language, "Alias"), language.Id);
                    }
                }

                // Ignore attributes that are not unique by name and alias.
                if (eAttributeId == 0)
                {
                    continue;
                }

                var option = pca.SelectSingleNode("SpecificationAttributeOption");
                var optionId = option.GetValue<int>("Id");
                var optionName = option.GetString("Name");

                if (optionName.IsEmpty())
                {
                    continue;
                }

                var optionKey = $"{eAttributeId}|{optionName}";

                if (!cargo.SpecAttributeOptions.TryGetValue(optionKey, out var eOptionId))
                {
                    var eOption = new SpecificationAttributeOption
                    {
                        SpecificationAttributeId = eAttributeId,
                        Name = optionName,
                        Alias = option.GetString("Alias"),
                        DisplayOrder = option.GetValue("DisplayOrder", 1),
                        NumberValue = option.GetValue<decimal>("NumberValue"),
                        Color = option.GetString("Color")
                    };

                    _rsSpecAttributeOption.Insert(eOption);

                    eOptionId = eOption.Id;
                    cargo.SpecAttributeOptions[optionKey] = eOption.Id;

                    foreach (var language in cargo.Languages)
                    {
                        _localizedEntityService.SaveLocalizedValue(eOption, x => x.Name, option.GetString(language, "Name"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(eOption, x => x.Alias, option.GetString(language, "Alias"), language.Id);
                    }
                }

                var insert = cargo.ProductSpecAttributes.ContainsKey(entity.Id)
                    ? !cargo.ProductSpecAttributes[entity.Id].Contains(eOptionId)
                    : true;

                if (insert)
                {
                    productSpecAttributes.Add(new ProductSpecificationAttribute
                    {
                        ProductId = entity.Id,
                        SpecificationAttributeOptionId = eOptionId,
                        AllowFiltering = pca.GetValue<bool?>("AllowFiltering"),
                        DisplayOrder = pca.GetValue("DisplayOrder", 1),
                        ShowOnProductPage = pca.GetValue<bool?>("ShowOnProductPage")
                    });

                    cargo.ProductSpecAttributes.Add(entity.Id, eOptionId);
                }
            }

            if (productSpecAttributes.Any())
            {
                BulkCommit(_rsProductSpecAttribute, () =>
                {
                    foreach (var psa in productSpecAttributes)
                    {
                        _rsProductSpecAttribute.Insert(psa);
                    }
                });
            }
        }

        private void ProcessProductAttributes(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductAttribute)))
            {
                return;
            }

            foreach (XPathNavigator pa in product.Select("ProductAttributes/ProductAttribute"))
            {
                // Attribute.
                var attribute = pa.SelectSingleNode("Attribute");
                var name = attribute.GetString("Name");
                var alias = attribute.GetString("Alias");
                int eAttributeId;

                if (name.IsEmpty())
                {
                    continue;
                }

                var key = string.Concat(name.EmptyNull(), "|", alias.EmptyNull());

                if (!cargo.Attributes.TryGetValue(key, out eAttributeId))
                {
                    var eAttribute = new ProductAttribute
                    {
                        Name = name,
                        Alias = alias,
                        Description = attribute.GetString("Description"),
                        AllowFiltering = attribute.GetValue("AllowFiltering", true),
                        DisplayOrder = attribute.GetValue<int>("DisplayOrder"),
                        FacetTemplateHint = (FacetTemplateHint)attribute.GetValue<int>("FacetTemplateHint"),
                        IndexOptionNames = attribute.GetValue<bool>("IndexOptionNames")
                    };

                    _rsProductAttribute.Insert(eAttribute);

                    eAttributeId = eAttribute.Id;
                    cargo.Attributes[key] = eAttributeId;

                    foreach (var language in cargo.Languages)
                    {
                        _localizedEntityService.SaveLocalizedValue(eAttribute, x => x.Name, attribute.GetString(language, "Name"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(eAttribute, x => x.Description, attribute.GetString(language, "Description"), language.Id);
                    }
                }

                // Ignore attributes that are not unique by name and alias.
                if (eAttributeId == 0)
                {
                    continue;
                }

                // Product attribute mapping.
                var mappingKey = $"{entity.Id}|{eAttributeId}";
                if (!cargo.ProductAttributes.TryGetValue(mappingKey, out var eAttributeMappingId))
                {
                    var eAttributeMapping = new ProductVariantAttribute
                    {
                        ProductId = entity.Id,
                        ProductAttributeId = eAttributeId,
                        TextPrompt = pa.GetString("TextPrompt"),
                        IsRequired = pa.GetValue<bool>("IsRequired"),
                        AttributeControlTypeId = pa.GetValue<int>("AttributeControlTypeId"),
                        DisplayOrder = pa.GetValue<int>("DisplayOrder")
                    };

                    _rsProductVariantAttribute.Insert(eAttributeMapping);

                    eAttributeMappingId = eAttributeMapping.Id;
                    cargo.ProductAttributes[mappingKey] = eAttributeMapping.Id;
                }

                if (eAttributeMappingId == 0)
                    continue;

                cargo.AttributeMappingIds[pa.GetValue<int>("Id")] = eAttributeMappingId;

                // Attribute values.
                foreach (XPathNavigator pav in pa.Select("AttributeValues/AttributeValue"))
                {
                    var valueName = pav.GetString("Name");
                    if (valueName.IsEmpty())
                        continue;

                    var valueKey = $"{eAttributeMappingId}|{valueName}";
                    if (!cargo.ProductAttributeValues.TryGetValue(valueKey, out var eValue))
                    {
                        eValue = new ProductVariantAttributeValue
                        {
                            ProductVariantAttributeId = eAttributeMappingId,
                            Name = valueName,
                            Alias = pav.GetString("Alias"),
                            Color = pav.GetString("Color") ?? pav.GetString("ColorSquaresRgb"),
                            PriceAdjustment = pav.GetValue<decimal>("PriceAdjustment"),
                            WeightAdjustment = pav.GetValue<decimal>("WeightAdjustment"),
                            IsPreSelected = pav.GetValue<bool>("IsPreSelected"),
                            DisplayOrder = pav.GetValue<int>("DisplayOrder"),
                            ValueTypeId = pav.GetValue<int>("ValueTypeId"),
                            Quantity = pav.GetValue<int>("Quantity")
                        };

                        _rsProductVariantAttributeValue.Insert(eValue);

                        cargo.ProductAttributeValues[valueKey] = eValue;
                    }

                    if ((eValue?.Id ?? 0) == 0)
                        continue;

                    var linkedProductId = pav.GetValue<int>("LinkedProductId");
                    if (linkedProductId != 0)
                    {
                        // ID of linked product is unknown here. We must process it later.
                        cargo.LinkedProductIds.Add(linkedProductId, eValue.Id);
                    }

                    cargo.AttributeValueIds[pav.GetValue<int>("Id")] = eValue.Id;

                    foreach (var language in cargo.Languages)
                    {
                        _localizedEntityService.SaveLocalizedValue(eValue, x => x.Name, pav.GetString(language, "Name"), language.Id);
                    }
                }
            }
        }

        private void ProcessProductAttributeCombinations(CargoObjects cargo, XPathNavigator product, Product entity)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductVariantAttributeCombination)))
            {
                return;
            }

            // Only process for inserted products.
            if (!cargo.IsNewEntity)
                return;

            var assignedPictureIds = new List<int>();

            // Insert quantity units.
            foreach (XPathNavigator pac in product.Select("ProductAttributeCombinations/ProductAttributeCombination"))
            {
                var unused = InsertQuantityUnit(cargo, pac);
            }

            BulkCommit(_rsProductVariantAttributeCombination, () =>
            {
                foreach (XPathNavigator pac in product.Select("ProductAttributeCombinations/ProductAttributeCombination"))
                {
                    var eCombination = new ProductVariantAttributeCombination
                    {
                        ProductId = entity.Id,
                        StockQuantity = pac.GetValue("StockQuantity", 10000),
                        AllowOutOfStockOrders = pac.GetValue<bool>("AllowOutOfStockOrders"),
                        Sku = pac.GetString("Sku"),
                        Gtin = pac.GetString("Gtin"),
                        ManufacturerPartNumber = pac.GetString("ManufacturerPartNumber"),
                        Price = pac.GetValue<decimal?>("Price"),
                        Length = pac.GetValue<decimal?>("Length"),
                        Width = pac.GetValue<decimal?>("Width"),
                        Height = pac.GetValue<decimal?>("Height"),
                        BasePriceAmount = pac.GetValue<decimal?>("BasePriceAmount"),
                        BasePriceBaseAmount = pac.GetValue<int?>("BasePriceBaseAmount"),
                        DeliveryTimeId = null,
                        IsActive = pac.GetValue<bool>("IsActive")
                    };

                    eCombination.QuantityUnitId = InsertQuantityUnit(cargo, pac);
                    eCombination.AttributesXml = CreateAttributeXml(cargo, pac.GetString("AttributesXml"));

                    // Assigned pictures.
                    assignedPictureIds.Clear();
                    foreach (XPathNavigator picture in pac.Select("Pictures/Picture"))
                    {
                        var pictureId = picture.GetValue<int>("Id");
                        var image = cargo.Images.FirstOrDefault(x => x.Id == pictureId && x.Id != 0 && x.Success == true);

                        if (image != null)
                            assignedPictureIds.Add(image.NewId);
                    }

                    eCombination.SetAssignedMediaIds(assignedPictureIds.ToArray());

                    _rsProductVariantAttributeCombination.Insert(eCombination);
                }
            });
        }

        private void ProcessLinkedProducts(CargoObjects cargo, int id)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductAttribute)))
            {
                return;
            }

            // Find attribute values this product is linked to.
            if (cargo.LinkedProductIds.ContainsKey(id))
            {
                var attributeValueIds = cargo.LinkedProductIds[id];

                if (attributeValueIds.Any() && cargo.ProductIds.TryGetValue(id, out var eProductId) && eProductId != 0)
                {
                    var attributeValues = _rsProductVariantAttributeValue.Table
                        .Where(x => attributeValueIds.Contains(x.Id))
                        .ToList();

                    attributeValues.Each(x => x.LinkedProductId = eProductId);

                    _rsProductVariantAttributeValue.UpdateRange(attributeValues);
                }
            }
        }

        private void ProcessBundleItems(CargoObjects cargo, XPathNavigator product, int id)
        {
            if (cargo.State.IgnoreEntityNames.Contains(nameof(ProductBundleItem)))
            {
                return;
            }

            int eBundleId, eProductId;
            var productTypeId = product.GetValue<int>("ProductTypeId");

            if (productTypeId != (int)ProductType.BundledProduct)
                return;

            if (!cargo.ProductIds.TryGetValue(id, out eBundleId) || eBundleId == 0)
                return;

            var bundleItems = _rsProductBundleItem.Table
                .Where(x => x.BundleProductId == eBundleId)
                .ToList()
                .ToMultimap(x => x.ProductId, x => x);

            foreach (XPathNavigator item in product.Select("ProductBundleItems/ProductBundleItem"))
            {
                if (cargo.ProductIds.TryGetValue(item.GetValue<int>("ProductId"), out eProductId) && eProductId != 0)
                {
                    var items = bundleItems.ContainsKey(eProductId)
                        ? bundleItems[eProductId]
                        : Enumerable.Empty<ProductBundleItem>();

                    var itemCount = items.Count();
                    var isNew = itemCount == 0;
                    ProductBundleItem bundleItem = null;

                    if (itemCount == 0)
                    {
                        bundleItem = new ProductBundleItem
                        {
                            ProductId = eProductId,
                            BundleProductId = eBundleId
                        };
                    }
                    else if (itemCount == 1)
                    {
                        bundleItem = items.First();
                    }

                    if (bundleItem != null)
                    {
                        bundleItem.Quantity = item.GetValue("Quantity", 1);
                        bundleItem.Discount = item.GetValue<decimal?>("Discount");
                        bundleItem.DiscountPercentage = item.GetValue<bool>("DiscountPercentage");
                        bundleItem.Name = item.GetString("Name");
                        bundleItem.ShortDescription = item.GetString("ShortDescription");
                        // FilterAttributes not supported
                        bundleItem.HideThumbnail = item.GetValue<bool>("HideThumbnail");
                        bundleItem.Visible = item.GetValue<bool>("Visible");
                        bundleItem.Published = item.GetValue<bool>("Published");
                        bundleItem.DisplayOrder = item.GetValue<int>("DisplayOrder");

                        if (isNew)
                        {
                            _rsProductBundleItem.Insert(bundleItem);
                        }
                        else
                        {
                            _rsProductBundleItem.Update(bundleItem);
                        }
                    }
                }
            }
        }

        private void ProcessGroupedProducts(CargoObjects cargo, XPathNavigator product, int id)
        {
            var parentId = product.GetValue<int>("ParentGroupedProductId");
            if (parentId != 0 && cargo.ProductIds.TryGetValue(parentId, out int eParentId) && eParentId != 0)
            {
                if (cargo.ProductIds.TryGetValue(id, out int eId) && eId != 0)
                {
                    var entity = _rsProduct.Table.FirstOrDefault(x => x.Id == eId);
                    if (entity != null)
                    {
                        entity.ParentGroupedProductId = eParentId;
                        _rsProduct.Update(entity);
                    }
                }
            }
        }

        private void ProcessRequiredProducts(CargoObjects cargo, XPathNavigator product, int id)
        {
            if (product.GetValue<bool>("RequireOtherProducts"))
            {
                var ids = new List<int>();
                foreach (int requiredId in product.GetString("RequiredProductIds").SplitSafe(",").Select(x => x.ToInt()))
                {
                    if (cargo.ProductIds.TryGetValue(requiredId, out int eRequiredId) && eRequiredId != 0)
                    {
                        ids.Add(eRequiredId);
                    }
                }

                if (ids.Any())
                {
                    if (cargo.ProductIds.TryGetValue(id, out int eId) && eId != 0)
                    {
                        var entity = _rsProduct.Table.FirstOrDefault(x => x.Id == eId);
                        if (entity != null)
                        {
                            entity.RequiredProductIds = string.Join(",", ids);
                            _rsProduct.Update(entity);
                        }
                    }
                }
            }
        }

        private void ImportCategoryBranch(CargoObjects cargo, XPathNavigator nav, IProgress<ShopConnectorProcessingInfo> progress, CancellationTokenSource cts, int parentId, int destinationParentId)
        {
            if (cargo.Stats.RecordsCount >= 100)
            {
                cargo.Stats.RecordsCount = 0;
                _rsCategory.Context.DetachAll(false);
            }

            var existingCategories = _rsCategory.Table
                .Where(x => x.ParentCategoryId == destinationParentId && !x.Deleted)
                .ToList()
                .ToDictionarySafe(x => x.Name, x => x);

            foreach (var kvp in cargo.CategoryIds.Where(x => x.Value.ParentId == parentId))
            {
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                var category = nav.SelectSingleNode($"Category[Id = {kvp.Key}]");
                if (category == null)
                {
                    ++cargo.Stats.Skipped;
                    _logger.Warn("Category with Id {0} not found.".FormatInvariant(kvp.Key));
                    continue;
                }

                var name = category.GetString("Name");
                if (name.IsEmpty())
                {
                    ++cargo.Stats.Skipped;
                    _logger.Error("Category with Id {0} has no name.".FormatInvariant(kvp.Key));
                    continue;
                }

                cargo.IsNewEntity = false;

                ++cargo.Stats.RecordsCount;
                progress.Report(cargo.Stats);

                var processEntity = true;
                existingCategories.TryGetValue(name, out var entity);

                if (entity == null)
                {
                    if (cargo.State.ImportCategories)
                    {
                        cargo.IsNewEntity = true;

                        entity = new Category
                        {
                            Name = name,
                            ParentCategoryId = destinationParentId,
                            Published = cargo.State.Publish,
                            LimitedToStores = cargo.State.LimitedToStores,
                            PageSize = category.GetValue<int?>("PageSize"),
                            AllowCustomersToSelectPageSize = category.GetValue<bool?>("AllowCustomersToSelectPageSize"),
                            PageSizeOptions = category.GetString("PageSizeOptions"),
                            ShowOnHomePage = category.GetValue("ShowOnHomePage", false),
                            DisplayOrder = category.GetValue("DisplayOrder", 0),
                            DefaultViewMode = category.GetString("DefaultViewMode")
                        };

                        var templateViewPath = category.GetString("CategoryTemplateViewPath");
                        if (cargo.CategoryTemplates.ContainsKey(templateViewPath))
                        {
                            entity.CategoryTemplateId = cargo.CategoryTemplates[templateViewPath];
                        }
                        else if (cargo.CategoryTemplates.ContainsKey("CategoryTemplate.ProductsInGridOrLines"))
                        {
                            entity.CategoryTemplateId = cargo.CategoryTemplates["CategoryTemplate.ProductsInGridOrLines"];
                        }

                        entity.MediaFileId = DownloadFile(cargo, category);
                    }
                    else
                    {
                        processEntity = false;
                    }
                }
                else
                {
                    if (!cargo.State.UpdateExistingCategories)
                    {
                        processEntity = false;
                    }
                }

                if (processEntity)
                {
                    entity.Alias = category.GetString("Alias");
                    entity.FullName = category.GetString("FullName");
                    entity.Description = category.GetString("Description");
                    entity.BottomDescription = category.GetString("BottomDescription");
                    entity.MetaKeywords = category.GetString("MetaKeywords");
                    entity.MetaDescription = category.GetString("MetaDescription");
                    entity.MetaTitle = category.GetString("MetaTitle");

                    if (cargo.IsNewEntity)
                    {
                        _rsCategory.Insert(entity);
                        ++cargo.Stats.Added;
                    }
                    else
                    {
                        _rsCategory.Update(entity);
                        ++cargo.Stats.Updated;
                    }

                    ++cargo.Stats.Success;

                    UpsertUrlRecord(entity, category.GetString("SeName"), name, true, 0);

                    // localization
                    foreach (var language in cargo.Languages)
                    {
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.Name, category.GetString(language, "Name"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.FullName, category.GetString(language, "FullName"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.Description, category.GetString(language, "Description"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.BottomDescription, category.GetString(language, "BottomDescription"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.MetaKeywords, category.GetString(language, "MetaKeywords"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.MetaDescription, category.GetString(language, "MetaDescription"), language.Id);
                        _localizedEntityService.SaveLocalizedValue(entity, x => x.MetaTitle, category.GetString(language, "MetaTitle"), language.Id);

                        UpsertUrlRecord(entity, category.GetString(language, "SeName"), category.GetString(language, "Name"), false, language.Id);
                    }

                    // store mapping
                    if (cargo.IsNewEntity && entity.LimitedToStores)
                    {
                        foreach (var storeId in cargo.State.SelectedStoreIds)
                        {
                            var storeMappings = _rsStoreMapping.Table.Count(x => x.EntityName == "Category" && x.EntityId == entity.Id && x.StoreId == storeId);
                            if (storeMappings <= 0)
                            {
                                _rsStoreMapping.Insert(new StoreMapping { EntityId = entity.Id, EntityName = "Category", StoreId = storeId });
                            }
                        }
                    }
                }
                else
                {
                    ++cargo.Stats.Skipped;
                    //_logger.Info("Category {0} »{1}«. Skipped by option.".FormatInvariant(kvp.Key, name.NaIfEmpty()));
                }

                ++cargo.Stats.TotalProcessed;

                if (entity != null && entity.Id != 0)
                {
                    kvp.Value.DestinationId = entity.Id;

                    ImportCategoryBranch(cargo, nav, progress, cts, kvp.Key, entity.Id);
                }
            }
        }

        private void LoadCargoData(CargoObjects cargo)
        {
            var duplicateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Ensure uniqueness by name and alias.
            var specAttributes = _rsSpecAttribute.TableUntracked
                .Select(x => new { x.Id, x.Name, x.Alias })
                .ToList();

            foreach (var sa in specAttributes)
            {
                var name = sa.Name.EmptyNull();
                var key = string.Concat(name, "|", sa.Alias.EmptyNull());

                // Return Id = 0 and ignore the attribute if its not unique.
                var id = cargo.SpecAttributes.ContainsKey(key) ? 0 : sa.Id;
                cargo.SpecAttributes[key] = id;

                if (id == 0)
                {
                    duplicateNames.Add(name);
                }
            }

            if (duplicateNames.Any())
            {
                _logger.Warn("No uniqueness by name and alias. Ignoring duplicate specification attributes: " + string.Join(", ", duplicateNames));
            }

            specAttributes.Clear();
            duplicateNames.Clear();

            var productAttributes = _rsProductAttribute.TableUntracked
                .Select(x => new { x.Id, x.Name, x.Alias })
                .ToList();

            foreach (var pa in productAttributes)
            {
                var name = pa.Name.EmptyNull();
                var key = string.Concat(name, "|", pa.Alias.EmptyNull());

                var id = cargo.Attributes.ContainsKey(key) ? 0 : pa.Id;
                cargo.Attributes[key] = id;

                if (id == 0)
                {
                    duplicateNames.Add(name);
                }
            }

            if (duplicateNames.Any())
            {
                _logger.Warn("No uniqueness by name and alias. Ignoring duplicate product attributes: " + string.Join(", ", duplicateNames));
            }
        }

        public void StartProductImport(ShopConnectorImportState state)
        {
            var importStartTime = DateTime.UtcNow;
            string resultInfoProducts = null;
            string resultInfoCategories = null;

            IProgress<ShopConnectorProcessingInfo> progress = new Progress<ShopConnectorProcessingInfo>(handler =>
            {
                _asyncState.Set(handler, ShopConnectorPlugin.SystemName);
            });

            try
            {
                _logger = new TraceLogger(ShopConnectorFileSystem.ImportLogFile(true));
                _logger.Info("Shop-Connector product import has been started.");

                if (state.IgnoreEntityNames.Any())
                {
                    _logger.Info("Ignoring entities: " + string.Join(", ", state.IgnoreEntityNames));
                }

                var cargo = new CargoObjects
                {
                    UtcNow = DateTime.UtcNow,
                    State = state,
                    Stats = new ShopConnectorProcessingInfo(),
                    DownloadContext = new FileDownloadManagerContext(),
                    ImageDirectory = ShopConnectorFileSystem.GetDirectory("Image"),
                    Files = new ShopConnectorFileSystem("Product"),
                    Images = new List<FileDownloadManagerItem>(),
                    ProductIds = new Dictionary<int, int>(),
                    AttributeValueIds = new Dictionary<int, int>(),
                    AttributeMappingIds = new Dictionary<int, int>(),
                    CategoryIds = new Dictionary<int, ShopConnectorImportCategory>(),
                    LinkedProductIds = new Multimap<int, int>(),
                    ProductsBySku = new Dictionary<string, Product>(),
                    ProductsByGtin = new Dictionary<string, Product>(),
                    ProductCategories = new Multimap<int, int>(),
                    Manufacturers = new Dictionary<string, Manufacturer>(),
                    ProductManufacturers = new Multimap<int, int>(),
                    ProductPictures = new Multimap<int, ProductMediaFile>(),
                    ProductTags = new Dictionary<string, ProductTag>(),
                    SpecAttributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    SpecAttributeOptions = new Dictionary<string, int>(),
                    ProductSpecAttributes = new Multimap<int, int>(),
                    Attributes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    ProductAttributes = new Dictionary<string, int>(),
                    ProductAttributeValues = new Dictionary<string, ProductVariantAttributeValue>()
                };

                string[] processingDescription = T("Plugins.SmartStore.ShopConnector.ProcessingDescription").Text.SplitSafe(";");
                cargo.Stats.Content = processingDescription.SafeGet(3);
                progress.Report(cargo.Stats);

                cargo.ProductTemplates = _productTemplateService.GetAllProductTemplates().ToDictionarySafe(x => x.ViewPath, x => x.Id);
                cargo.CategoryTemplates = _categoryTemplateService.GetAllCategoryTemplates().ToDictionarySafe(x => x.ViewPath, x => x.Id);
                cargo.Languages = _languageService.GetAllLanguages(true);
                cargo.QuantityUnits = _quantityUnitService.GetAllQuantityUnits().ToDictionarySafe(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
                cargo.DeliveryTimes = _deliveryTimeService.GetAllDeliveryTimes().ToDictionarySafe(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
                cargo.CatalogAlbumId = _folderService.GetNodeByPath(SystemAlbumProvider.Catalog).Value.Id;

                cargo.DownloadContext.Timeout = TimeSpan.FromMinutes(_shopConnectorSettings.ImageDownloadTimeout);
                cargo.DownloadContext.Logger = _logger;
                cargo.Stats.Content = T("Plugins.SmartStore.ShopConnector.RunningResult");

                _mediaService.ImagePostProcessingEnabled = false;

                var xmlPath = cargo.Files.GetFullFilePath(state.ImportFile);
                var fileStats = new ShopConnectorImportStats("Product").Get(state.ImportFile, true);
                var additionalDataBatches = new Multimap<string, AdditionalXmlData>();

                using (var cts = new CancellationTokenSource())
                using (var scope = new DbContextScope(ctx: _rsProduct.Context, hooksEnabled: false, autoDetectChanges: true, proxyCreation: false, validateOnSave: false))
                {
                    _asyncState.SetCancelTokenSource<ShopConnectorProcessingInfo>(cts, ShopConnectorPlugin.SystemName);

                    using (var reader = XmlReader.Create(xmlPath, new XmlReaderSettings { CheckCharacters = false }))
                    {
                        if (state.ImportCategories || state.UpdateExistingCategories)
                        {
                            reader.ReadFragments("Categories", "Category", false, nav =>
                            {
                                var categories = nav.Select("Category");
                                cargo.Stats.Reset(processingDescription.SafeGet(2), categories.Count);

                                foreach (XPathNavigator category in categories)
                                {
                                    cargo.CategoryIds[category.GetValue("Id", 0)] = new ShopConnectorImportCategory { ParentId = category.GetValue("ParentCategoryId", 0) };
                                }

                                if (cargo.CategoryIds.Any())
                                {
                                    // Import categories by branch.
                                    ImportCategoryBranch(cargo, nav, progress, cts, 0, 0);
                                }

                                return true;
                            });

                            resultInfoCategories = cargo.Stats.Format(T("Plugins.SmartStore.ShopConnector.FinalResult"), processingDescription.SafeGet(2));
                        }

                        // Import products by fragments.
                        cargo.Stats.Reset(processingDescription.SafeGet(0), fileStats.ProductCount);

                        // Load global product cargo data.
                        LoadCargoData(cargo);

                        reader.ReadFragments("Products", "Product", true, nav =>
                        {
                            // Detach for each XML fragment of 100 products.
                            _rsProduct.Context.DetachAll(false);

                            var stop = false;
                            GetExistingProducts(cargo, nav);

                            foreach (XPathNavigator product in nav.Select("Product"))
                            {
                                try
                                {
                                    ++cargo.Stats.RecordsCount;
                                    progress.Report(cargo.Stats);

                                    var id = product.GetValue<int>("Id");
                                    //"1. {0} {1}: {2} {3}".FormatInvariant(cargo.Stats.TotalProcessed + 1, IsProductSelected(state, product, id), id, product.GetString("Name")).Dump();
                                    if (state.IsSelected(id))
                                    {
                                        cargo.Images.Clear();
                                        var entity = GetEntity(cargo, product);

                                        if (entity != null)
                                        {
                                            Task imageDownload = null;

                                            foreach (XPathNavigator picture in product.Select("ProductPictures/ProductPicture"))
                                            {
                                                cargo.Images.AddDownloadItem(picture.SelectSingleNode("Picture").ToDownloadItem(_services, cargo.ImageDirectory, picture.GetValue("DisplayOrder", 0)));
                                            }

                                            if (cargo.Images.Any())
                                            {
                                                imageDownload = _fileDownloadManager.DownloadAsync(cargo.DownloadContext, cargo.Images);
                                            }

                                            ProcessProduct(cargo, product, entity, id);
                                            ProcessProductCategories(cargo, product, entity);
                                            ProcessManufacturers(cargo, product, entity);
                                            ProcessProductTags(cargo, product, entity);
                                            ProcessTierPrices(cargo, product, entity);
                                            ProcessProductSpecificationAttributes(cargo, product, entity);
                                            ProcessProductAttributes(cargo, product, entity);

                                            if (imageDownload != null)
                                            {
                                                imageDownload.Wait();
                                            }

                                            ProcessProductPictures(cargo, product, entity);
                                            ProcessProductAttributeCombinations(cargo, product, entity);

                                            // Collect additional data and process it in batch, not row.
                                            foreach (XPathNavigator additionalDataNode in product.Select("AdditionalData/*"))
                                            {
                                                if (additionalDataNode != null)
                                                {
                                                    var pluginName = additionalDataNode.Name;

                                                    if (additionalDataBatches.ContainsKey(pluginName))
                                                    {
                                                        additionalDataBatches[pluginName].Add(new AdditionalXmlData(id, entity.Id, additionalDataNode));
                                                    }
                                                    else
                                                    {
                                                        additionalDataBatches.Add(pluginName, new AdditionalXmlData(id, entity.Id, additionalDataNode));
                                                    }

                                                    if (additionalDataBatches[pluginName].Count >= 200)
                                                    {
                                                        // Send additional data to consumer.
                                                        var data = additionalDataBatches[pluginName].ToDictionarySafe(x => x.ExportedEntityId, x => x);
                                                        _services.EventPublisher.Publish(new XmlImportedEvent(data, pluginName));
                                                        additionalDataBatches[pluginName].Clear(); // Batch was processed > clear.
                                                        // The remaining data items will be processed when products loop is finished.
                                                    }
                                                }
                                            }

                                            if (cargo.IsNewEntity)
                                            {
                                                ++cargo.Stats.Added;
                                            }
                                            else
                                            {
                                                ++cargo.Stats.Updated;
                                            }

                                            ++cargo.Stats.Success;
                                        }
                                    }
                                    else
                                    {
                                        ++cargo.Stats.Skipped;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ++cargo.Stats.Failure;
                                    _logger.Error(ex, ex.Message.LoggerMessage(product));
                                }

                                ++cargo.Stats.TotalProcessed;
                                stop = cts.IsCancellationRequested;
                                if (stop) break;
                            }
                            return !stop;
                        }); // Product fragments.
                    }   // XmlReader.


                    // Send remaining items to consumers.
                    foreach (var batch in additionalDataBatches)
                    {
                        var data = additionalDataBatches[batch.Key].ToDictionarySafe(x => x.ExportedEntityId, x => x);
                        _services.EventPublisher.Publish(new XmlImportedEvent(data, batch.Key));
                    }

                    cargo.ClearFragmentData();
                    var deleteImportFile = state.DeleteImportFile && cargo.Stats.Failure == 0;
                    resultInfoProducts = cargo.Stats.Format(T("Plugins.SmartStore.ShopConnector.FinalResult"), processingDescription.SafeGet(0));

                    // Second run for entity assignments.
                    cargo.Stats.Reset(processingDescription.SafeGet(1), cargo.Stats.RecordsCount);

                    using (var reader = XmlReader.Create(xmlPath, new XmlReaderSettings { CheckCharacters = false }))
                    {
                        reader.ReadFragments("Products", "Product", true, nav =>
                        {
                            _rsProduct.Context.DetachAll(false);

                            foreach (XPathNavigator product in nav.Select("Product"))
                            {
                                try
                                {
                                    progress.Report(cargo.Stats);
                                    var id = product.GetValue<int>("Id");
                                    //"2. {0} {1}: {2} {3}".FormatInvariant(cargo.Stats.TotalProcessed + 1, IsProductSelected(state, product, id), id, product.GetString("Name")).Dump();
                                    if (state.IsSelected(id))
                                    {
                                        ProcessLinkedProducts(cargo, id);
                                        ProcessBundleItems(cargo, product, id);
                                        ProcessGroupedProducts(cargo, product, id);
                                        ProcessRequiredProducts(cargo, product, id);
                                        // TODO: RelatedProduct, CrossSellProduct...

                                        ++cargo.Stats.Success;
                                    }
                                    else
                                    {
                                        ++cargo.Stats.Skipped;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ++cargo.Stats.Failure;
                                    _logger.Error(ex, ex.Message.LoggerMessage(product));
                                }

                                ++cargo.Stats.TotalProcessed;
                            }

                            return !cts.IsCancellationRequested;
                        }); // Product fragments.
                    }   // XmlReader.

                    if (deleteImportFile && cargo.Stats.Failure == 0)
                    {
                        FileSystemHelper.DeleteFile(xmlPath);
                    }
                    if (cts.IsCancellationRequested)
                    {
                        _logger.Info("Import has been cancelled!");
                    }
                }   // DbScope.
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            finally
            {
                try
                {
                    _mediaService.ImagePostProcessingEnabled = true;

                    // PostProcess: normalization.
                    DataMigrator.FixProductMainPictureIds(_services.DbContext, importStartTime);

                    // Hooks are disabled but category tree may have changed.
                    _services.Cache.Clear();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }

                _asyncState.Remove<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName);

                _logger.Info(resultInfoCategories);
                _logger.Info(resultInfoProducts);
                _logger.Dispose();
                _logger = null;

                FileSystemHelper.ClearDirectory(ShopConnectorFileSystem.GetDirectory("Image"), false);

                if (_asyncState.Exists<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName))
                {
                    _asyncState.Remove<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName);
                }
            }
        }
    }


    internal class CargoObjects
    {
        public DateTime UtcNow { get; set; }
        public bool IsNewEntity { get; set; }

        public ShopConnectorImportState State { get; set; }
        public ShopConnectorProcessingInfo Stats { get; set; }
        public FileDownloadManagerContext DownloadContext { get; set; }
        public string ImageDirectory { get; set; }
        public int CatalogAlbumId { get; set; }
        public ShopConnectorFileSystem Files { get; set; }
        public List<FileDownloadManagerItem> Images { get; set; }
        public Dictionary<string, int> ProductTemplates { get; set; }
        public Dictionary<string, int> CategoryTemplates { get; set; }
        public Dictionary<string, int> QuantityUnits { get; set; }
        public Dictionary<string, int> DeliveryTimes { get; set; }
        public IList<Language> Languages { get; set; }
        public Manufacturer ReferenceManufacturer { get; set; }
        public Dictionary<int, int> ProductIds { get; set; }
        public Dictionary<string, int> SpecAttributes { get; set; }
        public Dictionary<string, int> Attributes { get; set; }
        public Dictionary<int, int> AttributeValueIds { get; set; }
        public Dictionary<int, int> AttributeMappingIds { get; set; }
        public Dictionary<int, ShopConnectorImportCategory> CategoryIds { get; set; }
        public Multimap<int, int> LinkedProductIds { get; set; }

        public Dictionary<string, Product> ProductsBySku { get; set; }
        public Dictionary<string, Product> ProductsByGtin { get; set; }
        public Multimap<int, int> ProductCategories { get; set; }
        public Dictionary<string, Manufacturer> Manufacturers { get; set; }
        public Multimap<int, int> ProductManufacturers { get; set; }
        public Multimap<int, ProductMediaFile> ProductPictures { get; set; }
        public Dictionary<string, ProductTag> ProductTags { get; set; }
        public Dictionary<string, int> SpecAttributeOptions { get; set; }
        public Multimap<int, int> ProductSpecAttributes { get; set; }
        public Dictionary<string, int> ProductAttributes { get; set; }
        public Dictionary<string, ProductVariantAttributeValue> ProductAttributeValues { get; set; }

        // Clears data loaded per XML fragment.
        public void ClearFragmentData()
        {
            ProductsBySku?.Clear();
            ProductsByGtin?.Clear();
            ProductCategories?.Clear();
            Manufacturers?.Clear();
            ProductManufacturers?.Clear();
            ProductPictures?.Clear();
            ProductTags?.Clear();
            SpecAttributeOptions?.Clear();
            ProductSpecAttributes?.Clear();
            ProductAttributes?.Clear();
            ProductAttributeValues?.Clear();
        }
    }

    internal class ShopConnectorImportCategory
    {
        public int ParentId { get; set; }
        public int DestinationId { get; set; }
    }
}