using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml.XPath;
using SmartStore.Core.Data;
using SmartStore.Core.Domain.Stores;
using SmartStore.Services.DataExchange.Export;
using SmartStore.Services.Localization;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Models;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector.Extensions
{
    internal static class MiscExtensions
    {
        public static string GetEndpointUrl(this string url, string action, bool exportController = true)
        {
            Guard.NotNull(url, nameof(url));

            var result = "{0}Plugins/{1}/{2}/{3}".FormatInvariant(
                url.EnsureEndsWith("/"),
                ShopConnectorPlugin.SystemName,
                exportController ? "ShopConnectorExport" : "ShopConnectorImport",
                action);

            return result;
        }

        public static Store FindStore(this IList<Store> stores, Uri uri)
        {
            try
            {
                foreach (var store in stores)
                {
                    var storeUri = new Uri(store.Url);

                    if (storeUri.Authority.IsCaseInsensitiveEqual(uri.Authority))
                        return store;

                    string[] hosts = store.Hosts.SplitSafe(",");

                    if (hosts.Contains(uri.Authority))
                        return store;
                }
            }
            catch (Exception exc)
            {
                exc.Dump();
            }
            return null;
        }

        public static void UpdatePersistedConnections(this ShopConnectorControllingData controllingData)
        {
            try
            {
                if (controllingData != null && controllingData.ConnectionsUpdated)
                {
                    var dataToStore = controllingData.Connections.Where(x => x.LastRequestUtc.HasValue);

                    if (dataToStore.Count() > 0)
                    {
                        if (DataSettings.Current.IsValid())
                        {
                            // Note: IOC objects are not available here!
                            var now = DateTime.UtcNow;
                            var dbContext = new ShopConnectorObjectContext(DataSettings.Current.DataConnectionString);

                            foreach (var connection in dataToStore)
                            {
                                try
                                {
                                    dbContext.Execute("Update ShopConnectorConnection Set LastRequestUtc = {1}, RequestCount = {2}, LastProductCallUtc = {3}, UpdatedOnUtc = {4} Where Id = {0}",
                                        connection.Id, connection.LastRequestUtc, connection.RequestCount, connection.LastProductCallUtc, now);
                                }
                                catch (Exception exc)
                                {
                                    exc.Dump();
                                }
                            }
                        }
                    }

                    controllingData.ConnectionsUpdated = false;
                }
            }
            catch (Exception exc)
            {
                exc.Dump();
            }
        }

        public static OperationResultModel CreateAuthErrorModel(this ShopConnectorAuthResult result, ILocalizationService localize, HttpContextBase httpContext, string message = null)
        {
            var model = new OperationResultModel();

            string[] descriptions = localize.GetResource("Plugins.SmartStore.ShopConnector.ShopConnectorAuthResults").SplitSafe(";");
            var description = descriptions.SafeGet((int)result);

            model.HasError = true;
            model.ShortMessage = "{0}: {1} ({2}).".FormatInvariant(localize.GetResource("Plugins.SmartStore.ShopConnector.UnauthorizedRequest"), description, result.ToString());

            if (message.HasValue())
                model.ShortMessage = string.Concat(model.ShortMessage, " ", message);

            model.Description = HttpUtility.UrlDecode(httpContext.Request.Headers.ToString().EmptyNull()).Replace("&", "\r\n");

            return model;
        }

        public static string LoggerMessage(this string value, XPathNavigator product)
        {
            try
            {
                if (value.HasValue())
                {
                    int id = product.GetValue<int>("Id");
                    string name = product.GetString("Name");
                    string sku = product.GetString("SKU");

                    return "Product {0} »{1}« ({2}). {3}".FormatInvariant(id, name.NaIfEmpty(), sku.NaIfEmpty(), value);
                }
            }
            catch { }

            return string.Empty;
        }

        public static string ToDomain(this string url)
        {
            if (url.HasValue())
            {
                var uri = new Uri(url);

                return uri.ToDomain();
            }

            return "";
        }

        public static string ToDomain(this Uri uri)
        {
            var domain = string.Empty;

            if (uri != null)
            {
                domain = uri.Authority;

                if (domain.StartsWith("www.", StringComparison.InvariantCultureIgnoreCase))
                {
                    domain = domain.Substring(4);
                }

                if (domain.HasValue() && domain.Length <= 63)
                {
                    var idnMapping = new IdnMapping();
                    domain = idnMapping.GetAscii(domain);
                }
            }

            return domain;
        }

        public static void SafeAddId<TValue>(this IDictionary<int, TValue> instance, int id, TValue value)
        {
            if (id != 0 && !instance.ContainsKey(id))
            {
                instance.Add(id, value);
            }
        }

        public static string GetFilePath(this DataExportResult result)
        {
            if (result != null && result.Succeeded && result.FileFolder.HasValue() && result.Files != null)
            {
                var file = result.Files.FirstOrDefault();
                if (file != null)
                {
                    return Path.Combine(result.FileFolder, file.FileName);
                }
            }

            return null;
        }
    }
}
