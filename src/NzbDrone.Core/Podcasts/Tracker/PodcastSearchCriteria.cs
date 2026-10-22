using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Podcasts.Tracker
{
    // Indexers only know how to search for TV, the free text "special episode" search
    // is reused since it boils down to a plain query in the configured categories.
    public class PodcastSearchCriteria : SpecialEpisodeSearchCriteria
    {
        public string PodcastTitle { get; set; }

        public override string ToString()
        {
            return $"[Podcast {PodcastTitle}: {string.Join(", ", EpisodeQueryTitles)}]";
        }
    }
}
