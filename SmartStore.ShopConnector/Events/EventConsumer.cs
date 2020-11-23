using System;
using System.Collections.Generic;
using System.Linq;
using SmartStore.Core.Data;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Events;
using SmartStore.Core.Localization;
using SmartStore.Core.Security;
using SmartStore.Services.DataExchange.Import.Events;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Security;
using SmartStore.Web.Framework.Events;
using SmartStore.Web.Framework.Modelling;

namespace SmartStore.ShopConnector.Events
{
    public class EventConsumer : IConsumer
    {
        private readonly Lazy<IRepository<ShopConnectorSkuMapping>> _skuMappingRepository;
        private readonly Lazy<IPermissionService> _permissionService;
        private readonly Lazy<ShopConnectorSettings> _shopConnectorSettings;

        public EventConsumer(
            Lazy<IRepository<ShopConnectorSkuMapping>> skuMappingRepository,
            Lazy<IPermissionService> permissionService,
            Lazy<ShopConnectorSettings> shopConnectorSettings)
        {
            _skuMappingRepository = skuMappingRepository;
            _permissionService = permissionService;
            _shopConnectorSettings = shopConnectorSettings;

            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        // Add tab on product edit page.
        public void HandleEvent(TabStripCreated eventMessage)
        {
            if (eventMessage.TabStripName == "product-edit" &&
                _shopConnectorSettings.Value.EnableSkuMapping &&
                _permissionService.Value.Authorize(ShopConnectorPermissions.EditSkuMapping))
            {
                var productId = ((TabbableModel)eventMessage.Model).Id;

                eventMessage.ItemFactory.Add()
                    .Text(T("Plugins.SmartStore.ShopConnector.ShopConnector"))
                    .Name("tab-shop-connector")
                    .Icon("fas fa-network-wired fa-lg fa-fw")
                    .LinkHtmlAttributes(new { data_tab_name = "ShopConnector" })
                    .Action("ProductEditTab", "ShopConnector", new { productId, area = ShopConnectorPlugin.SystemName })
                    .Ajax();
            }
        }

        // Import differing product SKUs.
        public void HandleEvent(ImportBatchExecutedEvent<Product> eventMessage)
        {
            if (!_shopConnectorSettings.Value.EnableSkuImport ||
                !eventMessage.Batch.Any() ||
                !_permissionService.Value.Authorize(ShopConnectorPermissions.EditSkuMapping))
            {
                return;
            }

            var columns = new List<string>();
            for (var i = 1; i < 99999; ++i)
            {
                var columnName = string.Concat("ClientSku", i);
                if (eventMessage.Context.DataSegmenter.HasColumn(columnName))
                {
                    columns.Add(columnName);
                }
                else
                {
                    break;
                }
            }

            if (!columns.Any())
            {
                return;
            }

            _skuMappingRepository.Value.Context.DetachEntities(x => x is ShopConnectorSkuMapping);
            _skuMappingRepository.Value.AutoCommitEnabled = false;

            var productIds = eventMessage.Batch.Select(x => x.Entity.Id).ToArray();
            var mappings = _skuMappingRepository.Value.TableUntracked
                .Where(x => productIds.Contains(x.ProductId))
                .ToList();

            // Update existing mappings. Avoid duplicates.
            var mappingsDic = mappings.ToDictionarySafe(x => string.Concat(x.ProductId, "|", x.Domain), x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var row in eventMessage.Batch)
            {
                foreach (var column in columns)
                {
                    if (row.Entity.Id != 0 && row.GetDataValue<string>(column).SplitToPair(out var domain, out var sku, "|"))
                    {
                        domain = domain.EmptyNull();

                        if (domain.StartsWith("www.", StringComparison.InvariantCultureIgnoreCase))
                        {
                            domain = domain.Substring(4);
                        }
                        if (string.IsNullOrWhiteSpace(domain))
                        {
                            continue;
                        }

                        var delete = sku.IsCaseInsensitiveEqual("[DELETE]");
                        var ignore = sku.IsCaseInsensitiveEqual("[IGNORE]");

                        if (mappingsDic.TryGetValue(string.Concat(row.Entity.Id, "|", domain), out var existingMapping))
                        {
                            // Mapping already exists.
                            if (delete)
                            {
                                _skuMappingRepository.Value.Delete(existingMapping);
                            }
                            else if (!ignore)
                            {
                                existingMapping.Sku = sku;
                                _skuMappingRepository.Value.Update(existingMapping);
                            }
                        }
                        else if (!delete && !ignore)
                        {
                            _skuMappingRepository.Value.Insert(new ShopConnectorSkuMapping
                            {
                                ProductId = row.Entity.Id,
                                Domain = domain,
                                Sku = sku
                            });
                        }
                    }
                }
            }

            _skuMappingRepository.Value.Context.SaveChanges();
        }
    }
}