using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using SmartStore.Services;
using SmartStore.Utilities;

namespace SmartStore.ShopConnector.Extensions
{
    internal static class XmlExtensions
    {
        public static XPathNavigator GetContent(this XPathDocument doc)
        {
            return doc.CreateNavigator().SelectSingleNode("//Content");
        }

        public static FileDownloadManagerItem ToDownloadItem(this XPathNavigator nav, ICommonServices services, string imageDirectory, int displayOrder)
        {
            var image = new FileDownloadManagerItem
            {
                Id = nav.GetValue<int>("Id"),
                Url = nav.GetString("FullSizeImageUrl"),
                MimeType = nav.GetString("MimeType"),
                DisplayOrder = displayOrder
            };

            if (image.Url.HasValue())
            {
                var localPath = string.Empty;

                try
                {
                    // Exclude query string parts!
                    localPath = new Uri(image.Url).LocalPath;
                }
                catch { }

                image.Url = services.WebHelper.ModifyQueryString(image.Url, "q=100", null);

                if (image.Id == 0)
                {
                    image.Id = CommonHelper.GenerateRandomInteger();
                }

                image.FileName = image.Id.ToString() + "-" + (Path.GetFileName(localPath).ToValidFileName().NullEmpty() ?? Path.GetRandomFileName());
                image.Path = Path.Combine(imageDirectory, image.FileName);

                return image;
            }

            return null;
        }

        public static void AddDownloadItem(this List<FileDownloadManagerItem> images, FileDownloadManagerItem item)
        {
            if (item != null && !images.Any(x => x.Path.IsCaseInsensitiveEqual(item.Path) || x.Id == item.Id))
            {
                images.Add(item);
            }
        }

        public static void ReadFragments(this XmlReader reader, string subtreeName, string childNodeName, bool readFragments, Func<XPathNavigator, bool> processFragments)
        {
            //var fragmentSize = 1024 * 1024 * 2; // 2MB
            var fragments = new StringBuilder();
            var fragmentsCount = 0;
            var stop = false;

            if (reader.ReadToFollowing(subtreeName))
            {
                using (var subReader = reader.ReadSubtree())
                {
                    var siblingDepth = subReader.Depth + 1;
                    while (!subReader.EOF && !stop)
                    {
                        if (subReader.Depth == siblingDepth && subReader.NodeType == XmlNodeType.Element && subReader.Name == childNodeName)
                        {
                            var xml = subReader.ReadOuterXml();
                            fragments.Append(xml);

                            //if (readFragments && fragments.Length > fragmentSize)
                            if (readFragments && ++fragmentsCount >= 100)
                            {
                                fragmentsCount = 0;
                                CallFragmentProcessor();
                            }
                        }
                        else
                        {
                            subReader.Read();
                        }
                    }
                }

                if (fragments.Length > 0 && !stop)
                {
                    CallFragmentProcessor();
                }
            }

            void CallFragmentProcessor()
            {
                using (var strReader = new StringReader(fragments.ToString()))
                using (var subtreeReader = XmlReader.Create(strReader, new XmlReaderSettings { CheckCharacters = false, ConformanceLevel = ConformanceLevel.Fragment }))
                {
                    var len = fragments.Length;
                    fragments.Clear();

                    var doc = new XPathDocument(subtreeReader);
                    if (!processFragments(doc.CreateNavigator()))
                    {
                        stop = true;
                    }
                }
            }
        }
    }
}