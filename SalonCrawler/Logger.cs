using System;
using System.IO;

namespace SalonCrawler
{
    public class Logger
    {
        private const string Filename = "crawler_log.log";

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

            using (var logStream = File.AppendText(Filename))
            {
                logStream.WriteLine(DateTime.Now);
                logStream.WriteLine(e.GetType().FullName);
                logStream.WriteLine(e.Message);
                logStream.Write(e.StackTrace);
                logStream.WriteLine();
                logStream.WriteLine();
            }
        }
    }
}
