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

        public static void Log(Exception e, object entity)
        {
            Console.WriteLine("### EXCEPTION ###");
            Console.WriteLine(e.GetType().FullName);
            Console.WriteLine(e.Message);
            Console.WriteLine("### --------- ###");

            var type = "-";
            var info = "-";
            var address = "-";

            var category = entity as Category;
            if (category != null)
            {
                type = "Category";
                info = category.Code;
                address = "-";
            }

            var user = entity as User;
            if (user != null)
            {
                type = "User";
                info = user.Nick;
                address = user.Address;
            }

            var post = entity as Post;
            if (post != null)
            {
                type = "Post";
                info = post.Title;
                address = post.Address;
            }

            var str = entity as string;
            if (str != null)
            {
                type = str;
                info = "-";
                address = "-";
            }

            using (var logStream = File.AppendText(Filename))
            {
                logStream.WriteLine(DateTime.Now);
                logStream.WriteLine(type);
                logStream.WriteLine(info);
                logStream.WriteLine(address);
                logStream.WriteLine(e.GetType().FullName);
                logStream.WriteLine(e.Message);
                logStream.Write(e.StackTrace);
                if (e.InnerException != null)
                {
                    logStream.WriteLine();
                    logStream.Write("Inner:");
                    logStream.Write(e.InnerException.GetType().FullName);
                    logStream.WriteLine(e.Message);
                    logStream.Write(e.StackTrace);
                }
                logStream.WriteLine();
                logStream.WriteLine();
            }
        }
    }
}
