using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using SmartStore.Core;
using SmartStore.ShopConnector.Models;
using SmartStore.Web.Framework.WebApi;
using SmartStore.Web.Framework.WebApi.Security;

namespace SmartStore.ShopConnector.Services
{
    public class ShopConnectorConsumer : HmacAuthentication
    {
        public static bool BodySupported(string method)
        {
            if (!string.IsNullOrWhiteSpace(method) && string.Compare(method, "GET", true) != 0 && string.Compare(method, "DELETE", true) != 0)
            {
                return true;
            }

            return false;
        }

        private bool GetResponse(HttpWebResponse webResponse, ShopConnectorRequestContext context)
        {
            context.Status = webResponse.StatusCode;
            context.StatusDescription = webResponse.StatusDescription;
            context.Success = context.Status == HttpStatusCode.OK;

            try
            {
                foreach (var key in webResponse.Headers.AllKeys.Where(x => x.EmptyNull().StartsWith("Sm-ShopConnector-", StringComparison.OrdinalIgnoreCase)))
                {
                    context.Headers[key] = webResponse.Headers[key].EmptyNull();
                }

                if (context.Success)
                {
                    if (context.ResponsePath.HasValue())
                    {
                        using (var fileStream = new FileStream(context.ResponsePath, FileMode.Create, FileAccess.Write))
                        using (var stream = webResponse.GetResponseStream())
                        {
                            stream.CopyTo(fileStream);//, 81920
                        }
                    }
                }
                else
                {
                    var errorMsg = webResponse.Headers["Sm-ShopConnector-ErrorMessageShort"];
                    if (errorMsg.HasValue())
                    {
                        context.ResponseModel = new OperationResultModel(errorMsg);
                    }
                    else
                    {
                        using (var reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                        {
                            var rawResponse = reader.ReadToEnd();
                            context.ResponseModel = XmlHelper.Deserialize<OperationResultModel>(rawResponse);

                            if (context?.ResponseModel?.ShortMessage?.IsEmpty() ?? true)
                            {
                                context.ResponseModel = new OperationResultModel(rawResponse);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                context.ResponseModel = new OperationResultModel(ex.ToAllMessages());
            }

            return true;
        }

        public HttpWebRequest StartRequest(ShopConnectorRequestContext context)
        {
            if (context == null || !context.IsValid)
            {
                context.ResponseModel = new OperationResultModel("Invalid request context")
                {
                    Description = ((WebApiRequestContext)context).ToString()
                };
                return null;
            }

            HttpWebRequest request;
            byte[] data = null;
            string contentMd5Hash = "";
            string timestamp = DateTime.UtcNow.ToString("o");

            var queryStringEncoded = context.RequestContent.BuildQueryString(null, true);
            var queryStringDecoded = context.RequestContent.BuildQueryString(null, false);

            if (!BodySupported(context.HttpMethod) && queryStringDecoded.HasValue())
            {
                request = (HttpWebRequest)WebRequest.Create(context.Url + "?" + queryStringEncoded);

                context.Url = context.Url + "?" + queryStringDecoded;
            }
            else
            {
                request = (HttpWebRequest)WebRequest.Create(context.Url);
            }

            request.UserAgent = "Smartstore Shop-Connector Consumer";       // optional
            request.Method = context.HttpMethod;
            request.KeepAlive = false;
            request.Timeout = Timeout.Infinite;

            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");                     // Compress.
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;  // Decompress.

            request.Headers.Add(HttpRequestHeader.Pragma, "no-cache");
            request.Headers.Add(HttpRequestHeader.CacheControl, "no-cache, no-store");

            request.Accept = context.HttpAcceptType;
            request.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");

            request.Headers.Add(ShopConnectorCore.Header.PublicKey, context.PublicKey);
            request.Headers.Add(ShopConnectorCore.Header.Date, timestamp);
            request.Headers.Add(ShopConnectorCore.Header.Version, context.Version);

            if (BodySupported(request.Method))
            {
                if (queryStringEncoded.HasValue())
                {
                    data = Encoding.UTF8.GetBytes(queryStringEncoded.ToString());

                    request.ContentLength = data.Length;
                    request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";

                    contentMd5Hash = CreateContentMd5Hash(data);
                }
                else
                {
                    request.ContentLength = 0;
                }
            }

            var messageRepresentation = CreateMessageRepresentation(context, contentMd5Hash, timestamp);
            var signature = CreateSignature(context.SecretKey, messageRepresentation);

            request.Headers.Add(HttpRequestHeader.Authorization, CreateAuthorizationHeader(signature));

            if (data != null)
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }

            return request;
        }

        public bool ProcessResponse(ShopConnectorRequestContext context, HttpWebRequest webRequest)
        {
            if (webRequest == null)
            {
                return false;
            }

            context.Success = false;
            HttpWebResponse webResponse = null;

            try
            {
                webResponse = webRequest.GetResponse() as HttpWebResponse;
                GetResponse(webResponse, context);
            }
            catch (WebException webexception)
            {
                webResponse = webexception.Response as HttpWebResponse;

                if (webResponse != null && GetResponse(webResponse, context))
                {
                    // nothing to do
                }
                else
                {
                    context.ResponseModel = new OperationResultModel(webexception.ToString());
                }
            }
            catch (Exception ex)
            {
                context.ResponseModel = new OperationResultModel(ex);
            }
            finally
            {
                if (webResponse != null)
                {
                    webResponse.Close();
                    webResponse.Dispose();
                }
            }

            return context.Success;
        }
    }


    public class ShopConnectorRequestContext : WebApiRequestContext
    {
        public ShopConnectorRequestContext()
        {
            ActionMethod = string.Empty;
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RequestContent = new NameValueCollection();
        }

        public string ActionMethod { get; set; }
        public NameValueCollection RequestContent { get; set; }

        public CachedConnection Connection { get; set; }
        public string Version { get; set; }
        public bool Success { get; set; }
        public HttpStatusCode? Status { get; set; }
        public string StatusDescription { get; set; }

        public string ResponsePath { get; set; }
        public OperationResultModel ResponseModel { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public override string ToString()
        {
            var str = "Success: {0}, Status: {1} ({2})".FormatInvariant(
                Success,
                (int)(Status.HasValue ? Status.Value : 0),
                Status.HasValue ? Status.Value.ToString() : "".NaIfEmpty()
            );

            if (StatusDescription.HasValue() && Status.HasValue && !StatusDescription.IsCaseInsensitiveEqual(Status.Value.ToString()))
                return str + ". Description: " + StatusDescription;

            return str;
        }
    }
}