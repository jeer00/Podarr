using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Podcasts
{
    public class Podcast
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string RssUrl { get; set; }
        public string WebsiteUrl { get; set; }
        public string Language { get; set; }
        public string Copyright { get; set; }
        public string Author { get; set; }
        public string OwnerName { get; set; }
        public string OwnerEmail { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public string ItunesId { get; set; }
        public string ItunesExplicit { get; set; }
        public string ItunesType { get; set; }
        public string ItunesAuthor { get; set; }
        public string ItunesSubtitle { get; set; }
        public string ItunesSummary { get; set; }
        public string ItunesOwnerName { get; set; }
        public string ItunesOwnerEmail { get; set; }
        public DateTime? LastUpdated { get; set; }
        public DateTime? Added { get; set; }
        public bool Monitored { get; set; }
        public string Path { get; set; }
        public List<PodcastEpisode> Episodes { get; set; }
    }
}
