using System;
using System.Collections.Generic;
using System.Linq;
using SmartStore.Core;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Models;
using SmartStore.Web.Framework.WebApi.Security;

namespace SmartStore.ShopConnector.Services
{
    public partial class ShopConnectorService
    {
        public ShopConnectorConnectionRecord InsertConnection(ConnectionModel model)
        {
            var utcNow = DateTime.UtcNow;
            ShopConnectorConnectionRecord connection = null;

            if (model.IsForExport)
            {
                var hmac = new HmacAuthentication();
                string publicKey, secretKey;

                for (var i = 0; i < 9999; ++i)
                {
                    if (hmac.CreateKeys(out publicKey, out secretKey) && !_connectionRepository.TableUntracked.Any(x => x.PublicKey == publicKey))
                    {
                        connection = new ShopConnectorConnectionRecord
                        {
                            IsActive = true,
                            IsForExport = model.IsForExport,
                            Url = model.Url,
                            PublicKey = publicKey,
                            SecretKey = secretKey,
                            LimitedToManufacturerIds = string.Join(",", model.LimitedToManufacturerIds ?? new int[0]),
                            LimitedToStoreIds = string.Join(",", model.LimitedToStoreIds ?? new int[0]),
                            CreatedOnUtc = utcNow,
                            UpdatedOnUtc = utcNow
                        };
                        break;
                    }
                }
            }
            else
            {
                connection = new ShopConnectorConnectionRecord
                {
                    IsActive = true,
                    IsForExport = model.IsForExport,
                    Url = model.Url,
                    PublicKey = model.PublicKey,
                    SecretKey = model.SecretKey,
                    LimitedToManufacturerIds = string.Join(",", model.LimitedToManufacturerIds ?? new int[0]),
                    LimitedToStoreIds = string.Join(",", model.LimitedToStoreIds ?? new int[0]),
                    CreatedOnUtc = utcNow,
                    UpdatedOnUtc = utcNow
                };
            }

            if (connection != null)
            {
                _connectionRepository.Insert(connection);
                ConnectionCache.Remove();
            }

            return connection;
        }

        public bool UpdateConnection(ConnectionModel model, bool isForExport)
        {
            var connection = _connectionRepository.GetById(model.Id);
            if (connection == null)
            {
                return false;
            }

            connection.IsActive = model.IsActive;
            connection.PublicKey = model.PublicKey;
            connection.SecretKey = model.SecretKey;
            connection.UpdatedOnUtc = DateTime.UtcNow;
            connection.Url = model.Url;

            if (isForExport)
            {
                connection.LimitedToManufacturerIds = string.Join(",", model.LimitedToManufacturerIds ?? new int[0]);
                connection.LimitedToStoreIds = string.Join(",", model.LimitedToStoreIds ?? new int[0]);
            }

            _connectionRepository.Update(connection);
            ConnectionCache.Remove();

            return true;
        }

        public void DeleteConnection(int id)
        {
            var connection = _connectionRepository.GetById(id);

            if (connection != null)
            {
                // hooks not working here, so do it manually
                // TODO: remove during uninstall too.
                //var mappings = _storeMappingService.GetStoreMappings(connection);

                //mappings.Each(x => _storeMappingService.DeleteStoreMapping(x));

                _connectionRepository.Delete(connection);

                ConnectionCache.Remove();
            }
        }

        public IPagedList<ConnectionModel> GetConnections(bool isForExport, int pageIndex, int pageSize)
        {
            var query = _connectionRepository.TableUntracked
                .Where(x => x.IsForExport == isForExport)
                .OrderByDescending(x => x.CreatedOnUtc);

            var list = new PagedList<ShopConnectorConnectionRecord>(query, pageIndex, pageSize);

            var models = list
                .Select(x => new ConnectionModel
                {
                    Id = x.Id,
                    IsActive = x.IsActive,
                    Url = x.Url,
                    PublicKey = x.PublicKey,
                    SecretKey = x.SecretKey,
                    RequestCount = x.RequestCount,
                    LastRequest = ConvertDateTime(x.LastRequestUtc, true),
                    LastProductCall = ConvertDateTime(x.LastProductCallUtc, true),
                    LimitedToManufacturerIds = x.LimitedToManufacturerIds.ToIntArray(),
                    LimitedToStoreIds = x.LimitedToStoreIds.ToIntArray()
                })
                .ToList();

            var result = new PagedList<ConnectionModel>(models, pageIndex, pageSize, list.TotalCount);
            return result;
        }

        public void InsertSkuMapping(ShopConnectorSkuMapping mapping)
        {
            Guard.NotNull(mapping, nameof(mapping));

            _skuMappingRepository.Insert(mapping);
        }

        public void UpdateSkuMapping(ShopConnectorSkuMapping mapping)
        {
            Guard.NotNull(mapping, nameof(mapping));

            _skuMappingRepository.Update(mapping);
        }

        public void DeleteSkuMapping(ShopConnectorSkuMapping mapping)
        {
            if (mapping != null)
            {
                _skuMappingRepository.Delete(mapping);
            }
        }

        public ShopConnectorSkuMapping GetSkuMappingsById(int id)
        {
            if (id == 0)
            {
                return null;
            }

            return _skuMappingRepository.GetById(id);
        }

        public List<ShopConnectorSkuMapping> GetSkuMappingsByProductIds(string domain, params int[] productIds)
        {
            var len = productIds?.Length ?? 0;

            if (len == 0)
            {
                return new List<ShopConnectorSkuMapping>();
            }

            var productId = len == 1 ? productIds[0] : 0;

            var query = productId != 0
                ? _skuMappingRepository.TableUntracked.Where(x => x.ProductId == productId)
                : _skuMappingRepository.TableUntracked.Where(x => productIds.Contains(x.ProductId));

            if (domain.HasValue())
            {
                query = query.Where(x => x.Domain == domain);
            }

            var mappings = query.ToList();
            return mappings;
        }
    }
}