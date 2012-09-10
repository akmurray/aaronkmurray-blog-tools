using System;
using System.Collections.Generic;
using System.IO;
using HtmlAgilityPack;
using Mono.Options;

namespace rssgen
{
    class Program
    {
        static void Main(string[] args)
        {
            var _startTime = DateTime.Now;
            string fileSource = null;
            string fileOutput = "feed-rss.xml";
            string baseUrl = "http://aaronkmurray.com";
            string feedTitle = "aaronkmurray.com | Aaron Murray's Blog Feed";
            bool showHelp = false;
            bool showDebug = true;
            bool pauseWhenFinished = false;
            string feedFormat = "rss"; //rss/atom
            int maxItems = 64;
            string xpathPost = "//div[@class='blog-post']";
            string xpathPostHeader = "div[@class='blog-post-header']/span";
            string xpathPostBody = "div[@class='blog-post-body']";
            string xpathPostDate = "div[@class='blog-post-footer']/span[@class='post-timestamp']";
            string xpathPostTags = "div[@class='blog-post-footer']/span[@class='post-tags']";
            string xpathPostImage = "div[@class='blog-post-footer']/span[@class='post-screenshot']/a/img";

            var p = new OptionSet () {
                { "s|fileSource=", "*REQUIRED* > ex: index.html", x => fileSource = x },
                { "o|fileOutput=", "*REQUIRED* > ex: feed-rss.xml", x => fileOutput = x },
                { "b|baseUrl=", "[optional, default="+baseUrl+"]",  x => baseUrl = x },
                { "t|feedTitle=", "[optional, default="+feedTitle+"]",  x => feedTitle = x },
                { "f|feedFormat=", "[optional, default="+feedFormat+"] output feed type: 'rss' or 'atom'",  x => feedFormat = x },
                { "m|maxItems=", "[optional, default="+maxItems+"] Max # of posts to render to the output feed",   x => maxItems = int.Parse("0" + x) },
                
                // add selector options to get to post details 
                { "xp|xpathPost=", "[optional, default="+xpathPost + "]", x => xpathPost = x },
                { "xh|xpathPostHeader=", "[optional, default="+xpathPostHeader + "]", x => xpathPostHeader = x },
                { "xb|xpathPostBody=", "[optional, default="+xpathPostBody + "]", x => xpathPostBody = x },
                { "xd|xpathPostDate=", "[optional, default="+xpathPostDate + "]", x => xpathPostDate = x },
                { "xt|xpathPostTags=", "[optional, default="+xpathPostTags + "]", x => xpathPostTags = x },
                { "xi|xpathPostImage=", "[optional, default="+xpathPostImage + "]", x => xpathPostImage = x },

                //standard options for command line utils
                { "d|debug", "[optional, show debug details (verbose), default="+showDebug + "]",   x => showDebug = x != null},
                { "pause|pauseWhenFinished", "[optional, pause output window with a ReadLine when finished, default="+pauseWhenFinished + "]",   x => pauseWhenFinished = (x != null)},
                { "h|?|help", "show the help options",   x => showHelp = x != null },
            };
            List<string> extraArgs = p.Parse (args);

            

            //validate the fileSource HTML/file from command-options
            var fi = new FileInfo(fileSource);
            if (!fi.Exists)
            {
                Console.WriteLine("Invalid fileSource: " + fileSource);
                showHelp = true;
            }

            if (showHelp || args.Length == 0 || string.IsNullOrWhiteSpace(fileSource))
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }



            //no negative values for max # of feed items to fetch
            maxItems = Math.Max(0, maxItems);

            //parse the passed in format to something like rss or atom
            var simpleFeedConverter = new SimpleFeedConverter(feedFormat);
            feedFormat = simpleFeedConverter.FeedFormat;


            Console.WriteLine();
            Console.WriteLine("rssgen by @AaronKMurray using options:");
            Console.WriteLine("\tfileSource:\t" + fileSource);
            Console.WriteLine("\tfileOutput:\t" + fileOutput);
            Console.WriteLine("\tfeedFormat:\t" + feedFormat);
            Console.WriteLine("\tfeedTitle:\t" + feedTitle);
            Console.WriteLine("\tmaxItems:\t" + maxItems);
            Console.WriteLine("\txpathPost:\t" + xpathPost);
            Console.WriteLine("\tbaseUrl:\t" + baseUrl);
            Console.WriteLine();


            //get source HTML/file from command-options
            var doc = new HtmlDocument();
            try
            {
                doc.Load(fileSource);
            } 
            catch (Exception ex)
            {
                Console.WriteLine("Error loading fileSource: " + fileSource);
                Console.WriteLine(ex.Message);
                return;
            }

            var feed = new SimpleFeed(baseUrl, feedTitle);


            try
            {

                //get top-level feed details from HTML (via selectors set in command options)
                var postNodes = doc.DocumentNode.SelectNodes(xpathPost);
                Console.WriteLine("found " + postNodes.Count + " posts");
                foreach (HtmlNode el in postNodes) //"//a[@href]"
                {
                    //parse feed items from HTML

                    //HtmlAttribute att = el.Attributes["href"];
                    var header = el.SelectSingleNode(xpathPostHeader);
                    var body =  el.SelectSingleNode(xpathPostBody);
                    var date =  el.SelectSingleNode(xpathPostDate);
                    var tags =  el.SelectSingleNode(xpathPostTags);
                    var image =  el.SelectSingleNode(xpathPostImage);

                    feed.AddEntry(header == null ? null : header.InnerText
                        , body == null ? null : body.InnerHtml
                        , date == null ? null : date.InnerText
                        , tags == null ? null : tags.InnerText
                        , image == null ? null : image.OuterHtml
                    );

                    if (showDebug)
                    {
                        if (header == null)
                            Console.WriteLine("header: NOT FOUND");
                        else
                            Console.WriteLine("header: " + header.InnerText);

                        //Console.WriteLine("body: " + body.InnerHtml);

                        if (date == null)
                            Console.WriteLine("date: NOT FOUND");
                        else
                            Console.WriteLine("date: " + date.InnerText);

                        if (tags == null)
                            Console.WriteLine("tags: NOT FOUND");
                        else
                            Console.WriteLine("tags: " + tags.InnerText);

                        if (image == null)
                            Console.WriteLine("image: NOT FOUND");
                        else
                            Console.WriteLine("image: " + image.OuterHtml);
                    }
                }
            } 
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing fileSource: " + fileSource);
                Console.WriteLine(ex.Message);
                return;
            }

            

            //convert the feed data into a string in the appropriate feed file & format (Atom/RSS)
            var feedText = simpleFeedConverter.ToFeedText(feed);

            //write feed string (xml) to filesystem 
            WriteTextFile(fileOutput, feedText);

            if (showDebug)
            {
                Console.WriteLine("Complete at " + DateTime.Now.ToLongTimeString() + ". Took " + DateTime.Now.Subtract(_startTime).TotalSeconds + " seconds to run");
            }

            if (pauseWhenFinished)
            {
                Console.WriteLine("Press any key to complete");
                Console.ReadLine(); //just here to pause the output window during testing
            }
        }

        /// <summary>
        /// Writes a string to a text file
        /// </summary>
        /// <param name="path">file to be written</param>
        /// <param name="text">text to be written</param>
        public static void WriteTextFile(string path, string text)
        {
            FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            var sw = new StreamWriter(fs);
            sw.Write(text);
            sw.Close();
        }
    }
}
