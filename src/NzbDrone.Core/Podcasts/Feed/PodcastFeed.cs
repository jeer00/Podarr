using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Podcasts.Feed
{
    public class PodcastFeed
    {
        public PodcastFeed()
        {
            Categories = new List<string>();
            Items = new List<PodcastFeedItem>();
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string WebsiteUrl { get; set; }
        public string ImageUrl { get; set; }
        public string Language { get; set; }
        public List<string> Categories { get; set; }
        public bool Explicit { get; set; }
        public string NewFeedUrl { get; set; }
        public List<PodcastFeedItem> Items { get; set; }
    }

    public class PodcastFeedItem
    {
        public string Guid { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? AirDateUtc { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string EpisodeType { get; set; }
        public int? Duration { get; set; }
        public string EnclosureUrl { get; set; }
        public string EnclosureType { get; set; }
        public long? EnclosureLength { get; set; }
        public string ImageUrl { get; set; }
        public bool Explicit { get; set; }
    }
}
