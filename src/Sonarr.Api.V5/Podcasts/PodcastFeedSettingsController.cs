using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Podcasts.Feed.S3;
using Sonarr.Http;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Podcasts;

[V5ApiController("podcastfeed/settings")]
public class PodcastFeedSettingsController : Controller
{
    private readonly IConfigService _configService;
    private readonly IPodcastFeedUrlProvider _urlProvider;
    private readonly IPodcastFeedPublisher _publisher;
    private readonly IManageCommandQueue _commandQueueManager;

    public PodcastFeedSettingsController(IConfigService configService,
                                         IPodcastFeedUrlProvider urlProvider,
                                         IPodcastFeedPublisher publisher,
                                         IManageCommandQueue commandQueueManager)
    {
        _configService = configService;
        _urlProvider = urlProvider;
        _publisher = publisher;
        _commandQueueManager = commandQueueManager;
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<PodcastFeedSettingsResource> GetSettings()
    {
        return TypedResults.Ok(GetResource());
    }

    [HttpPut]
    [Consumes("application/json")]
    [Produces("application/json")]
    public Ok<PodcastFeedSettingsResource> SaveSettings([FromBody] PodcastFeedSettingsResource resource)
    {
        var baseUrl = resource.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty;

        ValidateUrl(baseUrl, "External URL");
        ValidateUrl(resource.S3Endpoint?.Trim(), "S3 endpoint");
        ValidateUrl(resource.S3PublicUrl?.Trim(), "Public bucket URL");

        _configService.PodcastFeedsEnabled = resource.Enabled;
        _configService.PodcastFeedBaseUrl = baseUrl;
        _configService.PodcastFeedS3Enabled = resource.S3Enabled;
        _configService.PodcastFeedS3Endpoint = resource.S3Endpoint?.Trim();
        _configService.PodcastFeedS3Region = resource.S3Region?.Trim();
        _configService.PodcastFeedS3Bucket = resource.S3Bucket?.Trim();
        _configService.PodcastFeedS3AccessKey = resource.S3AccessKey?.Trim();
        _configService.PodcastFeedS3Prefix = resource.S3Prefix?.Trim();
        _configService.PodcastFeedS3PublicUrl = resource.S3PublicUrl?.Trim().TrimEnd('/');

        // The secret isn't sent to the browser, an empty value keeps the saved one.
        if (resource.S3SecretKey.IsNotNullOrWhiteSpace())
        {
            _configService.PodcastFeedS3SecretKey = resource.S3SecretKey.Trim();
        }

        if (_urlProvider.IsS3PublishingEnabled())
        {
            _commandQueueManager.Push(new PublishPodcastFeedsCommand { Force = true });
        }

        return TypedResults.Ok(GetResource());
    }

    [HttpPost("tests3")]
    [Consumes("application/json")]
    public NoContent TestS3([FromBody] PodcastFeedSettingsResource resource)
    {
        var settings = new S3Settings
        {
            Endpoint = resource.S3Endpoint,
            Region = resource.S3Region,
            Bucket = resource.S3Bucket,
            AccessKey = resource.S3AccessKey,
            SecretKey = resource.S3SecretKey.IsNotNullOrWhiteSpace() ? resource.S3SecretKey : _configService.PodcastFeedS3SecretKey
        };

        if (!settings.IsComplete())
        {
            throw new BadRequestException("Endpoint, bucket, access key and secret key are required");
        }

        try
        {
            _publisher.Test(settings);
        }
        catch (HttpException ex)
        {
            throw new BadRequestException($"Upload failed: {ex.Response?.StatusCode} {ex.Response?.Content}");
        }
        catch (Exception ex)
        {
            throw new BadRequestException($"Unable to connect: {ex.Message}");
        }

        return TypedResults.NoContent();
    }

    [HttpPost("publish")]
    public NoContent Publish()
    {
        _commandQueueManager.Push(new PublishPodcastFeedsCommand { Force = true });

        return TypedResults.NoContent();
    }

    [HttpPost("resettoken")]
    [Produces("application/json")]
    public Ok<PodcastFeedSettingsResource> ResetToken()
    {
        _configService.PodcastFeedToken = Guid.NewGuid().ToString("N");

        // Uploaded feeds are stored under the token, upload them again under the new one.
        if (_urlProvider.IsS3PublishingEnabled())
        {
            _commandQueueManager.Push(new PublishPodcastFeedsCommand { Force = true });
        }

        return TypedResults.Ok(GetResource());
    }

    private PodcastFeedSettingsResource GetResource()
    {
        return new PodcastFeedSettingsResource
        {
            Enabled = _configService.PodcastFeedsEnabled,
            BaseUrl = _configService.PodcastFeedBaseUrl,
            Token = _configService.PodcastFeedToken,
            FeedBaseUrl = _urlProvider.GetFeedBaseUrl(),
            S3Enabled = _configService.PodcastFeedS3Enabled,
            S3Endpoint = _configService.PodcastFeedS3Endpoint,
            S3Region = _configService.PodcastFeedS3Region,
            S3Bucket = _configService.PodcastFeedS3Bucket,
            S3AccessKey = _configService.PodcastFeedS3AccessKey,
            HasS3SecretKey = _configService.PodcastFeedS3SecretKey.IsNotNullOrWhiteSpace(),
            S3Prefix = _configService.PodcastFeedS3Prefix,
            S3PublicUrl = _configService.PodcastFeedS3PublicUrl,
            S3PublicOpmlUrl = _urlProvider.GetS3PublicOpmlUrl(),
            S3PublicFeedUrlTemplate = _urlProvider.GetS3PublicFeedUrl(0)?.Replace("/0.xml", "/{id}.xml")
        };
    }

    private static void ValidateUrl(string? url, string name)
    {
        if (url.IsNotNullOrWhiteSpace() && !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new BadRequestException($"{name} must be a full URL, eg. https://example.com");
        }
    }
}

public class PodcastFeedSettingsResource
{
    public bool Enabled { get; set; }
    public string? BaseUrl { get; set; }
    public string? Token { get; set; }

    // Where podcast apps reach Podarr's feeds, the external URL or this machine's address on the local network.
    public string? FeedBaseUrl { get; set; }

    public bool S3Enabled { get; set; }
    public string? S3Endpoint { get; set; }
    public string? S3Region { get; set; }
    public string? S3Bucket { get; set; }
    public string? S3AccessKey { get; set; }
    public string? S3SecretKey { get; set; }
    public bool HasS3SecretKey { get; set; }
    public string? S3Prefix { get; set; }
    public string? S3PublicUrl { get; set; }
    public string? S3PublicOpmlUrl { get; set; }
    public string? S3PublicFeedUrlTemplate { get; set; }
}
