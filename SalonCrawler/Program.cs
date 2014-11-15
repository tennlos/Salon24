using System;
using System.Linq;
using NHibernate.Criterion;

namespace SalonCrawler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var session = NHibernateHelper.GetCurrentSession();
            var crawler = new Crawler(session, DateTime.MinValue, false, UserType.Publicist, 1000, 2, 2,
                CrawledColumns.Right);
            crawler.Crawl();
            session.Close();
            Logger.Log("Crawling finished.");
            Console.ReadLine();
        }   
    }
}
