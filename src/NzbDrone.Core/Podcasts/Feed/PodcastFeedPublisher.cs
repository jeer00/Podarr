using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts.Events;
using NzbDrone.Core.Podcasts.Feed.S3;

namespace NzbDrone.Core.Podcasts.Feed
{
    public interface IPodcastFeedPublisher
    {
        void Test(S3Settings settings);
    }

    // Uploads the podcast feeds (only the XML, episode files are still served by Podarr) to an S3 compatible
    // bucket like Cloudflare R2, so podcast apps can fetch them from there. Feeds are re-uploaded when they change.
    public class PodcastFeedPublisher : IPodcastFeedPublisher,
                                        IExecute<PublishPodcastFeedsCommand>,
                                        IHandle<PodcastAddedEvent>,
                                        IHandle<PodcastUpdatedEvent>,
                                        IHandle<PodcastEpisodesUpdatedEvent>,
                                        IHandle<PodcastEpisodeDownloadedEvent>,
                                        IHandle<PodcastDeletedEvent>
    {
        private static readonly Regex LastBuildDateRegex = new(@"<lastBuildDate>[^<]*</lastBuildDate>", RegexOptions.Compiled);

        private readonly IPodcastService _podcastService;
        private readonly IPodcastEpisodeService _episodeService;
        private readonly IPodcastFeedUrlProvider _urlProvider;
        private readonly IS3Client _s3Client;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly ICached<string> _uploadedHashes;
        private readonly Debouncer _debouncer;
        private readonly HashSet<int> _pendingPodcastIds = new();
        private readonly Logger _logger;

        public PodcastFeedPublisher(IPodcastService podcastService,
                                    IPodcastEpisodeService episodeService,
                                    IPodcastFeedUrlProvider urlProvider,
                                    IS3Client s3Client,
                                    IManageCommandQueue commandQueueManager,
                                    ICacheManager cacheManager,
                                    IDebounceManager debounceManager,
                                    Logger logger)
        {
            _podcastService = podcastService;
            _episodeService = episodeService;
            _urlProvider = urlProvider;
            _s3Client = s3Client;
            _commandQueueManager = commandQueueManager;
            _uploadedHashes = cacheManager.GetCache<string>(GetType(), "uploadedFeedHashes");
            _debouncer = debounceManager.CreateDebouncer(PushPendingPublish, TimeSpan.FromSeconds(10));
            _logger = logger;
        }

        public void Execute(PublishPodcastFeedsCommand message)
        {
            if (!_urlProvider.IsS3PublishingEnabled())
            {
                _logger.ProgressInfo("Publishing feeds to S3 isn't enabled");
                return;
            }

            if (message.Force)
            {
                _uploadedHashes.Clear();
            }

            var settings = _urlProvider.GetS3Settings();
            var allPodcasts = _podcastService.GetAllPodcasts();
            var podcasts = message.PodcastIds.Any()
                ? allPodcasts.Where(p => message.PodcastIds.Contains(p.Id)).ToList()
                : allPodcasts;

            var episodeBaseUrl = _urlProvider.GetEpisodeFeedBaseUrl();
            var uploaded = 0;

            foreach (var podcast in podcasts)
            {
                try
                {
                    var rss = PodcastFeedBuilder.BuildRss(podcast,
                        _episodeService.GetEpisodesByPodcast(podcast.Id),
                        episodeBaseUrl,
                        _urlProvider.GetS3PublicFeedUrl(podcast.Id));

                    if (Upload(settings, _urlProvider.GetS3Key(podcast.Id), PodcastFeedBuilder.ToXml(rss), "application/rss+xml; charset=utf-8"))
                    {
                        uploaded++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to upload the feed of {0}", podcast.Title);
                }
            }

            // The OPML can only point at the uploaded feeds when the bucket's public URL is known.
            if (_urlProvider.GetS3PublicOpmlUrl() != null)
            {
                try
                {
                    var opml = PodcastFeedBuilder.BuildOpml(allPodcasts, p => _urlProvider.GetS3PublicFeedUrl(p.Id));
                    Upload(settings, _urlProvider.GetS3OpmlKey(), PodcastFeedBuilder.ToXml(opml), "text/x-opml; charset=utf-8");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to upload the podcast OPML");
                }
            }

            _logger.ProgressInfo("Uploaded {0} changed feed(s) to {1}", uploaded, settings.Bucket);
        }

        public void Test(S3Settings settings)
        {
            var key = $"{_urlProvider.GetS3Key(0).Replace("/0.xml", string.Empty)}/podarr-test.txt";

            _s3Client.PutObject(settings, key, Encoding.UTF8.GetBytes("Podarr connection test"), "text/plain");
            _s3Client.DeleteObject(settings, key);
        }

        private bool Upload(S3Settings settings, string key, string content, string contentType)
        {
            // lastBuildDate changes on every build, leave it out when deciding if anything changed.
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LastBuildDateRegex.Replace(content, string.Empty))));

            if (_uploadedHashes.Find(key) == hash)
            {
                return false;
            }

            _s3Client.PutObject(settings, key, Encoding.UTF8.GetBytes(content), contentType);
            _uploadedHashes.Set(key, hash);
            _logger.Debug("Uploaded {0} to {1}", key, settings.Bucket);

            return true;
        }

        private void QueuePublish(int podcastId)
        {
            if (!_urlProvider.IsS3PublishingEnabled())
            {
                return;
            }

            lock (_pendingPodcastIds)
            {
                _pendingPodcastIds.Add(podcastId);
            }

            _debouncer.Execute();
        }

        private void PushPendingPublish()
        {
            List<int> podcastIds;

            lock (_pendingPodcastIds)
            {
                podcastIds = _pendingPodcastIds.ToList();
                _pendingPodcastIds.Clear();
            }

            if (podcastIds.Any())
            {
                _commandQueueManager.Push(new PublishPodcastFeedsCommand { PodcastIds = podcastIds });
            }
        }

        public void Handle(PodcastAddedEvent message)
        {
            QueuePublish(message.Podcast.Id);
        }

        public void Handle(PodcastUpdatedEvent message)
        {
            QueuePublish(message.Podcast.Id);
        }

        public void Handle(PodcastEpisodesUpdatedEvent message)
        {
            QueuePublish(message.PodcastId);
        }

        public void Handle(PodcastEpisodeDownloadedEvent message)
        {
            QueuePublish(message.Podcast.Id);
        }

        public void Handle(PodcastDeletedEvent message)
        {
            if (!_urlProvider.IsS3PublishingEnabled())
            {
                return;
            }

            var settings = _urlProvider.GetS3Settings();

            foreach (var podcast in message.Podcasts)
            {
                try
                {
                    var key = _urlProvider.GetS3Key(podcast.Id);
                    _s3Client.DeleteObject(settings, key);
                    _uploadedHashes.Remove(key);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to delete the feed of {0} from {1}", podcast.Title, settings.Bucket);
                }
            }

            // Update the OPML so it no longer lists the deleted podcasts.
            _commandQueueManager.Push(new PublishPodcastFeedsCommand { PodcastIds = new List<int> { -1 } });
        }
    }
}
