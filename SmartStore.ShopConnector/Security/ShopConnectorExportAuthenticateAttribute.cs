using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using SmartStore.Core;
using SmartStore.Core.Infrastructure;
using SmartStore.Core.Logging;
using SmartStore.Services.Localization;
using SmartStore.ShopConnector.Extensions;
using SmartStore.ShopConnector.Services;
using SmartStore.Web.Framework.WebApi.Security;

namespace SmartStore.ShopConnector.Security
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ShopConnectorExportAuthenticateAttribute : AuthorizeAttribute
    {
        protected HmacAuthentication _hmac = new HmacAuthentication();
        protected ShopConnectorAuthResult _result = ShopConnectorAuthResult.FailedForUnknownReason;

        protected string CreateContentMd5Hash(HttpRequestBase request)
        {
            if (request != null && request.InputStream != null)
            {
                request.InputStream.Position = 0;

                using (var reader = new StreamReader(request.InputStream))
                {
                    string content = reader.ReadToEndAsync().Result;

                    if (content.HasValue())
                    {
                        byte[] contentBytes = Encoding.UTF8.GetBytes(content);

                        if (contentBytes != null && contentBytes.Length > 0)
                            return _hmac.CreateContentMd5Hash(contentBytes);
                    }
                }
            }
            return "";
        }

        protected virtual void LogUnauthorized(HttpContextBase httpContext)
        {
            try
            {
                var logger = EngineContext.Current.Resolve<ILoggerFactory>().GetLogger(this.GetType());
                var localize = EngineContext.Current.Resolve<ILocalizationService>();
                var model = _result.CreateAuthErrorModel(localize, httpContext);

                logger.Warn(new Exception(model.Description), model.ShortMessage);
            }
            catch (Exception ex)
            {
                ex.Dump();
            }
        }

        protected virtual ShopConnectorAuthResult IsAuthenticated(HttpContextBase httpContext, DateTime now, ShopConnectorControllingData controllingData)
        {
            var request = httpContext.Request;
            DateTime headDateTime;

            if (request == null)
                return ShopConnectorAuthResult.FailedForUnknownReason;

            if (controllingData.ConnectorUnavailable)
                return ShopConnectorAuthResult.ConnectorUnavailable;

            if (!controllingData.IsExportEnabled)
                return ShopConnectorAuthResult.ExportDeactivated;

            //string headContentMd5 = request.Headers["Content-Md5"] ?? request.Headers["Content-MD5"];
            string headTimestamp = request.Headers[ShopConnectorCore.Header.Date];
            string headPublicKey = request.Headers[ShopConnectorCore.Header.PublicKey];
            string action = request.RequestContext.RouteData.GetRequiredString("action");
            bool forExport = !action.IsCaseInsensitiveEqual("Notification");

            string[] authorization = request.Headers["Authorization"].SplitSafe(" ");

            if (string.IsNullOrWhiteSpace(headPublicKey))
                return ShopConnectorAuthResult.ConnectionInvalid;

            if (authorization.Length != 2 || !_hmac.IsAuthorizationHeaderValid(authorization[0], authorization[1]))
                return ShopConnectorAuthResult.InvalidAuthorizationHeader;

            if (!_hmac.ParseTimestamp(headTimestamp, out headDateTime))
                return ShopConnectorAuthResult.InvalidTimestamp;

            int maxMinutes = (controllingData.ValidMinutePeriod <= 0 ? ShopConnectorCore.DefaultTimePeriodMinutes : controllingData.ValidMinutePeriod);

            if (Math.Abs((headDateTime - now).TotalMinutes) > maxMinutes)
                return ShopConnectorAuthResult.TimestampOutOfPeriod;

            var connection = controllingData.Connections.FirstOrDefault(x => x.IsForExport == forExport && x.PublicKey == headPublicKey);
            if (connection == null)
                return ShopConnectorAuthResult.ConnectionUnknown;

            if (!connection.IsActive)
                return ShopConnectorAuthResult.ConnectionDisabled;

            if (connection.LastRequestUtc.HasValue && headDateTime <= connection.LastRequestUtc.Value)
                return ShopConnectorAuthResult.TimestampOlderThanLastRequest;

            var context = new ShopConnectorRequestContext()
            {
                HttpMethod = request.HttpMethod,
                HttpAcceptType = request.Headers["Accept"],
                PublicKey = headPublicKey,
                SecretKey = connection.SecretKey,
                Url = HttpUtility.UrlDecode(request.Url.AbsoluteUri.ToLower())
            };

            string contentMd5 = CreateContentMd5Hash(httpContext.Request);

            string messageRepresentation = _hmac.CreateMessageRepresentation(context, contentMd5, headTimestamp);

            if (string.IsNullOrEmpty(messageRepresentation))
                return ShopConnectorAuthResult.MissingMessageRepresentationParameter;

            string signatureProvider = _hmac.CreateSignature(connection.SecretKey, messageRepresentation);

            if (signatureProvider != authorization[1])
                return ShopConnectorAuthResult.InvalidSignature;

            controllingData.ConnectionsUpdated = true;
            connection.LastRequestUtc = now;

            if (connection.RequestCount < long.MaxValue)
                ++connection.RequestCount;

            return ShopConnectorAuthResult.Success;
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // first OnAuthorization then AuthorizeCore
            //_filterContext = filterContext;

            base.OnAuthorization(filterContext);
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            httpContext.Response.Clear();
            httpContext.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            _result = ShopConnectorAuthResult.FailedForUnknownReason;

            var controllingData = ConnectionCache.ControllingData();
            var now = DateTime.UtcNow;

            try
            {
                _result = IsAuthenticated(httpContext, now, controllingData);
            }
            catch (Exception ex)
            {
                ex.Dump();
            }

            if (_result == ShopConnectorAuthResult.Success)
            {
                var response = httpContext.Response;

                response.AddHeader(ShopConnectorCore.Header.Version, controllingData.Version);
                response.AddHeader(ShopConnectorCore.Header.Date, now.ToString("o"));
            }
            else
            {
                var headers = httpContext.Response.Headers;

                headers.Add("WWW-Authenticate", ShopConnectorCore.Header.WwwAuthenticate);

                headers.Add(ShopConnectorCore.Header.Date, now.ToString("o"));

                headers.Add(ShopConnectorCore.Header.AuthResultId, ((int)_result).ToString());
                headers.Add(ShopConnectorCore.Header.AuthResultDescription, _result.ToString());

                if (controllingData.LogUnauthorized)
                {
                    LogUnauthorized(httpContext);
                }
            }

            return _result == ShopConnectorAuthResult.Success;
        }

        protected override HttpValidationStatus OnCacheAuthorization(HttpContextBase httpContext)
        {
            return HttpValidationStatus.IgnoreThisRequest;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.HttpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            filterContext.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;

            var localize = EngineContext.Current.Resolve<ILocalizationService>();
            var model = _result.CreateAuthErrorModel(localize, filterContext.HttpContext);

            filterContext.Result = new ContentResult
            {
                Content = XmlHelper.Serialize(model)
            };
        }
    }
}