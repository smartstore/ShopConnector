using System;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SmartStore.Core;
using SmartStore.Core.Infrastructure;
using SmartStore.Services.Localization;
using SmartStore.ShopConnector.Extensions;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector.Security
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ShopConnectorCompatibilityAttribute : AuthorizeAttribute
    {
        protected ShopConnectorAuthResult _result = ShopConnectorAuthResult.FailedForUnknownReason;
        protected string _message = null;

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // first OnAuthorization then AuthorizeCore
            base.OnAuthorization(filterContext);
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            httpContext.Response.Clear();
            httpContext.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            int version = 0;
            var rawVersion = httpContext.Request.Headers[ShopConnectorCore.Header.Version];
            string connectorVersion, pluginVersion;

            if (rawVersion.SplitToPair(out connectorVersion, out pluginVersion, " "))
                version = connectorVersion.ToInt();

            if (version == ShopConnectorCore.ConnectorVersion)
            {
                _result = ShopConnectorAuthResult.Success;
                _message = null;
            }
            else
            {
                _result = ShopConnectorAuthResult.IncompatibleVersion;

                var key = (version > ShopConnectorCore.ConnectorVersion ? "Plugins.SmartStore.ShopConnector.PluginOutOfDateMe" : "Plugins.SmartStore.ShopConnector.PluginOutOfDateHe");

                _message = EngineContext.Current.Resolve<ILocalizationService>().GetResource(key);
            }

            if (_result != ShopConnectorAuthResult.Success)
            {
                var headers = httpContext.Response.Headers;

                headers.Add(ShopConnectorCore.Header.Date, DateTime.UtcNow.ToString("o"));
                headers.Add(ShopConnectorCore.Header.AuthResultId, ((int)_result).ToString());
                headers.Add(ShopConnectorCore.Header.AuthResultDescription, _result.ToString());
            }

            return _result == ShopConnectorAuthResult.Success;
        }

        protected override HttpValidationStatus OnCacheAuthorization(HttpContextBase httpContext)
        {
            return HttpValidationStatus.IgnoreThisRequest;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;

            var localize = EngineContext.Current.Resolve<ILocalizationService>();
            var model = _result.CreateAuthErrorModel(localize, filterContext.HttpContext, _message);

            filterContext.Result = new ContentResult
            {
                Content = XmlHelper.Serialize(model)
            };
        }
    }
}