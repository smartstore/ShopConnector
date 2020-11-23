using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using SmartStore.Core.Data;
using SmartStore.Core.Infrastructure;
using SmartStore.Core.Plugins;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Extensions;

namespace SmartStore.ShopConnector.Services
{
    public static class ConnectionCache
    {
        private const string _key = "ShopConnectorControllingData";
        private static readonly object _lock = new object();

        /// <remarks>
        /// Lazy storing... fired on app shut down. Note that items with CacheItemPriority.NotRemovable are not removed when the cache is emptied.
        /// We're beyond infrastructure and cannot use IOC objects here. It would lead to ComponentNotRegisteredException from autofac.
        /// </remarks>
        private static void OnDataRemoved(string key, object value, CacheItemRemovedReason reason)
        {
            if (key == _key)
            {
                var cacheData = value as ShopConnectorControllingData;
                cacheData.UpdatePersistedConnections();
            }
        }

        public static void Remove()
        {
            try
            {
                HttpRuntime.Cache.Remove(_key);
            }
            catch { }
        }

        public static ShopConnectorControllingData ControllingData()
        {
            var data = HttpRuntime.Cache[_key] as ShopConnectorControllingData;
            if (data == null)
            {
                lock (_lock)
                {
                    data = HttpRuntime.Cache[_key] as ShopConnectorControllingData;

                    if (data == null)
                    {
                        var engine = EngineContext.Current;
                        var plugin = engine.Resolve<IPluginFinder>().GetPluginDescriptorBySystemName(ShopConnectorPlugin.SystemName);
                        var settings = engine.Resolve<ShopConnectorSettings>();
                        var repository = engine.Resolve<IRepository<ShopConnectorConnectionRecord>>();

                        data = new ShopConnectorControllingData
                        {
                            IsImportEnabled = settings.IsImportEnabled,
                            IsExportEnabled = settings.IsExportEnabled,
                            ValidMinutePeriod = settings.ValidMinutePeriod,
                            LogUnauthorized = settings.LogUnauthorized,
                            ConnectorUnavailable = (plugin == null || !plugin.Installed),
                            PluginVersion = (plugin == null ? "1.0" : plugin.Version.ToString())
                        };

                        data.Connections = repository.Table
                            .Select(x => new CachedConnection
                            {
                                Id = x.Id,
                                IsActive = x.IsActive,
                                IsForExport = x.IsForExport,
                                Url = x.Url,
                                PublicKey = x.PublicKey,
                                SecretKey = x.SecretKey,
                                LastRequestUtc = x.LastRequestUtc,
                                LastProductCallUtc = x.LastProductCallUtc,
                                RequestCount = x.RequestCount
                            })
                            .ToList();

                        HttpRuntime.Cache.Add(_key, data, null, DateTime.UtcNow.AddHours(6), Cache.NoSlidingExpiration, CacheItemPriority.Normal,
                            new CacheItemRemovedCallback(OnDataRemoved));
                    }
                }
            }
            return data;
        }
    }


    public class ShopConnectorControllingData
    {
        public bool ConnectorUnavailable { get; set; }
        public bool IsImportEnabled { get; set; }
        public bool IsExportEnabled { get; set; }
        public int ValidMinutePeriod { get; set; }
        public bool LogUnauthorized { get; set; }
        public string PluginVersion { get; set; }
        public List<CachedConnection> Connections { get; set; }
        public bool ConnectionsUpdated { get; set; }

        public string Version => "{0} {1}".FormatInvariant(ShopConnectorCore.ConnectorVersion, PluginVersion);
    }

    public class CachedConnection
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
        public bool IsForExport { get; set; }
        public string Url { get; set; }
        public string PublicKey { get; set; }
        public string SecretKey { get; set; }
        public DateTime? LastRequestUtc { get; set; }
        public DateTime? LastProductCallUtc { get; set; }
        public long RequestCount { get; set; }
    }
}