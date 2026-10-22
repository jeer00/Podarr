using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Tracker;
using Sonarr.Http;

namespace Sonarr.Api.V5.Podcasts;

[V5ApiController("podcast/release")]
public class PodcastReleaseController : Controller
{
    private readonly IPodcastReleaseService _releaseService;
    private readonly IPodcastService _podcastService;
    private readonly IPodcastEpisodeService _episodeService;

    public PodcastReleaseController(IPodcastReleaseService releaseService,
                                    IPodcastService podcastService,
                                    IPodcastEpisodeService episodeService)
    {
        _releaseService = releaseService;
        _podcastService = podcastService;
        _episodeService = episodeService;
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<PodcastReleaseResource>> Search([FromQuery] int podcastId, [FromQuery] int? episodeId = null)
    {
        var podcast = _podcastService.GetPodcast(podcastId);
        var episode = episodeId.HasValue ? _episodeService.GetEpisode(episodeId.Value) : null;

        var releases = _releaseService.Search(podcast, episode, true);

        return TypedResults.Ok(releases.Select(r => r.ToResource()).ToList());
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    public Ok<PodcastDownloadResource> Grab([FromBody] PodcastGrabResource resource)
    {
        var download = _releaseService.Grab(resource.PodcastId, resource.EpisodeId, resource.IndexerId, resource.Guid ?? string.Empty);

        return TypedResults.Ok(download.ToResource());
    }
}

public class PodcastReleaseResource
{
    public string? Guid { get; set; }
    public string? Title { get; set; }
    public int IndexerId { get; set; }
    public string? Indexer { get; set; }
    public DownloadProtocol Protocol { get; set; }
    public long Size { get; set; }
    public int? Seeders { get; set; }
    public int? Leechers { get; set; }
    public DateTime PublishDate { get; set; }
    public string? InfoUrl { get; set; }
    public bool MatchesPodcast { get; set; }
    public int EpisodeScore { get; set; }
    public bool IsGrabbed { get; set; }
}

public class PodcastGrabResource
{
    public int PodcastId { get; set; }
    public int? EpisodeId { get; set; }
    public int IndexerId { get; set; }
    public string? Guid { get; set; }
}

public static class PodcastReleaseResourceMapper
{
    public static PodcastReleaseResource ToResource(this PodcastRelease model)
    {
        var release = model.Release;
        var torrent = release as TorrentInfo;

        return new PodcastReleaseResource
        {
            Guid = release.Guid,
            Title = release.Title,
            IndexerId = release.IndexerId,
            Indexer = release.Indexer,
            Protocol = release.DownloadProtocol,
            Size = release.Size,
            Seeders = torrent?.Seeders,
            Leechers = torrent?.Peers.HasValue == true && torrent.Seeders.HasValue ? torrent.Peers - torrent.Seeders : null,
            PublishDate = release.PublishDate,
            InfoUrl = release.InfoUrl,
            MatchesPodcast = model.MatchesPodcast,
            EpisodeScore = model.EpisodeScore,
            IsGrabbed = model.IsGrabbed
        };
    }
}
