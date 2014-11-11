using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace SalonCrawler
{
    public static class CrawlerHelper
    {
        public static HtmlNode GetNodeByClass(HtmlNode node, string hclass, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == hclass);
            var htmlNodes = k as HtmlNode[] ?? k.ToArray();
            return !htmlNodes.Any() ? null : htmlNodes.ElementAt(index);
        }

        public static HtmlNode GetNodeByPartialClass(HtmlNode node, string hclass, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains(hclass));
            return k.ElementAt(index);
        }

        public static HtmlNode GetNodeById(HtmlNode node, string hid, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["id"] != null && x.Attributes["id"].Value == hid);
            return k.ElementAt(index);
        }

        public static HtmlNode GetNodeByTag(HtmlNode node, string htag, int index = 0)
        {
            var k = node.Descendants(htag);
            return k.ElementAt(index);
        }

        public static IEnumerable<HtmlNode> GetAllNodesByTagAndClass(HtmlNode node, string htag, string hclass)
        {
            return node.Descendants(htag).Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == hclass);
        } 

        public static string GetStringValueByClass(HtmlNode node, string hclass, int index=0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == hclass);
            var htmlNodes = k as HtmlNode[] ?? k.ToArray();
            return !htmlNodes.Any() ? null : htmlNodes.ElementAt(index).InnerText;
        }

        public static string GetStringValueByPartialClass(HtmlNode node, string hclass, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value.Contains(hclass));
            return k.ElementAt(index).InnerText;
        }

        public static string GetStringValueById(HtmlNode node, string hid, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["id"] != null && x.Attributes["id"].Value == hid);
            return k.ElementAt(index).InnerText;
        }

        public static string GetStringValueByTag(HtmlNode node, string htag, int index = 0)
        {
            var k = node.Descendants(htag);
            return k.ElementAt(index).InnerText;
        }

        public static string GetStringValueByTagAndClass(HtmlNode node, string htag, string hclass, int index = 0)
        {
            var k = node.Descendants(htag).Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == hclass);
            return k.ElementAt(index).InnerText;
        }

        public static IList<string> GetAllStringValuesByTag(HtmlNode node, string htag)
        {
            var k = node.Descendants(htag);
            return k.Select(htmlNode => htmlNode.InnerText).ToList();
        }
    }
}
