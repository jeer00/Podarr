using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts.Events;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastService
    {
        Podcast GetPodcast(int podcastId);
        List<Podcast> GetPodcasts(IEnumerable<int> podcastIds);
        List<Podcast> GetAllPodcasts();
        Podcast FindByFeedUrl(string feedUrl);
        List<string> AllPodcastPaths();
        Podcast InsertPodcast(Podcast podcast);
        Podcast UpdatePodcast(Podcast podcast, bool publishUpdatedEvent = true);
        void DeletePodcast(int podcastId, bool deleteFiles);
        List<PodcastStatistics> GetStatistics();
        PodcastStatistics GetStatistics(int podcastId);
    }

    public class PodcastService : IPodcastService
    {
        private readonly IPodcastRepository _podcastRepository;
        private readonly IPodcastEpisodeRepository _episodeRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public PodcastService(IPodcastRepository podcastRepository,
                              IPodcastEpisodeRepository episodeRepository,
                              IDiskProvider diskProvider,
                              IRecycleBinProvider recycleBinProvider,
                              IEventAggregator eventAggregator,
                              Logger logger)
        {
            _podcastRepository = podcastRepository;
            _episodeRepository = episodeRepository;
            _diskProvider = diskProvider;
            _recycleBinProvider = recycleBinProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public Podcast GetPodcast(int podcastId)
        {
            return _podcastRepository.Get(podcastId);
        }

        public List<Podcast> GetPodcasts(IEnumerable<int> podcastIds)
        {
            return _podcastRepository.Get(podcastIds).ToList();
        }

        public List<Podcast> GetAllPodcasts()
        {
            return _podcastRepository.All().ToList();
        }

        public Podcast FindByFeedUrl(string feedUrl)
        {
            return _podcastRepository.FindByFeedUrl(feedUrl);
        }

        public List<string> AllPodcastPaths()
        {
            return _podcastRepository.AllPodcastPaths();
        }

        public Podcast InsertPodcast(Podcast podcast)
        {
            return _podcastRepository.Insert(podcast);
        }

        public Podcast UpdatePodcast(Podcast podcast, bool publishUpdatedEvent = true)
        {
            var updated = _podcastRepository.Update(podcast);

            if (publishUpdatedEvent)
            {
                _eventAggregator.PublishEvent(new PodcastUpdatedEvent(updated));
            }

            return updated;
        }

        public void DeletePodcast(int podcastId, bool deleteFiles)
        {
            var podcast = _podcastRepository.Get(podcastId);

            _episodeRepository.DeleteByPodcastIds(new List<int> { podcastId });
            _podcastRepository.Delete(podcastId);

            if (deleteFiles && podcast.Path.IsNotNullOrWhiteSpace() && _diskProvider.FolderExists(podcast.Path))
            {
                _logger.Info("Deleting podcast folder {0}", podcast.Path);
                _recycleBinProvider.DeleteFolder(podcast.Path);
            }

            _eventAggregator.PublishEvent(new PodcastDeletedEvent(new List<Podcast> { podcast }, deleteFiles));
        }

        public List<PodcastStatistics> GetStatistics()
        {
            return _episodeRepository.GetStatistics();
        }

        public PodcastStatistics GetStatistics(int podcastId)
        {
            return _episodeRepository.GetStatistics(podcastId).SingleOrDefault() ?? new PodcastStatistics { PodcastId = podcastId };
        }

        public static string GetEpisodePath(Podcast podcast, PodcastEpisode episode)
        {
            return episode.HasFile ? Path.Combine(podcast.Path, episode.RelativePath) : null;
        }
    }
}
