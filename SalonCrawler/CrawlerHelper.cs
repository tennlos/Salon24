using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace SalonCrawler
{
    public static class CrawlerHelper
    {
        public static string GetStringValue(HtmlNode node, string hclass, int index=0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == hclass);
            return k.ElementAt(index).InnerText; 
        }

        public static HtmlNode GetNode(HtmlNode node, string hclass, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["class"] != null && x.Attributes["class"].Value == hclass);
            return k.ElementAt(index);
        }

        public static string GetStringValueFromId(HtmlNode node, string hclass, int index = 0)
        {
            var k = node.DescendantsAndSelf().Where(x => x.Attributes["id"] != null && x.Attributes["id"].Value == hclass);
            return k.ElementAt(index).InnerText;
        }
    }
}
