using System;
using System.Collections.Generic;
using System.Xml;
using SmartStore.Core;
using SmartStore.Core.Domain.Catalog;
using SmartStore.Core.Domain.DataExchange;
using SmartStore.Core.Plugins;
using SmartStore.Services.DataExchange.Export;
using SmartStore.ShopConnector.Services;

namespace SmartStore.ShopConnector.ExportProvider
{
    [SystemName("Exports.ShopConnectorCategoryXml")]
    [FriendlyName("Shop-Connector XML Category Export")]
    [IsHidden(true)]
    [ExportFeatures(Features = ExportFeatures.CanOmitCompletionMail)]
    public class ShopConnectorCategoryXmlExportProvider : ExportProviderBase
    {
        public static string SystemName => "Exports.ShopConnectorCategoryXml";

        public override ExportEntityType EntityType => ExportEntityType.Category;

        public override string FileExtension => "XML";

        protected override void Export(ExportExecuteContext context)
        {
            var ignored = 0;
            var categoryIds = context.CustomProperties["CategoryIds"] as HashSet<int>;

            using (var writer = XmlWriter.Create(context.DataStream, ShopConnectorService.DefaultSettings))
            {
                var helper = new ExportXmlHelper(writer, true);

                writer.WriteStartElement("Content");
                writer.WriteStartElement("Categories");
                writer.WriteAttributeString("Version", SmartStoreVersion.CurrentVersion);

                while (context.Abort == DataExchangeAbortion.None && context.DataSegmenter.ReadNextSegment())
                {
                    var segment = context.DataSegmenter.CurrentSegment;

                    foreach (dynamic category in segment)
                    {
                        if (context.Abort != DataExchangeAbortion.None)
                            break;

                        Category entity = category.Entity;

                        try
                        {
                            if (categoryIds != null && !categoryIds.Contains(entity.Id))
                            {
                                ++ignored;
                            }
                            else
                            {
                                helper.WriteCategory(category, "Category");

                                ++context.RecordsSucceeded;
                            }
                        }
                        catch (OutOfMemoryException)
                        {
                            context.Abort = DataExchangeAbortion.Hard;
                            throw;
                        }
                        catch (Exception ex)
                        {
                            context.RecordException(ex, entity.Id);
                        }
                    }
                }

                writer.WriteElementString("Success", context.RecordsSucceeded.ToString());
                writer.WriteElementString("Failure", context.RecordsFailed.ToString());
                writer.WriteElementString("TotalRecords", (context.DataSegmenter.TotalRecords - ignored).ToString());

                writer.WriteEndElement();   // Categories
                writer.WriteEndElement();   // Content
            }
        }
    }
}
