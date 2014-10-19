using System;
using System.Collections.Generic;
namespace SalonCrawler
{
    public class Logger
    {
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }

        public static void Log(Exception e)
        {
            Console.WriteLine("### EXCEPTION ###");
            Console.WriteLine(e.GetType().FullName);
            Console.WriteLine(e.Message);
            Console.WriteLine("### --------- ###");
        }
    }
}
