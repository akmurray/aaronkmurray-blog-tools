using System;

namespace rssgen
{
    class SimpleFeedEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTime DatePublished { get; set; }
        public DateTime DateLastUpdated { get; set; }
        public string Tags { get; set; }
        public string ImageHtml { get; set; }
    }
}