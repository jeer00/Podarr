using NzbDrone.Core.Podcasts;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Podcasts;

public class PodcastEpisodeResource : RestResource
{
    public int PodcastId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? AirDateUtc { get; set; }
    public int? SeasonNumber { get; set; }
    public int? EpisodeNumber { get; set; }
    public string? EpisodeType { get; set; }
    public int? Duration { get; set; }
    public string? EnclosureUrl { get; set; }
    public string? EnclosureType { get; set; }
    public long? EnclosureLength { get; set; }
    public string? ImageUrl { get; set; }
    public bool Explicit { get; set; }
    public bool Monitored { get; set; }
    public bool HasFile { get; set; }
    public string? RelativePath { get; set; }
    public PodcastFileSource FileSource { get; set; }
    public long Size { get; set; }
    public DateTime? DateDownloaded { get; set; }
    public string? LastDownloadError { get; set; }
}

public class PodcastEpisodesMonitoredResource
{
    public List<int> EpisodeIds { get; set; } = [];
    public bool Monitored { get; set; }
}

public static class PodcastEpisodeResourceMapper
{
    public static PodcastEpisodeResource ToResource(this PodcastEpisode model)
    {
        return new PodcastEpisodeResource
        {
            Id = model.Id,
            PodcastId = model.PodcastId,
            Title = model.Title,
            Description = model.Description,
            AirDateUtc = model.AirDateUtc,
            SeasonNumber = model.SeasonNumber,
            EpisodeNumber = model.EpisodeNumber,
            EpisodeType = model.EpisodeType,
            Duration = model.Duration,
            EnclosureUrl = model.EnclosureUrl,
            EnclosureType = model.EnclosureType,
            EnclosureLength = model.EnclosureLength,
            ImageUrl = model.ImageUrl,
            Explicit = model.Explicit,
            Monitored = model.Monitored,
            HasFile = model.HasFile,
            RelativePath = model.RelativePath,
            FileSource = model.FileSource,
            Size = model.Size,
            DateDownloaded = model.DateDownloaded,
            LastDownloadError = model.LastDownloadError
        };
    }

    public static List<PodcastEpisodeResource> ToResource(this IEnumerable<PodcastEpisode> models)
    {
        return models.Select(ToResource).ToList();
    }
}
