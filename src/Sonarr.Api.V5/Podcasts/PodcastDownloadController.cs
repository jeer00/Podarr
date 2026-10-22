using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts.Tracker;
using NzbDrone.Core.Podcasts.Tracker.Events;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V5.Podcasts;

[V5ApiController]
public class PodcastDownloadController : RestControllerWithSignalR<PodcastDownloadResource, PodcastDownload>,
                                         IHandle<PodcastDownloadsUpdatedEvent>
{
    private readonly IPodcastDownloadService _downloadService;

    public PodcastDownloadController(IBroadcastSignalRMessage signalRBroadcaster, IPodcastDownloadService downloadService)
        : base(signalRBroadcaster)
    {
        _downloadService = downloadService;
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<PodcastDownloadResource>> GetDownloads([FromQuery] int podcastId)
    {
        return TypedResults.Ok(_downloadService.GetDownloads(podcastId).Select(d => d.ToResource()).ToList());
    }

    [RestDeleteById]
    public NoContent Remove(int id, [FromQuery] bool removeFromClient = false)
    {
        _downloadService.Remove(id, removeFromClient);

        return TypedResults.NoContent();
    }

    [HttpPost("{id:int}/reimport")]
    [Produces("application/json")]
    public Ok<PodcastDownloadResource> Reimport([FromRoute] int id)
    {
        return TypedResults.Ok(_downloadService.Reimport(id).ToResource());
    }

    [NonAction]
    public void Handle(PodcastDownloadsUpdatedEvent message)
    {
        BroadcastResourceChange(ModelAction.Sync);
    }
}

public class PodcastDownloadResource : RestResource
{
    public int PodcastId { get; set; }
    public int? PodcastEpisodeId { get; set; }
    public string? Title { get; set; }
    public string? Indexer { get; set; }
    public DownloadProtocol Protocol { get; set; }
    public long Size { get; set; }
    public PodcastDownloadStatus Status { get; set; }
    public double Progress { get; set; }
    public string? Message { get; set; }
    public DateTime Added { get; set; }
    public DateTime? Completed { get; set; }
}

public static class PodcastDownloadResourceMapper
{
    public static PodcastDownloadResource ToResource(this PodcastDownload model)
    {
        return new PodcastDownloadResource
        {
            Id = model.Id,
            PodcastId = model.PodcastId,
            PodcastEpisodeId = model.PodcastEpisodeId,
            Title = model.Title,
            Indexer = model.Indexer,
            Protocol = model.Protocol,
            Size = model.Size,
            Status = model.Status,
            Progress = model.Progress,
            Message = model.Message,
            Added = model.Added,
            Completed = model.Completed
        };
    }
}
