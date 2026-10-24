using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts.Events;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastEpisodeService
    {
        PodcastEpisode GetEpisode(int id);
        List<PodcastEpisode> GetEpisodes(IEnumerable<int> ids);
        List<PodcastEpisode> GetEpisodesByPodcast(int podcastId);
        List<PodcastEpisode> GetEpisodesToDownload(int? podcastId);
        List<PodcastEpisode> SetMonitored(IEnumerable<int> ids, bool monitored);
        void UpdateEpisode(PodcastEpisode episode);
        PodcastEpisode DeleteEpisodeFile(int id);
    }

    public class PodcastEpisodeService : IPodcastEpisodeService
    {
        private readonly IPodcastEpisodeRepository _episodeRepository;
        private readonly IPodcastRepository _podcastRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public PodcastEpisodeService(IPodcastEpisodeRepository episodeRepository,
                                     IPodcastRepository podcastRepository,
                                     IDiskProvider diskProvider,
                                     IRecycleBinProvider recycleBinProvider,
                                     IEventAggregator eventAggregator,
                                     Logger logger)
        {
            _episodeRepository = episodeRepository;
            _podcastRepository = podcastRepository;
            _diskProvider = diskProvider;
            _recycleBinProvider = recycleBinProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public PodcastEpisode GetEpisode(int id)
        {
            return _episodeRepository.Get(id);
        }

        public List<PodcastEpisode> GetEpisodes(IEnumerable<int> ids)
        {
            return _episodeRepository.Get(ids).ToList();
        }

        public List<PodcastEpisode> GetEpisodesByPodcast(int podcastId)
        {
            return _episodeRepository.GetByPodcastId(podcastId);
        }

        public List<PodcastEpisode> GetEpisodesToDownload(int? podcastId)
        {
            return _episodeRepository.GetEpisodesToDownload(podcastId);
        }

        public List<PodcastEpisode> SetMonitored(IEnumerable<int> ids, bool monitored)
        {
            var episodes = _episodeRepository.Get(ids).ToList();

            episodes.ForEach(e => e.Monitored = monitored);
            _episodeRepository.SetFields(episodes, e => e.Monitored);

            PublishUpdated(episodes);

            return episodes;
        }

        public void UpdateEpisode(PodcastEpisode episode)
        {
            _episodeRepository.Update(episode);

            PublishUpdated(new List<PodcastEpisode> { episode });
        }

        public PodcastEpisode DeleteEpisodeFile(int id)
        {
            var episode = _episodeRepository.Get(id);

            if (!episode.HasFile)
            {
                return episode;
            }

            var podcast = _podcastRepository.Get(episode.PodcastId);
            var path = PodcastService.GetEpisodePath(podcast, episode);

            if (_diskProvider.FileExists(path))
            {
                _logger.Info("Deleting podcast episode file {0}", path);
                _recycleBinProvider.DeleteFile(path);
            }

            // Unmonitor so the next refresh doesn't immediately download it again.
            episode.RelativePath = null;
            episode.FileSource = PodcastFileSource.None;
            episode.Size = 0;
            episode.DateDownloaded = null;
            episode.Monitored = false;

            _episodeRepository.Update(episode);
            PublishUpdated(new List<PodcastEpisode> { episode });

            return episode;
        }

        private void PublishUpdated(List<PodcastEpisode> episodes)
        {
            foreach (var group in episodes.GroupBy(e => e.PodcastId))
            {
                _eventAggregator.PublishEvent(new PodcastEpisodesUpdatedEvent(group.Key, group.ToList()));
            }
        }
    }
}
