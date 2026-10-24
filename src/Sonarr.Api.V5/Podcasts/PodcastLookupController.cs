using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Feed;
using Sonarr.Http;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Podcasts;

[V5ApiController("podcast/lookup")]
public class PodcastLookupController : Controller
{
    private readonly IPodcastLookupService _lookupService;
    private readonly IPodcastService _podcastService;

    public PodcastLookupController(IPodcastLookupService lookupService, IPodcastService podcastService)
    {
        _lookupService = lookupService;
        _podcastService = podcastService;
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<PodcastResource>> Search([FromQuery] string term)
    {
        List<Podcast> results;

        try
        {
            results = _lookupService.Search(term);
        }
        catch (PodcastFeedException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        var existing = _podcastService.GetAllPodcasts()
            .GroupBy(p => p.FeedUrl, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var resources = results.Select(podcast =>
        {
            if (podcast.FeedUrl != null && existing.TryGetValue(podcast.FeedUrl, out var added))
            {
                return added.ToResource();
            }

            return podcast.ToResource();
        }).ToList();

        return TypedResults.Ok(resources);
    }
}
