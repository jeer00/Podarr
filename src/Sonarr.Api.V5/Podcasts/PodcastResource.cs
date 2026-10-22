using NzbDrone.Core.Podcasts;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Podcasts;

public class PodcastResource : RestResource
{
    public string? Title { get; set; }
    public string? SortTitle { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? FeedUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Language { get; set; }
    public List<string> Categories { get; set; } = [];
    public bool Explicit { get; set; }
    public long? ItunesId { get; set; }
    public string? Path { get; set; }
    public string? RootFolderPath { get; set; }
    public bool Monitored { get; set; }
    public bool MonitorNewEpisodes { get; set; }
    public DateTime Added { get; set; }
    public DateTime? LastRefreshed { get; set; }
    public string? LastRefreshError { get; set; }
    public HashSet<int> Tags { get; set; } = [];
    public PodcastEpisodeSource EpisodeSource { get; set; } = PodcastEpisodeSource.FeedAndTracker;
    public string? TrackerSearchTerm { get; set; }
    public List<string> TrackerSearchTerms { get; set; } = [];
    public bool GrabTrackerReleases { get; set; }
    public DateTime? GrabTrackerReleasesSince { get; set; }
    public PodcastStatisticsResource? Statistics { get; set; }
    public AddPodcastOptionsResource? AddOptions { get; set; }
}

public class PodcastStatisticsResource
{
    public int EpisodeCount { get; set; }
    public int MonitoredEpisodeCount { get; set; }
    public int DownloadedEpisodeCount { get; set; }
    public long SizeOnDisk { get; set; }
    public DateTime? LastAired { get; set; }
}

public class AddPodcastOptionsResource
{
    public PodcastMonitorType Monitor { get; set; }
}

public static class PodcastResourceMapper
{
    public static PodcastResource ToResource(this Podcast model, PodcastStatistics? statistics = null)
    {
        return new PodcastResource
        {
            Id = model.Id,
            Title = model.Title,
            SortTitle = model.SortTitle,
            Description = model.Description,
            Author = model.Author,
            FeedUrl = model.FeedUrl,
            WebsiteUrl = model.WebsiteUrl,
            ImageUrl = model.ImageUrl,
            Language = model.Language,
            Categories = model.Categories ?? [],
            Explicit = model.Explicit,
            ItunesId = model.ItunesId,
            Path = model.Path,
            Monitored = model.Monitored,
            MonitorNewEpisodes = model.MonitorNewEpisodes,
            Added = model.Added,
            LastRefreshed = model.LastRefreshed,
            LastRefreshError = model.LastRefreshError,
            Tags = model.Tags ?? [],
            EpisodeSource = model.EpisodeSource,
            TrackerSearchTerm = model.TrackerSearchTerm,
            TrackerSearchTerms = model.GetTrackerSearchTerms(),
            GrabTrackerReleases = model.GrabTrackerReleases,
            GrabTrackerReleasesSince = model.GrabTrackerReleasesSince,
            Statistics = statistics?.ToResource()
        };
    }

    public static PodcastStatisticsResource ToResource(this PodcastStatistics model)
    {
        return new PodcastStatisticsResource
        {
            EpisodeCount = model.EpisodeCount,
            MonitoredEpisodeCount = model.MonitoredEpisodeCount,
            DownloadedEpisodeCount = model.DownloadedEpisodeCount,
            SizeOnDisk = model.SizeOnDisk,
            LastAired = model.LastAired
        };
    }

    public static Podcast ToModel(this PodcastResource resource)
    {
        return new Podcast
        {
            Title = resource.Title,
            FeedUrl = resource.FeedUrl,
            ImageUrl = resource.ImageUrl,
            ItunesId = resource.ItunesId,
            Path = resource.Path,
            RootFolderPath = resource.RootFolderPath,
            Monitored = resource.Monitored,
            MonitorNewEpisodes = resource.MonitorNewEpisodes,
            EpisodeSource = resource.EpisodeSource,
            TrackerSearchTerm = resource.TrackerSearchTerm,
            Tags = resource.Tags ?? []
        };
    }
}
