﻿using System;
using System.Collections.Generic;

namespace rssgen
{
    class SimpleFeed
    {
        public string BaseUrl { get; set; }
        public string Title { get; set; }
        public string Description{ get; set; }
        public string Language { get; set; }

        public IList<SimpleFeedEntry> Entries;

        public SimpleFeed(string pBaseUrl, string pTitle = null, string pDescription = null, string pLanguage = "en-us")
        {
            Entries = new List<SimpleFeedEntry>();
            BaseUrl = pBaseUrl;
            Title = pTitle;
            Description = pDescription;
            Language = pLanguage;
        }


        public void AddEntry(string id, string header, string body, DateTime datePublished, DateTime dateUpdated, string tags, string image, Uri titleLinkUri = null)
        {
            var entry = new SimpleFeedEntry();

            entry.Id = id;
            entry.Title = header;
            entry.Body = body;
            entry.DatePublished = datePublished;
            entry.DateLastUpdated = dateUpdated;
            entry.Tags = tags;
            entry.ImageHtml = image;
            entry.TitleLinkUri = titleLinkUri;

            Entries.Add(entry);
        }
    }
}