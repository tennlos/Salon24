using System;

namespace SalonCrawler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var session = NHibernateHelper.GetCurrentSession();
            var crawler = new Crawler(session, 10000);
            crawler.Crawl();
            session.Close();
            Logger.Log("Crawling finished.");
            Console.Read();
        }   
    }
}
