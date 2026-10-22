using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Podcasts.Tracker.Commands;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public interface IPodcastEpisodeSearchService
    {
        int SearchWantedEpisodes(Podcast podcast, bool automatic);
    }

    // Finds the full version of feed episodes on trackers, for shows that only publish previews on their free feed.
    public class PodcastEpisodeSearchService : IPodcastEpisodeSearchService, IExecute<SearchPodcastEpisodesCommand>
    {
        // Only date, episode number and full title matches are trusted enough to grab without review.
        public const int MinimumAutomaticScore = 80;
        public const int AutomaticSearchDays = 14;
        private const int MaxGrabsPerSearch = 20;

        private readonly IPodcastService _podcastService;
        private readonly IPodcastEpisodeService _episodeService;
        private readonly IPodcastReleaseService _releaseService;
        private readonly IPodcastDownloadRepository _downloadRepository;
        private readonly Logger _logger;

        public PodcastEpisodeSearchService(IPodcastService podcastService,
                                           IPodcastEpisodeService episodeService,
                                           IPodcastReleaseService releaseService,
                                           IPodcastDownloadRepository downloadRepository,
                                           Logger logger)
        {
            _podcastService = podcastService;
            _episodeService = episodeService;
            _releaseService = releaseService;
            _downloadRepository = downloadRepository;
            _logger = logger;
        }

        public void Execute(SearchPodcastEpisodesCommand message)
        {
            var podcasts = message.PodcastIds.Any()
                ? _podcastService.GetPodcasts(message.PodcastIds)
                : _podcastService.GetAllPodcasts().Where(p => p.EpisodeSource != PodcastEpisodeSource.Feed).ToList();

            var grabbed = 0;

            foreach (var podcast in podcasts.Where(p => p.Monitored))
            {
                try
                {
                    grabbed += SearchWantedEpisodes(podcast, message.Automatic);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to search trackers for episodes of {0}", podcast.Title);
                }
            }

            _logger.ProgressInfo("Grabbed {0} episode(s) from trackers", grabbed);
        }

        public int SearchWantedEpisodes(Podcast podcast, bool automatic)
        {
            var downloads = _downloadRepository.GetByPodcastId(podcast.Id);

            // Episodes with a download in progress or already imported from a tracker aren't searched again.
            var busyEpisodeIds = downloads
                .Where(d => d.PodcastEpisodeId.HasValue && (d.IsActive || d.Status == PodcastDownloadStatus.Imported))
                .Select(d => d.PodcastEpisodeId.Value)
                .ToHashSet();

            var failedGuids = downloads
                .Where(d => d.Status == PodcastDownloadStatus.Failed && d.ReleaseGuid.IsNotNullOrWhiteSpace())
                .Select(d => d.ReleaseGuid)
                .ToHashSet();

            var cutoff = DateTime.UtcNow.AddDays(-AutomaticSearchDays);

            var wanted = _episodeService.GetEpisodesByPodcast(podcast.Id)
                .Where(e => e.Monitored && e.FileSource != PodcastFileSource.Tracker && !busyEpisodeIds.Contains(e.Id))
                .Where(e => !automatic || (e.AirDateUtc ?? e.Added) >= cutoff)
                .OrderByDescending(e => e.AirDateUtc ?? DateTime.MinValue)
                .ToList();

            if (wanted.Empty())
            {
                _logger.Debug("No episodes of {0} are waiting for a tracker release", podcast.Title);
                return 0;
            }

            _logger.ProgressInfo("Searching trackers for {0} episode(s) of {1}", wanted.Count, podcast.Title);

            var releases = _releaseService.Search(podcast, null, false)
                .Where(r => r.MatchesPodcast && !r.IsGrabbed && !failedGuids.Contains(r.Release.Guid))
                .Select(r => r.Release)
                .ToList();

            var term = podcast.GetTrackerSearchTerm();
            var usedGuids = new HashSet<string>();
            var grabbed = 0;

            foreach (var episode in wanted)
            {
                if (grabbed >= MaxGrabsPerSearch)
                {
                    _logger.Info("Grabbed {0} releases for {1}, the rest will be searched next time", grabbed, podcast.Title);
                    break;
                }

                var release = FindRelease(term, episode, releases.Where(r => !usedGuids.Contains(r.Guid)));

                if (release == null)
                {
                    continue;
                }

                try
                {
                    _releaseService.Grab(podcast, episode, release);
                    usedGuids.Add(release.Guid);
                    grabbed++;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to grab '{0}' for episode '{1}'", release.Title, episode.Title);
                }
            }

            return grabbed;
        }

        // The tracker release is always preferred over the feed's copy of an episode,
        // the feed often only has a preview of it.
        public static ReleaseInfo FindRelease(string term, PodcastEpisode episode, IEnumerable<ReleaseInfo> releases)
        {
            return releases
                .Select(r => new { Release = r, Score = PodcastReleaseMatcher.GetEpisodeScore(term, episode, r.Title) })
                .Where(r => r.Score >= MinimumAutomaticScore)
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => TorrentInfo.GetSeeders(r.Release) ?? 0)
                .ThenByDescending(r => r.Release.PublishDate)
                .Select(r => r.Release)
                .FirstOrDefault();
        }
    }
}
