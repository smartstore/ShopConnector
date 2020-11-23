using System.Text;
using System.Web.Mvc;
using SmartStore.ShopConnector.Models;

namespace SmartStore.ShopConnector.Extensions
{
    public static class HtmlHelperExtensions
    {
        public static string GridButtons<T>(this HtmlHelper<T> helper, bool isForExport)
        {
            var sb = new StringBuilder();
            var model = helper.ViewData.Model as ConfigurationModel;
            var uh = new UrlHelper(helper.ViewContext.RequestContext);
            var updateUrl = uh.Action("ConnectionUpsert", "ShopConnector", new { area = ShopConnectorPlugin.SystemName });
            var deleteUrl = uh.Action("ConnectionDelete", "ShopConnector", new { area = ShopConnectorPlugin.SystemName });
            var imExClass = isForExport ? "action-export" : "action-import";

            sb.AppendFormat("<div class='btn-group'>");
            sb.AppendFormat("<button type='button' class='btn btn-secondary dropdown-toggle' data-toggle='dropdown' aria-haspopup='true' aria-expanded='false'><i class='fa fa-fw fa-ellipsis-h'></i></button>");
            sb.Append("<div class='dropdown-menu dropdown-menu-right'>");

            // Update.
            sb.AppendFormat("<a href='javascript:void(0)' class='dropdown-item connection-action {0} action-update' data-url='{1}' data-id='<#= Id #>'>" +
                "<i class='far fa-pen-square'></i><span>{2}</span></a>",
                imExClass, updateUrl, model.Strings["Admin.Common.Edit"]);

            // Delete.
            sb.AppendFormat("<a href='javascript:void(0)' class='dropdown-item connection-action {0} action-delete' data-url='{1}' data-id='<#= Id #>'>" +
                "<i class='far fa-trash-alt'></i><span>{2}</span></a>",
                imExClass, deleteUrl, model.Strings["Admin.Common.Delete"]);

            if (!isForExport)
            {
                sb.Append("<div class='dropdown-divider'></div>");

                sb.AppendFormat("<a href='javascript:void(0)' class='dropdown-item connection-action {0} action-about' data-url='{1}' data-id='<#= Id #>' title='{2}'>" +
                    "<i class='fa fa-fw fa-info-circle'></i><span>{3}</span></a>",
                    imExClass, model.ImportUrls["About"], model.Strings["Action.About.Hint"], model.Strings["Admin.Common.About"]);

                sb.AppendFormat("<a href='javascript:void(0)' class='dropdown-item connection-action {0} action-product-data' data-url='{1}' data-id='<#= Id #>' title='{2}'>" +
                    "<i class='far fa-fw fa-file-code'></i><span>{3}</span></a>",
                    imExClass, model.ImportUrls["ProductData"], model.Strings["Action.ProductData.Hint"], model.Strings["Action.ProductData"]);

                sb.AppendFormat("<a href='javascript:void(0)' class='dropdown-item connection-action {0} action-product-import' data-url='{1}' data-id='<#= Id #>' title='{2}' data-urlprogress='{3}'>" +
                    "<i class=\"fa fa-fw fa-cogs\"></i><span>{4}</span></a>",
                    imExClass, model.ImportUrls["ProductFileSelect"], model.Strings["Action.ProductImport.Hint"], model.ImportUrls["ProductImportProgress"], model.Strings["Action.ProductImport"]);
            }

            sb.Append("</div></div>");

            return sb.ToString();
        }

        public static string NoteAndUrlLabel<T>(this HtmlHelper<T> helper)
        {
            var link = "<a href='<#= Url #>' target='_blank'><#= Url #></a>";
            var result = "<span class='label label-smnet label-<#= NoteLabelHint #>'><#= Note #></span>" + link;

            return result;
        }
    }
}