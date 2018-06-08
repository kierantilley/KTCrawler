using System;

namespace KTCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            var startUrl = "http://hirespace.com";

            if (args.Length != 0)
                Crawler.Crawl(args[0], true, Tuple.Create(true, 100));
            
            else
                Crawler.Crawl(startUrl, true, Tuple.Create(true, 100));
        }
    }
}
