using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json.Linq;

namespace SluhyWeatherClient.Controllers
{
    public class HomeController : Controller
    {
        [OutputCache(Duration = 300)]
        public JObject Index()
        {
            var breakPoints = new Queue<Tuple<string, string>>();
            breakPoints.Enqueue(new Tuple<string, string>("Temperature", "Current:"));
            breakPoints.Enqueue(new Tuple<string, string>("DewPoint", "Current:"));
            breakPoints.Enqueue(new Tuple<string, string>("Humidity", "Current:"));
            breakPoints.Enqueue(new Tuple<string, string>("WindChill", "Current:"));
            breakPoints.Enqueue(new Tuple<string, string>("HeatIndex", "Current:"));
            breakPoints.Enqueue(new Tuple<string, string>("WindRun", "Hour:"));
            breakPoints.Enqueue(new Tuple<string, string>("Wind", "Current Direction:"));
            breakPoints.Enqueue(new Tuple<string, string>("Barometer", "Current:"));
            breakPoints.Enqueue(new Tuple<string, string>("Rain", "Rate:"));

            var result = new JObject();
            
            using (WebClient client = new WebClient()) // WebClient class inherits IDisposable
            {
                string htmlCode = client.DownloadString(ConfigurationManager.AppSettings["Endpoint"]);
                var lines = htmlCode.Split(new string[] {"\n\t"}, StringSplitOptions.RemoveEmptyEntries);
                List<string> groups = new List<string>();

                var read = false;
                var lineStr = "";
                foreach (string line in lines)
                {
                    if (read && (line.Contains("<tr height=\"20\">") || line.Contains("</tr>")))
                    {
                        read = false;
                        if (String.IsNullOrEmpty(lineStr) == false)
                        {
                            groups.Add(lineStr);
                        }
                        lineStr = "";
                    }

                    if (read == false && line.Contains("<tr height=\"20\">"))
                    {
                        read = true;
                    }

                    if (read)
                    {
                        lineStr += line;
                    }

                }

                var tuples = ParseTuples(groups);
                var current = new JObject();
                foreach (var tuple in tuples)
                {
                    if (breakPoints.Count > 0 && breakPoints.Peek().Item2 == tuple.Item1)
                    {
                        current = new JObject();
                        result.Add(breakPoints.Dequeue().Item1, current);
                    }
                    
                    current.Add(GetCleanLabel(tuple.Item1), tuple.Item2);
                }

            }

            var lastUpdated = DateTime.Now;
            result.Add("LastUpdated", lastUpdated.ToString("yyyy-MM-dd HH:mm:ss"));
            result.Add("LastUpdatedCZ", lastUpdated.ToString("dd. MM. yyyy v HH:mm"));

            Response.ContentType = "application/json";
            return result;
        }

        private List<Tuple<string, string>> ParseTuples(List<string> values)
        {
            var results = new List<Tuple<string, string>>();
            values.ForEach(x => results.Add(ParseTuple(x)));
            return results;
        }

        private string GetCleanLabel(string label)
        {
            if (label.StartsWith("Year ("))
            {
                return "Year";
            }
            return label.Replace(":", String.Empty).Replace(" ", String.Empty).Replace("(", String.Empty).Replace(")", String.Empty);
        } 

        private Tuple<string, string> ParseTuple(string value)
        {
            var t = Regex.Match(value, "<strong>(.*)</strong>", RegexOptions.Singleline).Groups[1].Value;
            var v = Regex.Match(value, "<b>(.*)</b>", RegexOptions.Singleline).Groups[1].Value;
            var add = String.Empty;

            if (v.Contains("at&nbsp;"))
            {
                add = " v "+  v.Substring(v.Length - 5);
            }

            if (v.Contains("on&nbsp;"))
            {
                var dateStr = v.Substring(v.Length - 10);
                var date = DateTime.ParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                add = " dne " + String.Format("{0}. {1}. {2}", date.Day, date.Month, date.Year);
            }

            return new Tuple<string,string>(t, GetCleanValue(v) + add);
        }

        private string GetCleanValue(string value)
        {
            return value.SubstringUpToFirst('&').Replace("degrees", "stupňů").Replace(".",",").Replace(" C", " °C");
        }
        
    }
}