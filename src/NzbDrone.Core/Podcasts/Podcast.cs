using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Podcasts
{
    public class Podcast : ModelBase
    {
        public Podcast()
        {
            Categories = new List<string>();
            Tags = new HashSet<int>();
            EpisodeSource = PodcastEpisodeSource.FeedAndTracker;
        }

        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string FeedUrl { get; set; }
        public string WebsiteUrl { get; set; }
        public string ImageUrl { get; set; }
        public string Language { get; set; }
        public List<string> Categories { get; set; }
        public bool Explicit { get; set; }
        public long? ItunesId { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public bool Monitored { get; set; }
        public bool MonitorNewEpisodes { get; set; }
        public DateTime Added { get; set; }
        public DateTime? LastRefreshed { get; set; }
        public string LastRefreshError { get; set; }
        public HashSet<int> Tags { get; set; }
        public PodcastEpisodeSource EpisodeSource { get; set; }
        public string TrackerSearchTerm { get; set; }
        public bool GrabTrackerReleases { get; set; }
        public DateTime? GrabTrackerReleasesSince { get; set; }

        // The name used to find and recognise this podcast's releases on trackers.
        public string GetTrackerSearchTerm()
        {
            return TrackerSearchTerm.IsNotNullOrWhiteSpace() ? TrackerSearchTerm.Trim() : Tracker.PodcastReleaseMatcher.CleanPodcastTitle(Title);
        }

        // Everything searched for: a user provided term, or the cleaned up title plus the title as it is in the feed.
        public List<string> GetTrackerSearchTerms()
        {
            if (TrackerSearchTerm.IsNotNullOrWhiteSpace())
            {
                return new List<string> { TrackerSearchTerm.Trim() };
            }

            return new[] { GetTrackerSearchTerm(), Title }
                .Where(t => t.IsNotNullOrWhiteSpace())
                .DistinctBy(Tracker.PodcastReleaseMatcher.Normalize)
                .ToList();
        }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", FeedUrl, Title.NullSafe());
        }
    }
}
