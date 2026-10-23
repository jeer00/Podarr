using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Feed;

namespace Sonarr.Api.V5.Podcasts;

// Serves RSS feeds of downloaded episodes to podcast apps. Podcast apps can't send the API key,
// so these are anonymous and protected by a secret token in the URL instead.
[AllowAnonymous]
public class PodcastFeedController : Controller
{
    private readonly IConfigService _configService;
    private readonly IPodcastService _podcastService;
    private readonly IPodcastEpisodeService _episodeService;
    private readonly IDiskProvider _diskProvider;
    private readonly IPodcastFeedUrlProvider _urlProvider;

    public PodcastFeedController(IConfigService configService,
                                 IPodcastService podcastService,
                                 IPodcastEpisodeService episodeService,
                                 IDiskProvider diskProvider,
                                 IPodcastFeedUrlProvider urlProvider)
    {
        _configService = configService;
        _podcastService = podcastService;
        _episodeService = episodeService;
        _diskProvider = diskProvider;
        _urlProvider = urlProvider;
    }

    [HttpGet("/feed/{token}/podcasts.opml")]
    public IActionResult GetOpml([FromRoute] string token)
    {
        if (!IsAuthorized(token))
        {
            return NotFound();
        }

        return Xml(PodcastFeedBuilder.BuildOpml(_podcastService.GetAllPodcasts(), GetFeedBaseUrl()), "text/x-opml");
    }

    [HttpGet("/feed/{token}/podcast/{podcastId:int}.rss")]
    [HttpGet("/feed/{token}/podcast/{podcastId:int}.xml")]
    public IActionResult GetFeed([FromRoute] string token, [FromRoute] int podcastId)
    {
        if (!IsAuthorized(token))
        {
            return NotFound();
        }

        Podcast podcast;

        try
        {
            podcast = _podcastService.GetPodcast(podcastId);
        }
        catch (ModelNotFoundException)
        {
            return NotFound();
        }

        var episodes = _episodeService.GetEpisodesByPodcast(podcastId);

        return Xml(PodcastFeedBuilder.BuildRss(podcast, episodes, GetFeedBaseUrl()), "application/rss+xml");
    }

    [HttpGet("/feed/{token}/episode/{episodeId:int}/{fileName}")]
    [HttpHead("/feed/{token}/episode/{episodeId:int}/{fileName}")]
    public IActionResult GetEpisodeFile([FromRoute] string token, [FromRoute] int episodeId)
    {
        if (!IsAuthorized(token))
        {
            return NotFound();
        }

        try
        {
            var episode = _episodeService.GetEpisode(episodeId);

            if (!episode.HasFile)
            {
                return NotFound();
            }

            var path = PodcastService.GetEpisodePath(_podcastService.GetPodcast(episode.PodcastId), episode);

            if (!_diskProvider.FileExists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, PodcastFeedBuilder.GetMimeType(path), enableRangeProcessing: true);
        }
        catch (ModelNotFoundException)
        {
            return NotFound();
        }
    }

    private bool IsAuthorized(string token)
    {
        if (!_configService.PodcastFeedsEnabled || token.IsNullOrWhiteSpace())
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(_configService.PodcastFeedToken));
    }

    private string GetFeedBaseUrl()
    {
        return _urlProvider.GetFeedBaseUrl($"{Request.Scheme}://{Request.Host}{Request.PathBase}");
    }

    private ContentResult Xml(XDocument document, string contentType)
    {
        return Content(PodcastFeedBuilder.ToXml(document), $"{contentType}; charset=utf-8");
    }
}
