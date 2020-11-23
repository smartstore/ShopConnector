using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Web.Mvc;
using System.Xml.XPath;
using SmartStore.Core.Async;
using SmartStore.Core.Security;
using SmartStore.ShopConnector.Extensions;
using SmartStore.ShopConnector.Models;
using SmartStore.ShopConnector.Security;
using SmartStore.ShopConnector.Services;
using SmartStore.Utilities;
using SmartStore.Web.Framework.Controllers;
using SmartStore.Web.Framework.Security;
using SmartStore.Web.Framework.Theming;
using Telerik.Web.Mvc;

namespace SmartStore.ShopConnector.Controllers
{
    [AdminAuthorize]
    public class ShopConnectorImportController : ShopConnectorControllerBase
    {
        private readonly IShopConnectorService _connectorService;
        private readonly IAsyncState _asyncState;

        public ShopConnectorImportController(
            IShopConnectorService connectorService,
            IAsyncState asyncState)
        {
            _connectorService = connectorService;
            _asyncState = asyncState;
        }

        [Permission(ShopConnectorPermissions.Read)]
        public ActionResult ImportLog()
        {
            var path = ShopConnectorFileSystem.ImportLogFile();
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var stream = new FileStream(path, FileMode.Open);
                    var result = new FileStreamResult(stream, MediaTypeNames.Text.Plain);

                    return result;
                }
                catch (IOException)
                {
                    NotifyWarning(T("Admin.Common.FileInUse"));
                }
            }

            TempData["SelectedTab"] = "Import";

