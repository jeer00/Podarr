using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Events;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V5.Podcasts;

[V5ApiController]
public class PodcastController : RestControllerWithSignalR<PodcastResource, Podcast>,
                                 IHandle<PodcastAddedEvent>,
                                 IHandle<PodcastUpdatedEvent>,
                                 IHandle<PodcastDeletedEvent>,
                                 IHandle<PodcastEpisodesUpdatedEvent>
{
    private readonly IPodcastService _podcastService;
    private readonly IAddPodcastService _addPodcastService;
    private readonly IRootFolderService _rootFolderService;

    public PodcastController(IBroadcastSignalRMessage signalRBroadcaster,
                             IPodcastService podcastService,
                             IAddPodcastService addPodcastService,
                             IRootFolderService rootFolderService,
                             RootFolderExistsValidator rootFolderExistsValidator,
                             SystemFolderValidator systemFolderValidator)
        : base(signalRBroadcaster)
    {
        _podcastService = podcastService;
        _addPodcastService = addPodcastService;
        _rootFolderService = rootFolderService;

        PostValidator.RuleFor(p => p.FeedUrl).NotEmpty();
        PostValidator.RuleFor(p => p.RootFolderPath).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .IsValidPath()
            .SetValidator(rootFolderExistsValidator)
            .SetValidator(systemFolderValidator);
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<PodcastResource>> GetAll()
    {
        var statistics = _podcastService.GetStatistics().ToDictionary(s => s.PodcastId);

        var resources = _podcastService.GetAllPodcasts()
            .Select(p => p.ToResource(statistics.GetValueOrDefault(p.Id) ?? new PodcastStatistics()))
            .ToList();

        resources.ForEach(LinkRootFolderPath);

        return TypedResults.Ok(resources);
    }

    protected override PodcastResource? GetResourceById(int id)
    {
        var resource = _podcastService.GetPodcast(id).ToResource(_podcastService.GetStatistics(id));
        LinkRootFolderPath(resource);

        return resource;
    }

    [RestPostById]
    [Consumes("application/json")]
    [Produces("application/json")]
    public Results<Created<PodcastResource>, NotFound> AddPodcast([FromBody] PodcastResource resource)
    {
        var monitor = resource.AddOptions?.Monitor ?? PodcastMonitorType.All;
        var podcast = _addPodcastService.AddPodcast(resource.ToModel(), monitor);

        return TypedCreated(podcast.Id);
    }

    [RestPutById]
    [Consumes("application/json")]
    [Produces("application/json")]
    public Results<Accepted<PodcastResource>, NotFound> UpdatePodcast([FromBody] PodcastResource resource)
    {
        var podcast = _podcastService.GetPodcast(resource.Id);

        podcast.Monitored = resource.Monitored;
        podcast.MonitorNewEpisodes = resource.MonitorNewEpisodes;
        podcast.Tags = resource.Tags ?? [];
        podcast.EpisodeSource = resource.EpisodeSource;
        podcast.TrackerSearchTerm = resource.TrackerSearchTerm.IsNullOrWhiteSpace() ? null : resource.TrackerSearchTerm.Trim();

        // Only releases published after automatic grabbing was switched on are grabbed automatically,
        // older ones can still be grabbed from an interactive search.
        if (resource.GrabTrackerReleases && !podcast.GrabTrackerReleases)
        {
            podcast.GrabTrackerReleasesSince = DateTime.UtcNow;
        }

        podcast.GrabTrackerReleases = resource.GrabTrackerReleases;

        _podcastService.UpdatePodcast(podcast);

        return TypedAccepted(resource.Id);
    }

    [RestDeleteById]
    public NoContent DeletePodcast(int id, bool deleteFiles = false)
    {
        _podcastService.DeletePodcast(id, deleteFiles);

        return TypedResults.NoContent();
    }

    private void LinkRootFolderPath(PodcastResource resource)
    {
        resource.RootFolderPath = _rootFolderService.GetBestRootFolderPath(resource.Path);
    }

    [NonAction]
    public void Handle(PodcastAddedEvent message)
    {
        BroadcastResourceChange(ModelAction.Created, message.Podcast.Id);
    }

    [NonAction]
    public void Handle(PodcastUpdatedEvent message)
    {
        BroadcastResourceChange(ModelAction.Updated, message.Podcast.Id);
    }

    [NonAction]
    public void Handle(PodcastDeletedEvent message)
    {
        foreach (var podcast in message.Podcasts)
        {
            BroadcastResourceChange(ModelAction.Deleted, podcast.Id);
        }
    }

    [NonAction]
    public void Handle(PodcastEpisodesUpdatedEvent message)
    {
        try
        {
            BroadcastResourceChange(ModelAction.Updated, message.PodcastId);
        }
        catch (ModelNotFoundException)
        {
            // Podcast was deleted while episodes were being updated
        }
    }
}
