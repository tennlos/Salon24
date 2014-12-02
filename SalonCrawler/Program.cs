using System;

namespace SalonCrawler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var session = NHibernateHelper.GetCurrentSession();
            var crawler = new Crawler(session, DateTime.MinValue, false, UserType.Common, 1000, 19, 21,
                CrawledColumns.Both);
            crawler.Crawl();
//            crawler.CrawlOnlyNewPosts();
            session.Close();
            Logger.Log("Crawling finished.");
            Console.ReadLine();
        }   
    }
}
