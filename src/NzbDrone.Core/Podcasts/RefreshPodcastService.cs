using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Podcasts.Commands;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Podcasts.Tracker;
using NzbDrone.Core.Podcasts.Tracker.Commands;

namespace NzbDrone.Core.Podcasts
{
    public interface IRefreshPodcastService
    {
        void ApplyFeedMetadata(Podcast podcast, PodcastFeed feed);
        void SyncEpisodes(Podcast podcast, PodcastFeed feed, PodcastMonitorType? initialMonitor = null);
    }

    public class RefreshPodcastService : IRefreshPodcastService, IExecute<RefreshPodcastCommand>
    {
        private readonly IPodcastService _podcastService;
        private readonly IPodcastEpisodeRepository _episodeRepository;
        private readonly IPodcastFeedParser _feedParser;
        private readonly IDiskProvider _diskProvider;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public RefreshPodcastService(IPodcastService podcastService,
                                     IPodcastEpisodeRepository episodeRepository,
                                     IPodcastFeedParser feedParser,
                                     IDiskProvider diskProvider,
                                     IManageCommandQueue commandQueueManager,
                                     Logger logger)
        {
            _podcastService = podcastService;
            _episodeRepository = episodeRepository;
            _feedParser = feedParser;
            _diskProvider = diskProvider;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public void ApplyFeedMetadata(Podcast podcast, PodcastFeed feed)
        {
            podcast.Title = feed.Title;
            podcast.SortTitle = PodcastTitleNormalizer.Normalize(feed.Title);
            podcast.Description = feed.Description;
            podcast.Author = feed.Author;
            podcast.WebsiteUrl = feed.WebsiteUrl;
            podcast.ImageUrl = feed.ImageUrl ?? podcast.ImageUrl;
            podcast.Language = feed.Language;
            podcast.Categories = feed.Categories;
            podcast.Explicit = feed.Explicit;
        }

        public void SyncEpisodes(Podcast podcast, PodcastFeed feed, PodcastMonitorType? initialMonitor = null)
        {
            var existing = _episodeRepository.GetByPodcastId(podcast.Id);
            var existingByGuid = existing.ToDictionary(e => e.FeedGuid);
            var toInsert = new List<PodcastEpisode>();
            var toUpdate = new List<PodcastEpisode>();
            var now = DateTime.UtcNow;

            foreach (var item in feed.Items)
            {
                if (!existingByGuid.TryGetValue(item.Guid, out var episode))
                {
                    episode = new PodcastEpisode
                    {
                        PodcastId = podcast.Id,
                        FeedGuid = item.Guid,
                        Added = now,
                        Monitored = initialMonitor.HasValue ? initialMonitor == PodcastMonitorType.All : podcast.MonitorNewEpisodes
                    };

                    toInsert.Add(episode);
                }
                else
                {
                    toUpdate.Add(episode);
                }

                episode.Title = item.Title;
                episode.Description = item.Description;
                episode.AirDateUtc = item.AirDateUtc;
                episode.SeasonNumber = item.SeasonNumber;
                episode.EpisodeNumber = item.EpisodeNumber ?? PodcastReleaseMatcher.ParseEpisodeNumberFromTitle(item.Title);
                episode.EpisodeType = item.EpisodeType;
                episode.Duration = item.Duration;
                episode.EnclosureUrl = item.EnclosureUrl;
                episode.EnclosureType = item.EnclosureType;
                episode.EnclosureLength = item.EnclosureLength;
                episode.ImageUrl = item.ImageUrl;
                episode.Explicit = item.Explicit;
            }

            if (initialMonitor == PodcastMonitorType.Latest)
            {
                var latest = toInsert.Where(e => e.EnclosureUrl.IsNotNullOrWhiteSpace())
                                     .MaxBy(e => e.AirDateUtc ?? DateTime.MinValue);

                if (latest != null)
                {
                    latest.Monitored = true;
                }
            }

            // Episodes that dropped out of the feed are kept, most feeds only list the newest N episodes.
            foreach (var episode in existing.Where(e => e.HasFile))
            {
                var path = PodcastService.GetEpisodePath(podcast, episode);

                if (!_diskProvider.FileExists(path))
                {
                    _logger.Info("Episode file for '{0}' is missing from disk, unmonitoring", episode.Title);

                    episode.RelativePath = null;
                    episode.FileSource = PodcastFileSource.None;
                    episode.Size = 0;
                    episode.DateDownloaded = null;
                    episode.Monitored = false;

                    if (!toUpdate.Contains(episode))
                    {
                        toUpdate.Add(episode);
                    }
                }
            }

            _episodeRepository.InsertMany(toInsert);
            _episodeRepository.UpdateMany(toUpdate);

            _logger.Debug("Synced podcast {0}: {1} new, {2} updated", podcast.Title, toInsert.Count, toUpdate.Count);
        }

        private void RefreshPodcast(Podcast podcast)
        {
            _logger.ProgressInfo("Refreshing podcast {0}", podcast.Title);

            try
            {
                var feed = _feedParser.Fetch(podcast.FeedUrl);

                ApplyFeedMetadata(podcast, feed);

                if (feed.NewFeedUrl.IsNotNullOrWhiteSpace() && feed.NewFeedUrl != podcast.FeedUrl && _podcastService.FindByFeedUrl(feed.NewFeedUrl) == null)
                {
                    _logger.Info("Podcast {0} has moved to a new feed URL: {1}", podcast.Title, feed.NewFeedUrl);
                    podcast.FeedUrl = feed.NewFeedUrl;
                }

                SyncEpisodes(podcast, feed);

                podcast.LastRefreshError = null;
            }
            catch (PodcastFeedException ex)
            {
                _logger.Warn("Unable to refresh podcast {0}: {1}", podcast.Title, ex.Message);
                podcast.LastRefreshError = ex.Message;
            }

            podcast.LastRefreshed = DateTime.UtcNow;
            _podcastService.UpdatePodcast(podcast);
        }

        public void Execute(RefreshPodcastCommand message)
        {
            var podcasts = message.PodcastIds.Any()
                ? _podcastService.GetPodcasts(message.PodcastIds)
                : _podcastService.GetAllPodcasts().OrderBy(p => p.SortTitle).ToList();

            foreach (var podcast in podcasts)
            {
                try
                {
                    RefreshPodcast(podcast);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Couldn't refresh podcast {0}", podcast);
                }
            }

            var episodeSearchIds = podcasts.Where(p => p.Monitored && p.EpisodeSource != PodcastEpisodeSource.Feed).Select(p => p.Id).ToList();

            if (episodeSearchIds.Any())
            {
                _commandQueueManager.Push(new SearchPodcastEpisodesCommand { PodcastIds = episodeSearchIds, Automatic = true });
            }

            var trackerIds = podcasts.Where(p => p.Monitored && p.GrabTrackerReleases).Select(p => p.Id).ToList();

            if (trackerIds.Any())
            {
                _commandQueueManager.Push(new GrabPodcastReleasesCommand { PodcastIds = trackerIds });
            }

            var monitoredIds = podcasts.Where(p => p.Monitored).Select(p => p.Id).ToList();

            if (monitoredIds.Count == 1)
            {
                _commandQueueManager.Push(new DownloadPodcastEpisodesCommand { PodcastId = monitoredIds.First() });
            }
            else if (monitoredIds.Any())
            {
                _commandQueueManager.Push(new DownloadPodcastEpisodesCommand());
            }
        }
    }
}
