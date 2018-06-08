using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KTCrawler
{
    public static class UrlChecks
    {
        public static bool IsRelativePath(string url)
        {
            return url.StartsWith("/");
        }

        /// <summary>
        /// Attempts to extract the scheme and domain components of a URL
        /// and remove any extra path information.
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>Substring of URL from scheme to top-level domain</returns>
        public static string GetDomain(string url)
        {
            var schemePattern = new Regex(@"^https?://(www\.)?");
            var pathPattern = new Regex(@"/.*");

            var scheme = schemePattern.Match(url);
            var schemeRemoved = schemePattern.Replace(url, "");
            var pathRemoved = pathPattern.Replace(schemeRemoved, "");

            return $"{scheme}{pathRemoved}";
        }

        /// <summary>
        /// Attempts to split a URL into domain levels and return the highest domain level
        /// controlled by the site owner. Will only work for .com and .uk sites.
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>Second level domain for .com sites, third level domain for .uk sites</returns>
        public static string GetSubdomain(string url)
        {
            var dotComPattern = new Regex(@"\.com$");
            var dotUKPattern = new Regex(@"\.uk$");

            var domain = new Regex(@"[a-zA-Z-]*$");

            if (dotComPattern.IsMatch(url))
                return domain.Match(dotComPattern.Replace(url, "")).Value;

            else if (dotUKPattern.IsMatch(url))
                return domain.Match(Regex.Replace(url, @"\.[a-zA-Z]*\.uk$", "")).Value;

            else
                return url;
        }

        /// <summary>
        /// Evaluates whether a URI has a http(s) scheme.
        /// </summary>
        /// <param name="url">URI</param>
        /// <returns>True if http(s), false for all other schemes (e.g. ftp)</returns>
        public static bool IsValidScheme(string url)
        {
            var httpPattern = new Regex(@"^https?://");

            if (httpPattern.IsMatch(url))
                return true;

            else
                return false;
        }

        /// <summary>
        /// Distinguishes between http and https. False doesn't guarantee http.
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>True is https, false if http</returns>
        public static bool IsHttps(string url)
        {
            Regex httpsPattern = new Regex(@"^https://");

            if (httpsPattern.IsMatch(url))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Removes escape characters from a URL that may have been pulled from HTML.
        /// Will attach scheme and domain to a string not containing them.
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="baseUrl">Scheme plus domain for site being crawled</param>
        /// <returns>Full URL which can be requested with any HTML escape characters removed</returns>
        public static string SanitiseUrl(string url, string baseUrl)
        {
            url = Regex.Replace(url, @"\\\w", "");

            if (IsRelativePath(url))
                return $"{baseUrl}{url}";

            else
                return url;
        }

        /// <summary>
        /// Conservatively determines whether a URL matches anything disallowed in robots.txt.
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="disallowedList">Collection of disallowed request locations</param>
        /// <returns>True if URL should not be crawled</returns>
        public static bool MatchesDisallowedList(string url, IEnumerable<string> disallowedList)
        {
            foreach (var d in disallowedList)
            {
                var pattern = $@"({Regex.Escape(d)}[^a-zA-Z0-9])|({Regex.Escape(d.Replace("*", ""))})";

                if (Regex.Matches(url.ToLower(), pattern).Count != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Takes the concatenation of visited and unvisited sites and determines whether a URL
        /// is already in one of these lists.
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="visitedAndUnvisited">Visited sites plus unvisited sites</param>
        /// <returns>True if URL already visited or listed to be visited</returns>
        public static bool MatchesRecordedList(string url, IEnumerable<string> visitedAndUnvisited)
        {
            string trailingSlashRemoved = url.Trim('/');
            string wwwRemoved = Regex.Replace(trailingSlashRemoved, @"www\.", "");

            string[] equalityTests = { trailingSlashRemoved, wwwRemoved };
            return visitedAndUnvisited.Any(x => equalityTests.Contains(x.Trim('/')));
        }
    }
}
