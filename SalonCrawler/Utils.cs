using System;
using System.Globalization;

namespace SalonCrawler
{
    public class Utils
    {
        private const string FullFormat = "dd.MM.yyyy HH:mm";
        private const string ShortFormat = "dd.MM HH:mm";
        private const string HourFormat = "HH:mm";

        public static DateTime ParseDate(string dateString)
        {
            if (dateString.Length == 5) // time only (today)
            {
                var date = DateTime.ParseExact(dateString, HourFormat, CultureInfo.InvariantCulture);
                return date;
            }
            var toParse = dateString[1] == '.' ? "0" + dateString : dateString;
            try
            {
                var date = DateTime.ParseExact(toParse, FullFormat, CultureInfo.InvariantCulture);
                return date;
            }
            catch (FormatException)
            {
                var date = DateTime.ParseExact(toParse, ShortFormat, CultureInfo.InvariantCulture);
                return date;
            }
        }
    }
}
