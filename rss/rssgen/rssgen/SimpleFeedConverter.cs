using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace rssgen
{
    class SimpleFeedConverter
    {

        private string _feedFormat;

        public string FeedFormat
        {
            get { return _feedFormat; }
        }

        public SimpleFeedConverter(string feedFormat)
        {
            _feedFormat = ParseFeedFormat(feedFormat);
        }

        /// <summary>
        /// Parse a string to get rss/atom
        /// </summary>
        /// <param name="feedFormat"></param>
        /// <returns></returns>
        private string ParseFeedFormat(string feedFormat)
        {
            //set feed format to rss or atom
            if (string.IsNullOrWhiteSpace(feedFormat))
                feedFormat = "rss";
            else
                feedFormat = feedFormat.Replace("\"", "").Replace("'", "");

            if (string.IsNullOrWhiteSpace(feedFormat) || !feedFormat.ToLower().StartsWith("a"))
                feedFormat = "rss";
            else
                feedFormat = "atom";

            return feedFormat;
        }

        /// <summary>
        /// Convert the feed to rss/atom
        /// </summary>
        /// <returns></returns>
        public string ToFeedText(SimpleFeed pFeed)
        {
            if (FeedFormat == "rss")
                return ToRssText(pFeed);
            return ToAtomText(pFeed);
        }

        private string ToRssText(SimpleFeed pFeed)
        {
            var feed = GetSyndicationFeed(pFeed);
            var formatter = new Rss20FeedFormatter(feed);
            return ToFeedText(formatter);
        }

        private string ToAtomText(SimpleFeed pFeed)
        {
            var feed = GetSyndicationFeed(pFeed);
            var formatter = new Atom10FeedFormatter(feed);
            return ToFeedText(formatter);
        }

        private string ToFeedText(SyndicationFeedFormatter pFormatter)
        {

            var settings = new XmlWriterSettings
            {
                CheckCharacters = true,
                CloseOutput = true,
                ConformanceLevel = ConformanceLevel.Document,
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "    ",
                NamespaceHandling = NamespaceHandling.OmitDuplicates,
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
                NewLineOnAttributes = true,
                OmitXmlDeclaration = false
            };

            //var sb = new StringBuilder();
            //using (var writer = XmlWriter.Create(sb, settings))
            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, settings))
            {
                pFormatter.WriteTo(writer);
                writer.Flush();
                writer.Close();
                //return sb.ToString();
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Turn a SimpleFeed object into a SyndicationFeed object
        /// </summary>
        private SyndicationFeed GetSyndicationFeed(SimpleFeed pFeed)
        {
            var feed = new SyndicationFeed();
            var items = new List<SyndicationItem>();

            feed.BaseUri = new Uri(pFeed.BaseUrl);
            if (!string.IsNullOrWhiteSpace(pFeed.Title))
                feed.Title = new TextSyndicationContent(pFeed.Title);
            if (!string.IsNullOrWhiteSpace(pFeed.Description))
                feed.Description = new TextSyndicationContent(pFeed.Description);
            if (!string.IsNullOrWhiteSpace(pFeed.Language))
                feed.Language = pFeed.Language;

            if (pFeed.Entries != null)
            {
                foreach (var entry in pFeed.Entries)
                {
                    items.Add(new SyndicationItem
                        {
                            //Id, DatePublished, DateLastUpdated are required so that each new post won't make readers think that every entry is new
                            Title = new TextSyndicationContent(entry.Title)
                            , Id = entry.Id 
                            , PublishDate = entry.DatePublished
                            , LastUpdatedTime = entry.DateLastUpdated
                            , Content = new TextSyndicationContent(entry.Body) // + entry.ImageHtml
                        });
                }
            }

            feed.Items = items;
            return feed;
        }



    }
}
