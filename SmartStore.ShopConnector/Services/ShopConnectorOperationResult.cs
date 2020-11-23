using System.Net;
using System.Web.Mvc;
using SmartStore.Core.Infrastructure;
using SmartStore.Services.Localization;
using SmartStore.ShopConnector.Models;

namespace SmartStore.ShopConnector.Services
{
    public class ShopConnectorOperationResult : ActionResult
    {
        private readonly ShopConnectorRequestContext _context;
        private readonly OperationResultModel _model;

        public ShopConnectorOperationResult(ShopConnectorRequestContext context)
        {
            _context = context;
        }
        public ShopConnectorOperationResult(string message, bool hasError)
        {
            _context = null;
            _model = new OperationResultModel(message, hasError);
        }

        public override void ExecuteResult(ControllerContext context)
        {
            OperationResultModel model;

            if (_model != null)
            {
                model = _model;
            }
            else if (_context.ResponseModel != null)
            {
                model = _context.ResponseModel;
            }
            else
            {
                model = new OperationResultModel
                {
                    Description = _context.ToString()
                };

                if (_context.Status == HttpStatusCode.OK)
                    model.ShortMessage = _context.Status.ToString();
                else
                    model.ShortMessage = string.Concat(_context.Status.ToString(), " ", EngineContext.Current.Resolve<ILocalizationService>().GetResource("Admin.Common.UnknownError"));
            }

            var viewResult = new ViewResult
            {
                MasterName = "",
                ViewName = "OperationResult",
                ViewData = new ViewDataDictionary { Model = model }
            };

            viewResult.ExecuteResult(context);
        }
    }
}
