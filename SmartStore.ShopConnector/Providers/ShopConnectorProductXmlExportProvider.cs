using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using SmartStore.Collections;
using SmartStore.Core;
using SmartStore.Core.Data;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Domain.DataExchange;
using SmartStore.Core.Domain.Stores;
using SmartStore.Core.Plugins;
using SmartStore.Services.DataExchange.Export;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector.ExportProvider
{
    [SystemName("Exports.ShopConnectorProductXml")]
    [FriendlyName("Shop-Connector XML Product Export")]
    [IsHidden(true)]
    [ExportFeatures(Features = ExportFeatures.CanOmitCompletionMail)]
    public class ShopConnectorProductXmlExportProvider : ExportProviderBase
    {
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<StoreMapping> _storeMappingRepository;
        private readonly Lazy<IShopConnectorService> _shopConnectorService;

        public ShopConnectorProductXmlExportProvider(
            IRepository<Category> categoryRepository,
            IRepository<StoreMapping> storeMappingRepository,
            Lazy<IShopConnectorService> shopConnectorService)
        {
            _categoryRepository = categoryRepository;
            _storeMappingRepository = storeMappingRepository;
            _shopConnectorService = shopConnectorService;
        }

        private bool IsCategoryAllowed(int categoryId, HashSet<int> limitedStoreIds, Dictionary<int, int> allCategoryIds, Multimap<int, int> storeMappings)
        {
            if (categoryId == 0)
                return false;

            if (!limitedStoreIds.Any())
                return true;

            while (categoryId != 0)
            {
                if (storeMappings.ContainsKey(categoryId) && !storeMappings[categoryId].Intersect(limitedStoreIds).Any())
                {
                    // is limited to stores but not to any of allowed stores
                    return false;
                }

                if (allCategoryIds.ContainsKey(categoryId) && allCategoryIds[categoryId] != categoryId)
                {
                    categoryId = allCategoryIds[categoryId];
                }
                else
                {
                    break;
                }
            }

            return true;
        }

        private void IncludeIdAndAllParentIds(int categoryId, HashSet<int> exportCategoryIds, Dictionary<int, int> allCategoryIds)
        {
            if (categoryId == 0 || !allCategoryIds.ContainsKey(categoryId))
                return;

            if (!exportCategoryIds.Contains(categoryId))
            {
                exportCategoryIds.Add(categoryId);
            }

            // include parent
            if (allCategoryIds[categoryId] != categoryId)
            {
                IncludeIdAndAllParentIds(allCategoryIds[categoryId], exportCategoryIds, allCategoryIds);
            }
        }

        private Multimap<int, int> GetCategoryToStoreMappings()
        {
            var query =
                from sm in _storeMappingRepository.TableUntracked
                join c in _categoryRepository.TableUntracked on sm.EntityId equals c.Id
                where sm.EntityName == "Category" && c.LimitedToStores
                select new { CategoryId = sm.EntityId, StoreId = sm.StoreId };

            var result = query
                .ToList()
                .ToMultimap(x => x.CategoryId, x => x.StoreId);

            return result;
        }

        public static string SystemName => "Exports.ShopConnectorProductXml";

        public override ExportEntityType EntityType => ExportEntityType.Product;

        public override string FileExtension => "XML";

        protected override void Export(ExportExecuteContext context)
        {
            var categoryIds = context.CustomProperties["CategoryIds"] as HashSet<int>;
            var storeIds = new HashSet<int>(context.CustomProperties["StoreIds"] as int[]);
            var domain = context.CustomProperties["Domain"] as string;

            var categoryToStoreMappings = GetCategoryToStoreMappings();

            var allCategoryIds = _categoryRepository.TableUntracked
                .Where(x => !x.Deleted)
                .Select(x => new { x.Id, x.ParentCategoryId })
                .ToDictionary(x => x.Id, x => x.ParentCategoryId);

            using (var writer = XmlWriter.Create(context.DataStream, ShopConnectorService.DefaultSettings))
            {
                var helper = new ExportXmlHelper(writer, true);
                helper.Exclude = ExportXmlExclude.Category;

                writer.WriteStartElement("Content");
                writer.WriteStartElement("Products");
                writer.WriteAttributeString("Version", SmartStoreVersion.CurrentVersion);

                while (context.Abort == DataExchangeAbortion.None && context.DataSegmenter.ReadNextSegment())
                {
                    var segment = context.DataSegmenter.CurrentSegment;
                    var skuMappings = new Dictionary<int, string>();

                    if (domain.HasValue())
                    {
                        var productIds = segment.Select(x => (int)((dynamic)x).Id).ToArray();
                        var mappings = _shopConnectorService.Value.GetSkuMappingsByProductIds(domain, productIds);
                        skuMappings = mappings.ToDictionarySafe(x => x.ProductId, x => x.Sku);
                    }

                    foreach (dynamic product in segment)
                    {
                        if (context.Abort != DataExchangeAbortion.None)
                        {
                            break;
                        }

                        try
                        {
                            Product entity = product.Entity;

                            // SKU mapping.
                            if (skuMappings.TryGetValue(entity.Id, out var sku))
                            {
                                product.Sku = sku;
                            }

                            helper.WriteProduct(product, "Product");

                            if (product.ProductCategories != null)
                            {
                                foreach (dynamic productCategory in product.ProductCategories)
                                {
                                    if (productCategory.Category != null)
                                    {
                                        var categoryId = (int)productCategory.Category.Id;

                                        if (IsCategoryAllowed(categoryId, storeIds, allCategoryIds, categoryToStoreMappings))
                                        {
                                            IncludeIdAndAllParentIds(categoryId, categoryIds, allCategoryIds);
                                        }
                                    }
                                }
                            }

                            ++context.RecordsSucceeded;
                        }
                        catch (OutOfMemoryException)
                        {
                            context.Abort = DataExchangeAbortion.Hard;
                            throw;
                        }
                        catch (Exception ex)
                        {
                            context.RecordException(ex, (int)product.Id);
                        }
                    }
                }

                writer.WriteElementString("Success", context.RecordsSucceeded.ToString());
                writer.WriteElementString("Failure", context.RecordsFailed.ToString());
                writer.WriteElementString("TotalRecords", context.DataSegmenter.TotalRecords.ToString());

                writer.WriteEndElement();	// Products
                writer.WriteEndElement();   // Content

                var publicKey = (string)context.CustomProperties[ShopConnectorCore.Header.PublicKey];
                var controllingData = ConnectionCache.ControllingData();
                var connection = controllingData.Connections.FirstOrDefault(x => x.PublicKey == publicKey && x.IsForExport);
                if (connection != null)
                {
                    connection.LastProductCallUtc = DateTime.UtcNow;
                    ConnectionCache.ControllingData().ConnectionsUpdated = true;
                }
            }
        }
    }
}
