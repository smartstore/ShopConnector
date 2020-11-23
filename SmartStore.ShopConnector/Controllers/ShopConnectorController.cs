using System.Linq;
using System.Web.Mvc;
using SmartStore.Core.Security;
using SmartStore.ShopConnector.Data;
using SmartStore.ShopConnector.Extensions;
using SmartStore.ShopConnector.Models;
using SmartStore.ShopConnector.Security;
using SmartStore.ShopConnector.Services;
using SmartStore.Web.Framework.Controllers;
using SmartStore.Web.Framework.Security;
using Telerik.Web.Mvc;

namespace SmartStore.ShopConnector.Controllers
{
    [AdminAuthorize]
    public class ShopConnectorController : PluginControllerBase
    {
        private readonly IShopConnectorService _connectorService;

        public ShopConnectorController(IShopConnectorService connectorService)
        {
            _connectorService = connectorService;
        }

        [ChildActionOnly]
        [Permission(ShopConnectorPermissions.Read)]
        public ActionResult Configure()
        {
            var settings = Services.Settings.LoadSetting<ShopConnectorSettings>();

            var model = new ConfigurationModel();
            model.SelectedTab = TempData["SelectedTab"] as string;
            model.Copy(settings, true);

            _connectorService.SetupConfiguration(model);

            return View(model);
        }

        [HttpPost, ChildActionOnly]
        [Permission(ShopConnectorPermissions.Update)]
        public ActionResult Configure(ConfigurationModel model, FormCollection form)
        {
            if (!ModelState.IsValid)
            {
                return Configure();
            }

            var settings = Services.Settings.LoadSetting<ShopConnectorSettings>();

            model.Copy(settings, false);

            Services.Settings.SaveSetting(settings);
            ConnectionCache.Remove();

            NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
            return Configure();
        }

        [HttpPost, GridAction(EnableCustomBinding = true)]
        [Permission(ShopConnectorPermissions.Read)]
        public ActionResult ConnectionSelect(GridCommand command, bool isForExport)
        {
            var model = new GridModel<ConnectionModel>();

            ConnectionCache.ControllingData().UpdatePersistedConnections();

            var connections = _connectorService.GetConnections(isForExport, command.Page - 1, command.PageSize);

            model.Data = connections;
            model.Total = connections.TotalCount;

            return new JsonResult { Data = model };
        }

        [Permission(ShopConnectorPermissions.Read)]
        public ActionResult ConnectionUpsert(int id, bool isForExport)
        {
            var model = new ConnectionModel();

            _connectorService.SetupConnectionModel(model, id, isForExport);

            return PartialView(model);
        }

        [HttpPost]
        public ActionResult ConnectionUpsert(ConnectionModel model, bool isForExport, FormCollection form)
        {
            var permissionName = model.Id == 0 ? ShopConnectorPermissions.Create : ShopConnectorPermissions.Update;

            if (Services.Permissions.Authorize(permissionName))
            {
                var validator = new ConnectionModelValidator(T);
                validator.Validate(model, ModelState);

                if (ModelState.IsValid)
                {
                    if (model.Id == 0)
                    {
                        if (_connectorService.InsertConnection(model) != null)
                        {
                            NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
                        }
                    }
                    else
                    {
                        if (_connectorService.UpdateConnection(model, isForExport))
                        {
                            NotifySuccess(T("Admin.Common.DataEditSuccess"));
                        }
                    }
                }
                else
                {
                    return Content(string.Join("<br />", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)));
                }
            }
            else
            {
                NotifyError(T("Admin.AccessDenied.Description"));
            }

            return new EmptyResult();
        }

        [HttpPost]
        [Permission(ShopConnectorPermissions.Delete)]
        public void ConnectionDelete(int id)
        {
            _connectorService.DeleteConnection(id);
        }

        [HttpPost]
        [Permission(ShopConnectorPermissions.Upload)]
        public ActionResult XmlFileUpload()
        {
            var success = false;
            var postedFile = Request.ToPostedFileResult();
            if (postedFile != null)
            {
                var files = new ShopConnectorFileSystem("Product");
                var path = files.GetFilePath(postedFile.FileTitle);

                success = postedFile.Stream.ToFile(path);
            }

            var message = T(success ? "Admin.Common.UploadFileSucceeded" : "Admin.Common.UploadFileFailed").Text;

            return new JsonResult
            {
                Data = new
                {
                    success,
                    Message = message.NaIfEmpty(),
                    MessageType = success ? "success" : "error",
                    name = postedFile.FileName,
                    ext = postedFile.FileExtension
                }
            };
        }

        #region SKU mapping

        [Permission(ShopConnectorPermissions.EditSkuMapping)]
        public ActionResult ProductEditTab(int productId)
        {
            var model = new ShopConnectorProductModel
            {
                ProductId = productId
            };

            return PartialView(model);
        }

        [HttpPost, GridAction(EnableCustomBinding = true)]
        [Permission(ShopConnectorPermissions.EditSkuMapping)]
        public ActionResult SkuMappingList(GridCommand command, int productId)
        {
            var model = new GridModel<SkuMappingModel>();

            var mappings = _connectorService.GetSkuMappingsByProductIds(null, productId);
            var mappingsModel = mappings
                .Select(x => new SkuMappingModel
                {
                    Id = x.Id,
                    Domain = x.Domain,
                    Sku = x.Sku
                })
                .ToList();

            model.Data = mappingsModel;
            model.Total = mappingsModel.Count;

            return new JsonResult
            {
                Data = model
            };
        }

        [GridAction(EnableCustomBinding = true)]
        [Permission(ShopConnectorPermissions.EditSkuMapping)]
        public ActionResult SkuMappingInsert(GridCommand command, SkuMappingModel model, int productId)
        {
            if (productId != 0 && model.Domain.HasValue())
            {
                var entity = new ShopConnectorSkuMapping
                {
                    ProductId = productId,
                    Domain = model.Domain.TrimSafe(),
                    Sku = model.Sku.TrimSafe()
                };

                _connectorService.InsertSkuMapping(entity);
            }

            return SkuMappingList(command, productId);
        }

        [GridAction(EnableCustomBinding = true)]
        [Permission(ShopConnectorPermissions.EditSkuMapping)]
        public ActionResult SkuMappingUpdate(GridCommand command, SkuMappingModel model, int productId)
        {
            if (model.Domain.HasValue())
            {
                var entity = _connectorService.GetSkuMappingsById(model.Id);
                if (entity != null)
                {
                    entity.Domain = model.Domain;
                    entity.Sku = model.Sku;

                    _connectorService.UpdateSkuMapping(entity);
                }
            }

            return SkuMappingList(command, productId);
        }

        [GridAction(EnableCustomBinding = true)]
        [Permission(ShopConnectorPermissions.EditSkuMapping)]
        public ActionResult SkuMappingDelete(GridCommand command, SkuMappingModel model, int productId)
        {
            var entity = _connectorService.GetSkuMappingsById(model.Id);
            _connectorService.DeleteSkuMapping(entity);

            return SkuMappingList(command, productId);
        }

        #endregion
    }
}