            return RedirectToConfiguration(ShopConnectorPlugin.SystemName);
        }

        [AdminThemed]
        [Permission(ShopConnectorPermissions.Read)]
        public ActionResult About(int id)
        {
            var context = new ShopConnectorRequestContext
            {
                ActionMethod = "About"
            };

            try
            {
                if (!_connectorService.SendRequest(context, id))
                {
                    return new ShopConnectorOperationResult(context);
                }

                var model = new AboutModel();

                if (System.IO.File.Exists(context.ResponsePath))
                {
                    var doc = new XPathDocument(context.ResponsePath);
                    var content = doc.GetContent();

                    model.AppVersion = content.GetString("AppVersion");
                    model.UtcTime = content.GetString("UtcTime").ToDateTimeIso8601() ?? DateTime.UtcNow;
                    model.ConnectorVersion = content.GetString("ConnectorVersion");
                    model.StoreName = content.GetString("StoreName");
                    model.StoreUrl = content.GetString("StoreUrl");
                    model.StoreCount = content.GetValue("StoreCount", 1);
                    model.CompanyName = content.GetString("CompanyName");
                    model.StoreLogoUrl = content.GetString("StoreLogoUrl");
                    model.UpdatedProductsCount = content.GetString("UpdatedProductsCount", "".NaIfEmpty());
                }
                return PartialView(model);
            }
            catch (Exception ex)
            {
                context.ResponseModel = new OperationResultModel(ex);
            }

            return new ShopConnectorOperationResult(context);
        }

        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult ProductData(int id)
        {
            var context = new ShopConnectorRequestContext
            {
                ActionMethod = "About"
            };

            try
            {
                if (!_connectorService.SendRequest(context, id))
                {
                    return new ShopConnectorOperationResult(context);
                }

                var model = new ProductDataModel { Id = id };
                model.FetchFromDate = _connectorService.ConvertDateTime(context.Connection.LastProductCallUtc, true);
                model.AvailableCategories = new List<SelectListItem>();

                if (System.IO.File.Exists(context.ResponsePath))
                {
                    var doc = new XPathDocument(context.ResponsePath);
                    var content = doc.GetContent();
                    var manus = new List<SelectListItem>();

                    foreach (XPathNavigator manu in content.Select("Manufacturers/Manufacturer"))
                    {
                        manus.Add(new SelectListItem
                        {
                            Text = manu.GetString("Name"),
                            Value = manu.GetString("Id")
                        });
                    }
                    model.AvailableManufacturers = new MultiSelectList(manus, "Value", "Text");

                    foreach (XPathNavigator category in content.Select("Categories/Category"))
                    {
                        model.AvailableCategories.Add(new SelectListItem
                        {
                            Text = category.GetString("Name"),
                            Value = category.GetString("Id")
                        });
                    }
                }

                ViewData["pickTimeFieldIds"] = new List<string> { "FetchFromDate" };

                return PartialView(model);
            }
            catch (Exception ex)
            {
                context.ResponseModel = new OperationResultModel(ex);
            }

            return new ShopConnectorOperationResult(context);
        }

        [HttpPost]
        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult ProductData(ProductDataModel model)
        {
            string val;
            var fetchFrom = _connectorService.ConvertDateTime(model.FetchFromDate, false);

            var context = new ShopConnectorRequestContext
            {
                ActionMethod = "ProductData"
            };

            try
            {
                if (model.DataFileName.IsEmpty())
                {
                    var controllingData = ConnectionCache.ControllingData();
                    var connection = controllingData.Connections.FirstOrDefault(x => x.Id == model.Id);

                    model.DataFileName = connection.Url.EmptyNull().Replace("https://", "").Replace("http://", "").Replace("/", "");
                }

                context.RequestContent.Add("FetchFrom", fetchFrom.HasValue ? fetchFrom.Value.ToString("o") : "");
                context.RequestContent.Add("FilterManufacturerIds", string.Join(",", model.FilterManufacturerIds ?? new int[0]));
                context.RequestContent.Add("FilterCategoryId", model.FilterCategoryId.EmptyNull());
                context.RequestContent.Add("DataFileName", model.DataFileName.EmptyNull());

                if (!_connectorService.SendRequest(context, model.Id))
                {
                    return new ShopConnectorOperationResult(context);
                }

                var cStats = context.Headers.TryGetValue("Sm-ShopConnector-Category", out val)
                    ? val.ToIntArray()
                    : new int[] { 0, 0, 0 };

                var pStats = context.Headers.TryGetValue("Sm-ShopConnector-Product", out val)
                    ? val.ToIntArray()
                    : new int[] { 0, 0, 0 };

                string message = null;

                if ((cStats[0] == 0 && pStats[0] == 0) || ShopConnectorFileSystem.GetFileSize(context.ResponsePath) == 0)
                {
                    // Avoid empty files.
                    FileSystemHelper.DeleteFile(context.ResponsePath);

                    message = T("Plugins.SmartStore.ShopConnector.NoContent");
                }
                else
                {
                    message = T("Plugins.SmartStore.ShopConnector.ProcessingResult",
                        cStats[2].ToString("N0"), cStats[0].ToString("N0"), cStats[1].ToString("N0"),
                        pStats[2].ToString("N0"), pStats[0].ToString("N0"), pStats[1].ToString("N0"));

                    var stats = new ShopConnectorImportStats("Product");
                    stats.Add(new ImportStats.FileStats
                    {
                        Name = Path.GetFileName(context.ResponsePath),
                        CategoryCount = cStats[0],
                        ProductCount = pStats[0]
                    });
                }

                context.ResponseModel = new OperationResultModel(message, false);
            }
            catch (Exception ex)
            {
                context.ResponseModel = new OperationResultModel(ex);
            }

            return new ShopConnectorOperationResult(context);
        }

        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult ProductDataDownload(int id, string name)
        {
            if (name.HasValue())
            {
                var files = new ShopConnectorFileSystem("Product");
                var path = files.GetFullFilePath(name);
                var fstream = new FileStream(path, FileMode.Open, FileAccess.Read);

                return new FileStreamResult(fstream, MediaTypeNames.Text.Xml);
            }

            return null;
        }

        [Permission(ShopConnectorPermissions.Import)]
        public void ProductDataDelete(int id, string name)
        {
            if (name.HasValue())
            {
                var files = new ShopConnectorFileSystem("Product");
                files.DeleteFile(name);
            }
        }

        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult ProductFileSelect(int id)
        {
            var progress = _asyncState.Get<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName);
            if (progress != null)
            {
                return new EmptyResult();
            }

            var model = new ProductFileSelectModel();
            _connectorService.SetupProductFileSelectModel(model, id);

            if (model.AvailableImportFiles.Count <= 0)
            {
                return new ShopConnectorOperationResult(T("Plugins.SmartStore.ShopConnector.NoImportFilesFound"), false);
            }

            return PartialView(model);
        }

        [HttpPost, GridAction(EnableCustomBinding = true)]
        [Permission(ShopConnectorPermissions.Read)]
        public ActionResult ProductImportList(GridCommand command, ProductImportModel model)
        {
            var totalItems = 0;
            List<ProductImportItemModel> data = null;

            try
            {
                data = _connectorService.GetProductImportItems(model.ImportFile, command.Page - 1, out totalItems);
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }

            var gridModel = new GridModel
            {
                Total = totalItems,
                Data = data ?? new List<ProductImportItemModel>()
            };

            return new JsonResult { Data = gridModel };
        }

        [AdminThemed]
        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult ProductImport(int id, ProductImportModel model)
        {
            _connectorService.SetupProductImportModel(model, id);

            if (model.ImportFile.IsEmpty())
            {
                TempData["SelectedTab"] = "Import";

                return RedirectToConfiguration(ShopConnectorPlugin.SystemName);
            }

            return View(model);
        }

        [HttpPost]
        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult ProductImport(ProductImportModel model)
        {
            _connectorService.Import(model);

            TempData["SelectedTab"] = "Import";

            return RedirectToConfiguration(ShopConnectorPlugin.SystemName);
        }

        public JsonResult ProductImportProgress()
        {
            var progress = _asyncState.Get<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName);
            string message;

            if (progress == null)
            {
                var completedModel = new ProductImportCompletedModel();
                _connectorService.SetupProductImportCompletedModel(completedModel);

                message = this.RenderPartialViewToString("ProductImportCompleted", completedModel);
            }
            else
            {
                var cancelButton = "<a href=\"{0}\" class=\"btn btn-danger btn-sm\">{1}</a>".FormatInvariant(
                    Url.Action("CancelImport", "ShopConnectorImport", new { area = ShopConnectorPlugin.SystemName }),
                    T("Common.Cancel"));

                message = "<div class=\"progress-stats\">{0}</div><div class=\"mt-2\">{1}</div>".FormatInvariant(progress.ToString(), cancelButton);
            }

            return Json(new
            {
                NoRunningTask = progress == null,
                Message = message
            },
            JsonRequestBehavior.AllowGet);
        }

        [Permission(ShopConnectorPermissions.Import)]
        public ActionResult CancelImport()
        {
            try
            {
                _asyncState.Cancel<ShopConnectorProcessingInfo>(ShopConnectorPlugin.SystemName);
            }
            catch (Exception ex)
            {
                ex?.Message?.Dump();
            }
            finally
            {
                NotifyWarning(T("Admin.System.ScheduleTasks.CancellationRequested"));
            }

            return RedirectToConfiguration(ShopConnectorPlugin.SystemName);
        }
    }
}