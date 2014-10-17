using NHibernate;
using NHibernate.Cfg;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace SalonCrawler
{
    public class Program
    {
        
        static void Main(string[] args)
        {
            ISession session = NHibernateHelper.GetCurrentSession();
            Crawler crawler = new Crawler();
            crawler.Crawl();
        }

        
    }
}
