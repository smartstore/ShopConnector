using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Web.Mvc;
using System.Xml;
using SmartStore.Core;
using SmartStore.Services.DataExchange.Export;
using SmartStore.ShopConnector.ExportProvider;
using SmartStore.ShopConnector.Extensions;
using SmartStore.ShopConnector.Models;
using SmartStore.ShopConnector.Security;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector.Controllers
{
    [ShopConnectorExportAuthenticate]
    public class ShopConnectorExportController : ShopConnectorControllerBase
    {
        private readonly IShopConnectorService _connectorService;
        private readonly ShopConnectorSettings _shopConnectorSettings;

        public ShopConnectorExportController(
            IShopConnectorService connectorService,
            ShopConnectorSettings shopConnectorSettings)
        {
            _connectorService = connectorService;
            _shopConnectorSettings = shopConnectorSettings;
        }

        //[HttpPost]
        //public void Notification()
        //{
        //	//_connectorService.ProcessNotification();
        //}

        public ActionResult About()
        {
            string errorMsg = null;
            var fileSystem = new ShopConnectorFileSystem("Export");
            var path = fileSystem.GetFullFilePath(string.Concat("about-", Guid.NewGuid().ToString(), ".xml"));

            var publicKey = Request.Headers[ShopConnectorCore.Header.PublicKey];
            var controllingData = ConnectionCache.ControllingData();
            var connection = controllingData.Connections.FirstOrDefault(x => x.PublicKey == publicKey && x.IsForExport);

            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = XmlWriter.Create(fileStream, ShopConnectorService.DefaultSettings))
            {
                try
                {
                    var model = _connectorService.CreateAboutModel(connection);

                    writer.WriteStartElement("Content");
                    writer.WriteElementString("AppVersion", model.AppVersion);
                    writer.WriteElementString("UtcTime", model.UtcTime.ToString("o"));
                    writer.WriteElementString("ConnectorVersion", model.ConnectorVersion);
                    writer.WriteElementString("StoreName", model.StoreName);
                    writer.WriteElementString("StoreUrl", model.StoreUrl);
                    writer.WriteElementString("StoreCount", model.StoreCount.ToString());
                    writer.WriteElementString("CompanyName", model.CompanyName);
                    writer.WriteElementString("StoreLogoUrl", model.StoreLogoUrl);
                    writer.WriteElementString("UpdatedProductsCount", model.UpdatedProductsCount.NaIfEmpty());

                    writer.WriteStartElement("Manufacturers");
                    foreach (var manu in model.AvailableManufacturers)
                    {
                        writer.WriteStartElement("Manufacturer");
                        writer.WriteElementString("Name", manu.Text);
                        writer.WriteElementString("Id", manu.Value);
                        writer.WriteEndElement();   // Manufacturer
                    }
                    writer.WriteEndElement();   // Manufacturers

                    writer.WriteStartElement("Categories");
                    foreach (var category in model.AvailableCategories)
                    {
                        writer.WriteStartElement("Category");
                        writer.WriteElementString("Name", category.Text);
                        writer.WriteElementString("Id", category.Value);
                        writer.WriteEndElement();   // Category
                    }
                    writer.WriteEndElement();   // Categories

                    writer.WriteEndElement();   // Content
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }
            }

            if (errorMsg.HasValue())
            {
                Response.AddHeader("Sm-ShopConnector-ErrorMessageShort", errorMsg);
            }
            if (connection != null)
            {
                Response.AddHeader("Sm-ShopConnector-RequestCount", connection.RequestCount.ToString());
                Response.AddHeader("Sm-ShopConnector-LastRequest", connection.LastRequestUtc.HasValue ? connection.LastRequestUtc.Value.ToString("o") : "");
                Response.AddHeader("Sm-ShopConnector-LastProductCall", connection.LastProductCallUtc.HasValue ? connection.LastProductCallUtc.Value.ToString("o") : "");
            }

            ShopConnectorFileSystem.CleanupDirectories();

            Response.BufferOutput = false; // !!
            var finalStream = new FileStream(path, FileMode.Open);
            return new FileStreamResult(finalStream, MediaTypeNames.Text.Xml);
        }

        [ShopConnectorCompatibility]
        public ActionResult ProductData(ProductDataModel model)
        {
            // Ids are transmitted as a string (for backward compatibility) but model property is an int array.
            var rawManuIds = Request.QueryString["FilterManufacturerIds"] as string;
            model.FilterManufacturerIds = rawManuIds.ToIntArray();

            var cancellation = new CancellationTokenSource(TimeSpan.FromHours(_shopConnectorSettings.MaxHoursToExport));
            var context = new ShopConnectorExportContext
            {
                Model = model,
                PublicKey = Request.Headers[ShopConnectorCore.Header.PublicKey],
                CategoryIds = new HashSet<int>()
            };

            // Important: first products, then categories.
            var productResult = _connectorService.Export(context, cancellation.Token, ShopConnectorProductXmlExportProvider.SystemName);
            var categoryResult = _connectorService.Export(context, cancellation.Token, ShopConnectorCategoryXmlExportProvider.SystemName);

            var productPath = productResult.GetFilePath();
            var categoryPath = categoryResult.GetFilePath();

            if (!productResult.Succeeded || !categoryResult.Succeeded)
            {
                return ShopConnectorError(HttpStatusCode.InternalServerError, T("Admin.Common.UnknownError"), productResult.LastError.HasValue() ? productResult.LastError : categoryResult.LastError);
            }


            // Create compound XML file.
            var fileSystem = new ShopConnectorFileSystem("Export");
            var path = fileSystem.GetFullFilePath(string.Concat("data-", Guid.NewGuid().ToString(), ".xml"));
            string errorMsg = null;
            var cSuccess = 0;
            var cFailure = 0;
            var cTotalRecords = 0;
            var pSuccess = 0;
            var pFailure = 0;
            var pTotalRecords = 0;

            using (var fileStream = new ExportFileStream(new FileStream(path, FileMode.Create, FileAccess.Write)))
            using (var writer = XmlWriter.Create(fileStream, ShopConnectorService.DefaultSettings))
            {
                try
                {
                    writer.WriteStartElement("Content");
                    writer.WriteStartElement("Categories");
                    writer.WriteAttributeString("Version", SmartStoreVersion.CurrentVersion);
                    if (categoryPath.HasValue())
                    {
                        var nodeNames = new string[] { "Category", "Success", "Failure", "TotalRecords" };
                        using (var reader = XmlReader.Create(categoryPath, new XmlReaderSettings { CheckCharacters = false }))
                        {
                            if (reader.ReadToFollowing("Categories"))
                            {
                                using (var categories = reader.ReadSubtree())
                                {
                                    var siblingDepth = categories.Depth + 1;
                                    while (!categories.EOF)
                                    {
                                        if (categories.Depth == siblingDepth && categories.NodeType == XmlNodeType.Element && nodeNames.Contains(categories.Name))
                                        {
                                            var name = categories.Name;
                                            var val = categories.ReadOuterXml();    // Must be last categories statement cause of cursor position.
                                            switch (name)
                                            {
                                                case "Category":
                                                    writer.WriteRaw(val);
                                                    break;
                                                case "Success":
                                                    cSuccess = val.EmptyNull().Replace("<Success>", "").Replace("</Success>", "").ToInt();
                                                    break;
                                                case "Failure":
                                                    cFailure = val.EmptyNull().Replace("<Failure>", "").Replace("</Failure>", "").ToInt();
                                                    break;
                                                case "TotalRecords":
                                                    cTotalRecords = val.EmptyNull().Replace("<TotalRecords>", "").Replace("</TotalRecords>", "").ToInt();
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            categories.Read();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    writer.WriteEndElement();   // Categories

                    writer.WriteStartElement("Products");
                    writer.WriteAttributeString("Version", SmartStoreVersion.CurrentVersion);
                    if (productPath.HasValue())
                    {
                        var nodeNames = new string[] { "Product", "Success", "Failure", "TotalRecords" };
                        using (var reader = XmlReader.Create(productPath, new XmlReaderSettings { CheckCharacters = false }))
                        {
                            if (reader.ReadToFollowing("Products"))
                            {
                                using (var products = reader.ReadSubtree())
                                {
                                    var siblingDepth = products.Depth + 1;
                                    while (!products.EOF)
                                    {
                                        if (products.Depth == siblingDepth && products.NodeType == XmlNodeType.Element && nodeNames.Contains(products.Name))
                                        {
                                            var name = products.Name;
                                            var val = products.ReadOuterXml();
                                            switch (name)
                                            {
                                                case "Product":
                                                    writer.WriteRaw(val);
                                                    break;
                                                case "Success":
                                                    pSuccess = val.EmptyNull().Replace("<Success>", "").Replace("</Success>", "").ToInt();
                                                    break;
                                                case "Failure":
                                                    pFailure = val.EmptyNull().Replace("<Failure>", "").Replace("</Failure>", "").ToInt();
                                                    break;
                                                case "TotalRecords":
                                                    pTotalRecords = val.EmptyNull().Replace("<TotalRecords>", "").Replace("</TotalRecords>", "").ToInt();
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            products.Read();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    writer.WriteEndElement();   // Products
                    writer.WriteEndElement();   // Content
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }
            }

            if (errorMsg.HasValue())
            {
                Response.AddHeader("Sm-ShopConnector-ErrorMessageShort", errorMsg);
            }

            Response.AddHeader("Sm-ShopConnector-Category", string.Join(",", cSuccess, cFailure, cTotalRecords));
            Response.AddHeader("Sm-ShopConnector-Product", string.Join(",", pSuccess, pFailure, pTotalRecords));

            var publicKey = Request.Headers[ShopConnectorCore.Header.PublicKey];
            var controllingData = ConnectionCache.ControllingData();
            var connection = controllingData.Connections.FirstOrDefault(x => x.PublicKey == publicKey && x.IsForExport);
            if (connection != null)
            {
                Response.AddHeader("Sm-ShopConnector-RequestCount", connection.RequestCount.ToString());
                Response.AddHeader("Sm-ShopConnector-LastRequest", connection.LastRequestUtc.HasValue ? connection.LastRequestUtc.Value.ToString("o") : "");
                Response.AddHeader("Sm-ShopConnector-LastProductCall", connection.LastProductCallUtc.HasValue ? connection.LastProductCallUtc.Value.ToString("o") : "");
            }

            ShopConnectorFileSystem.CleanupDirectories();

            Response.BufferOutput = false; // !!
            var finalStream = new FileStream(path, FileMode.Open);
            return new FileStreamResult(finalStream, MediaTypeNames.Text.Xml);
        }
    }
}