using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Http;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastService
    {
        Task<List<Podcast>> GetAllPodcasts();
        Task<Podcast> GetPodcast(int id);
        Task<Podcast> AddPodcast(Podcast podcast);
        Task UpdatePodcast(Podcast podcast);
        Task DeletePodcast(int id);
        Task<List<PodcastEpisode>> GetEpisodes(int podcastId);
        Task<PodcastEpisode> GetEpisode(int id);
        Task UpdateEpisode(PodcastEpisode episode);
        Task DeleteEpisode(int id);
        Task RefreshPodcast(int id);
        Task<List<Podcast>> SearchPodcasts(string query);
    }

    public class PodcastService : IPodcastService
    {
        private readonly IPodcastRssParser _rssParser;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly IPodcastRepository _podcastRepository;
        private readonly IPodcastEpisodeRepository _episodeRepository;

        public PodcastService(IPodcastRssParser rssParser,
                             IQualityProfileService qualityProfileService,
                             IPodcastRepository podcastRepository,
                             IPodcastEpisodeRepository episodeRepository)
        {
            _rssParser = rssParser;
            _qualityProfileService = qualityProfileService;
            _podcastRepository = podcastRepository;
            _episodeRepository = episodeRepository;
        }

        public async Task<List<Podcast>> GetAllPodcasts()
        {
            return await _podcastRepository.GetAll();
        }

        public async Task<Podcast> GetPodcast(int id)
        {
            return await _podcastRepository.Get(id);
        }

        public async Task<Podcast> AddPodcast(Podcast podcast)
        {
            podcast.Added = DateTime.UtcNow;
            podcast.Monitored = true;
            return await _podcastRepository.Insert(podcast);
        }

        public async Task UpdatePodcast(Podcast podcast)
        {
            await _podcastRepository.Update(podcast);
        }

        public async Task DeletePodcast(int id)
        {
            await _podcastRepository.Delete(id);
        }

        public async Task<List<PodcastEpisode>> GetEpisodes(int podcastId)
        {
            return await _episodeRepository.GetByPodcastId(podcastId);
        }

        public async Task<PodcastEpisode> GetEpisode(int id)
        {
            return await _episodeRepository.Get(id);
        }

        public async Task UpdateEpisode(PodcastEpisode episode)
        {
            await _episodeRepository.Update(episode);
        }

        public async Task DeleteEpisode(int id)
        {
            await _episodeRepository.Delete(id);
        }

        public async Task RefreshPodcast(int id)
        {
            var podcast = await GetPodcast(id);
            if (podcast == null) return;

            var episodes = await _rssParser.ParseFeed(podcast.RssUrl, podcast);
            
            // Remove existing episodes
            await _episodeRepository.DeleteByPodcastId(podcast.Id);
            
            // Insert new episodes
            await _episodeRepository.BulkInsert(episodes);
            
            // Update podcast last updated time
            podcast.LastUpdated = DateTime.UtcNow;
            await UpdatePodcast(podcast);
        }

        public async Task<List<Podcast>> SearchPodcasts(string query)
        {
            // TODO: Implement podcast search
            return new List<Podcast>();
        }
    }
}
