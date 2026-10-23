using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Events;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V5.Podcasts;

[V5ApiController]
public class PodcastEpisodeController : RestControllerWithSignalR<PodcastEpisodeResource, PodcastEpisode>,
                                        IHandle<PodcastEpisodesUpdatedEvent>
{
    private readonly IPodcastEpisodeService _episodeService;
    private readonly IPodcastService _podcastService;
    private readonly IDiskProvider _diskProvider;

    public PodcastEpisodeController(IBroadcastSignalRMessage signalRBroadcaster,
                                    IPodcastEpisodeService episodeService,
                                    IPodcastService podcastService,
                                    IDiskProvider diskProvider)
        : base(signalRBroadcaster)
    {
        _episodeService = episodeService;
        _podcastService = podcastService;
        _diskProvider = diskProvider;
    }

    [HttpGet]
    [Produces("application/json")]
    public Results<Ok<List<PodcastEpisodeResource>>, BadRequest> GetEpisodes([FromQuery] int? podcastId)
    {
        if (!podcastId.HasValue)
        {
            throw new BadRequestException("podcastId is required");
        }

        var episodes = _episodeService.GetEpisodesByPodcast(podcastId.Value)
            .OrderByDescending(e => e.AirDateUtc ?? DateTime.MinValue)
            .ThenByDescending(e => e.Id);

        return TypedResults.Ok(episodes.ToResource());
    }

    protected override PodcastEpisodeResource? GetResourceById(int id)
    {
        return _episodeService.GetEpisode(id).ToResource();
    }

    [RestPutById]
    [Consumes("application/json")]
    [Produces("application/json")]
    public Results<Accepted<PodcastEpisodeResource>, NotFound> UpdateEpisode([FromBody] PodcastEpisodeResource resource)
    {
        _episodeService.SetMonitored([resource.Id], resource.Monitored);

        return TypedAccepted(resource.Id);
    }

    [HttpPut("monitor")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public Accepted<List<PodcastEpisodeResource>> SetMonitored([FromBody] PodcastEpisodesMonitoredResource resource)
    {
        var episodes = _episodeService.SetMonitored(resource.EpisodeIds, resource.Monitored);

        return TypedResults.Accepted(string.Empty, episodes.ToResource());
    }

    [HttpDelete("{id:int}/file")]
    [Produces("application/json")]
    public Ok<PodcastEpisodeResource> DeleteFile([FromRoute] int id)
    {
        return TypedResults.Ok(_episodeService.DeleteEpisodeFile(id).ToResource());
    }

    [HttpGet("{id:int}/audio")]
    public IActionResult GetAudio([FromRoute] int id)
    {
        PodcastEpisode episode;

        try
        {
            episode = _episodeService.GetEpisode(id);
        }
        catch (ModelNotFoundException)
        {
            return NotFound();
        }

        if (!episode.HasFile)
        {
            return NotFound();
        }

        var podcast = _podcastService.GetPodcast(episode.PodcastId);
        var path = PodcastService.GetEpisodePath(podcast, episode);

        if (!_diskProvider.FileExists(path))
        {
            return NotFound();
        }

        var contentType = episode.EnclosureType ?? "application/octet-stream";

        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    [NonAction]
    public void Handle(PodcastEpisodesUpdatedEvent message)
    {
        foreach (var episode in message.Episodes)
        {
            BroadcastResourceChange(ModelAction.Updated, episode.ToResource());
        }
    }
}
