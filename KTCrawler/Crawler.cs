using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

using static KTCrawler.UrlChecks;

namespace KTCrawler
{
    static class Crawler
    {
        private static string StartingUrl;
        private static string BaseUrl;
        private static string Subdomain;

        private static bool LimitedPageVisits;
        private static int RemainingPageVisits;
        private static List<string> UnvisitedLinks = new List<string>();
        private static Dictionary<string, List<String>> VisitedLinks = new Dictionary<string, List<String>>();

        private static bool RobotsTxt = false;
        private static List<string> Disallowed = new List<string>();
        private static bool ObservseCrawlDelay = true;
        private static int CrawlDelay = 0;


        public static void Crawl(string url, bool observeCrawlDelay)
        {
            Crawl(url, observeCrawlDelay, Tuple.Create(false, 0));
        }

        /// <summary>
        /// Pass maxPageVisits = (false, x) to not limit requests
        /// </summary>
        /// <param name="url"></param>
        /// <param name="observeCrawlDelay"></param>
        /// <param name="maxPageVisits"></param>
        public static void Crawl(string url, bool observeCrawlDelay, Tuple<bool, int> maxPageVisits)
        {
            StartingUrl = url;
            ObservseCrawlDelay = observeCrawlDelay;
            LimitedPageVisits = maxPageVisits.Item1;
            RemainingPageVisits = maxPageVisits.Item1 ? maxPageVisits.Item2 : 1;

            if (!IsValidScheme(StartingUrl))
            {
                Console.WriteLine("Please provide a valid http/https starting URL");
                return;
            }

            if (IsHttps(StartingUrl))
                BaseUrl = GetDomain(StartingUrl);
            else
                BaseUrl = GetDomain(StartingUrl);

            RobotsTxt = ProcessRobotsTxt();
            if (RobotsTxt && Disallowed.Contains("/"))
            {
                Console.WriteLine("Site's robots.txt disallows crawlers at all locations");
                return;
            }

            Subdomain = GetSubdomain(GetDomain(StartingUrl));

            UnvisitedLinks.Add(SanitiseUrl(StartingUrl, BaseUrl));
            while (RemainingPageVisits > 0 && UnvisitedLinks.Any())
            {
                ProcessLinks(UnvisitedLinks[0], GetLinks(GetHtml(UnvisitedLinks[0])));

                if (LimitedPageVisits)
                    RemainingPageVisits--;
            }

            WriteSitemapToFile(maxPageVisits.Item2);
        }

        /// <summary>
        /// Outputs text sitemap in project directory.
        /// </summary>
        private static void WriteSitemapToFile(int maxRequests)
        {
            using (StreamWriter file = new StreamWriter(@"..\..\Sitemap.txt"))
            {
                file.WriteLine($"Sitemap from {StartingUrl}");

                if (LimitedPageVisits)
                    file.WriteLine($"Limited to {maxRequests} requests");

                file.WriteLine("");

                foreach (var visited in VisitedLinks)
                {
                    file.WriteLine($"{visited.Key} ({visited.Value.Count} links)");

                    foreach (var child in visited.Value)
                    {
                        file.WriteLine($"-- {child}");
                    }

                    file.WriteLine("");
                }

                file.WriteLine("");


                if (UnvisitedLinks.Count > 0)
                {
                    file.WriteLine("Did not visit:");

                    foreach (var unvisited in UnvisitedLinks)
                    {
                        file.WriteLine(unvisited);
                    }
                }
            }

            Console.WriteLine("Sitemap saved in Sitemap.txt");
        }

        /// <summary>
        /// Attempts to read robots.txt for the domain and captures relevant information from it.
        /// If file not found will fail silently.
        /// </summary>
        /// <returns>True if robots.txt file found and contains directives for modified crawler behaviour</returns>
        private static bool ProcessRobotsTxt()
        {
            var request = (HttpWebRequest)WebRequest.Create($"{BaseUrl}/robots.txt");

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var dataStream = new StreamReader(responseStream))
                {
                    bool agent = false;
                    while (dataStream.Peek() >= 0)
                    {
                        string line = dataStream.ReadLine().Trim().ToLower();

                        if (line.StartsWith("user-agent: *"))
                            agent = true;

                        else if (line.StartsWith("user-agent: "))
                            agent = false;

                        else if (agent && line.StartsWith("disallow: "))
                            Disallowed.Add(line.Substring(10, line.Length - 10).ToLower());

                        else if (line.StartsWith("crawl-delay: "))
                            Int32.TryParse(line.Substring(13, line.Length - 13), out CrawlDelay);
                    }
                }

                return (Disallowed.Any() || CrawlDelay > 0) ? true : false;
            }
            catch (WebException)
            {
                return false;
            }
        }

        /// <summary>
        /// Sends HTTP request to specified location. Observes 'Crawl-delay' directive. Reads all HTML at location.
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>HTML at location as a string</returns>
        private static string GetHtml(string url)
        {
            if (ObservseCrawlDelay && CrawlDelay > 0)
            {
                Console.WriteLine($"waiting for {CrawlDelay} seconds to observse robots.txt 'Crawl-delay' directive");
                System.Threading.Thread.Sleep(CrawlDelay * 1000);
            }

            Console.WriteLine($"fetching {url}");

            var request = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var dataStream = new StreamReader(responseStream))
                {
                    return dataStream.ReadToEnd();
                }
            }
            catch (WebException)
            {
                return "";
            }
        }

        /// <summary>
        /// Given a string of HTML, extracts all href hyperlinks.
        /// </summary>
        /// <param name="html">HTML string</param>
        /// <returns>Collection of links found</returns>
        private static IEnumerable<string> GetLinks(string html)
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            foreach (var link in document.DocumentNode.Descendants("a"))
            {
                var href = link.Attributes["href"];

                if (href != null)
                    yield return SanitiseUrl(href.Value, BaseUrl);
            }
        }

        /// <summary>
        /// Given a URL and all hyperlinks found at that location, determines which links can/should be crawled,
        /// and modified class collections VisitedLinks and UnvisitedLinks. Excludes links to different domains,
        /// locations disallowed in robots.txt and any non-HTTP links.
        /// </summary>
        /// <param name="parent">URL</param>
        /// <param name="children">Hyperlinks found at URL</param>
        private static void ProcessLinks(string parent, IEnumerable<string> children)
        {
            var validChildren = new HashSet<String>();

            foreach (var c in children.Select(l => SanitiseUrl(l, BaseUrl)))
            {
                bool disallowed = false;

                if (RobotsTxt)
                {
                    disallowed = MatchesDisallowedList(c, Disallowed);

                    if (disallowed
                        && IsValidScheme(c)
                        && c.Contains($"{Subdomain}."))
                    {
                        validChildren.Add(c);
                    }
                }

                if (!disallowed
                    && IsValidScheme(c)
                    && c.Contains($"{Subdomain}."))
                {
                    validChildren.Add(c);

                    if (!MatchesRecordedList(c, UnvisitedLinks.Concat(VisitedLinks.Keys)))
                        UnvisitedLinks.Add(c);
                }
            }

            UnvisitedLinks.Remove(parent);
            VisitedLinks.Add(parent, validChildren.ToList());
        }
    }
}
