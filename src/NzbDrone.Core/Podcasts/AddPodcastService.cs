using System;
using System.Collections.Generic;
using System.IO;
using FluentValidation;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Podcasts.Commands;
using NzbDrone.Core.Podcasts.Events;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Podcasts.Tracker.Commands;

namespace NzbDrone.Core.Podcasts
{
    public interface IAddPodcastService
    {
        Podcast AddPodcast(Podcast newPodcast, PodcastMonitorType monitor);
    }

    public class AddPodcastService : IAddPodcastService
    {
        private readonly IPodcastService _podcastService;
        private readonly IRefreshPodcastService _refreshPodcastService;
        private readonly IPodcastFeedParser _feedParser;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public AddPodcastService(IPodcastService podcastService,
                                 IRefreshPodcastService refreshPodcastService,
                                 IPodcastFeedParser feedParser,
                                 IManageCommandQueue commandQueueManager,
                                 IEventAggregator eventAggregator,
                                 Logger logger)
        {
            _podcastService = podcastService;
            _refreshPodcastService = refreshPodcastService;
            _feedParser = feedParser;
            _commandQueueManager = commandQueueManager;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public Podcast AddPodcast(Podcast newPodcast, PodcastMonitorType monitor)
        {
            Ensure.That(newPodcast, () => newPodcast).IsNotNull();

            var feedUrl = newPodcast.FeedUrl?.Trim();

            if (_podcastService.FindByFeedUrl(feedUrl) != null)
            {
                throw new ValidationException(new List<ValidationFailure>
                {
                    new("FeedUrl", "This podcast has already been added", feedUrl)
                });
            }

            PodcastFeed feed;

            try
            {
                feed = _feedParser.Fetch(feedUrl);
            }
            catch (PodcastFeedException ex)
            {
                throw new ValidationException(new List<ValidationFailure>
                {
                    new("FeedUrl", ex.Message, feedUrl)
                });
            }

            newPodcast.FeedUrl = feedUrl;
            _refreshPodcastService.ApplyFeedMetadata(newPodcast, feed);

            if (newPodcast.Path.IsNullOrWhiteSpace())
            {
                var folderName = FileNameBuilder.CleanFolderName(FileNameBuilder.CleanFileName(newPodcast.Title));
                newPodcast.Path = Path.Combine(newPodcast.RootFolderPath, folderName);
            }

            if (monitor is PodcastMonitorType.None)
            {
                newPodcast.MonitorNewEpisodes = false;
            }

            newPodcast.Added = DateTime.UtcNow;
            newPodcast.LastRefreshed = DateTime.UtcNow;

            _logger.Info("Adding podcast {0} Path: [{1}]", newPodcast, newPodcast.Path);

            var podcast = _podcastService.InsertPodcast(newPodcast);

            _refreshPodcastService.SyncEpisodes(podcast, feed, monitor);

            _eventAggregator.PublishEvent(new PodcastAddedEvent(podcast));

            if (podcast.Monitored && monitor is PodcastMonitorType.All or PodcastMonitorType.Latest)
            {
                if (podcast.EpisodeSource != PodcastEpisodeSource.Tracker)
                {
                    _commandQueueManager.Push(new DownloadPodcastEpisodesCommand { PodcastId = podcast.Id });
                }

                if (podcast.EpisodeSource != PodcastEpisodeSource.Feed)
                {
                    _commandQueueManager.Push(new SearchPodcastEpisodesCommand { PodcastIds = new List<int> { podcast.Id } });
                }
            }

            return podcast;
        }
    }
}
