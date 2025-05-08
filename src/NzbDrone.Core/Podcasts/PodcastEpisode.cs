using System;

namespace NzbDrone.Core.Podcasts
{
    public class PodcastEpisode
    {
        public int Id { get; set; }
        public int PodcastId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string AudioUrl { get; set; }
        public string AudioType { get; set; }
        public string AudioLength { get; set; }
        public string AudioSize { get; set; }
        public string ItunesDuration { get; set; }
        public string ItunesExplicit { get; set; }
        public string ItunesSubtitle { get; set; }
        public string ItunesSummary { get; set; }
        public string ItunesImage { get; set; }
        public DateTime? Published { get; set; }
        public bool Monitored { get; set; }
        public bool Downloaded { get; set; }
        public string FilePath { get; set; }
    }
}
