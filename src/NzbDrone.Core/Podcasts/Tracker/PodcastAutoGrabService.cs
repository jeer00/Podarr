using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Podcasts.Tracker.Commands;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public class PodcastAutoGrabService : IExecute<GrabPodcastReleasesCommand>
    {
        // Protects against flooding the download client when a tracker returns lots of new matches at once.
        private const int MaxGrabsPerPodcast = 10;

        private readonly IPodcastService _podcastService;
        private readonly IPodcastReleaseService _releaseService;
        private readonly IPodcastDownloadRepository _downloadRepository;
        private readonly Logger _logger;

        public PodcastAutoGrabService(IPodcastService podcastService,
                                      IPodcastReleaseService releaseService,
                                      IPodcastDownloadRepository downloadRepository,
                                      Logger logger)
        {
            _podcastService = podcastService;
            _releaseService = releaseService;
            _downloadRepository = downloadRepository;
            _logger = logger;
        }

        public void Execute(GrabPodcastReleasesCommand message)
        {
            var podcasts = (message.PodcastIds.Any()
                    ? _podcastService.GetPodcasts(message.PodcastIds)
                    : _podcastService.GetAllPodcasts())
                .Where(p => p.Monitored && p.GrabTrackerReleases)
                .ToList();

            var grabbed = 0;

            foreach (var podcast in podcasts)
            {
                try
                {
                    grabbed += GrabNewReleases(podcast);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to grab tracker releases for {0}", podcast.Title);
                }
            }

            _logger.ProgressInfo("Grabbed {0} tracker release(s)", grabbed);
        }

        private int GrabNewReleases(Podcast podcast)
        {
            _logger.ProgressInfo("Searching trackers for {0}", podcast.Title);

            var since = podcast.GrabTrackerReleasesSince ?? podcast.Added;
            var previous = _downloadRepository.GetByPodcastId(podcast.Id);
            var previousTitles = previous.Select(d => PodcastReleaseMatcher.Normalize(d.Title)).ToHashSet();

            var candidates = _releaseService.Search(podcast, null, false)
                .Where(r => r.MatchesPodcast && !r.IsGrabbed)
                .Where(r => r.Release.PublishDate >= since)
                .Where(r => !previousTitles.Contains(PodcastReleaseMatcher.Normalize(r.Release.Title)))
                .GroupBy(r => PodcastReleaseMatcher.Normalize(r.Release.Title))
                .Select(g => g.OrderByDescending(r => (r.Release as TorrentInfo)?.Seeders ?? 0).First())
                .OrderBy(r => r.Release.PublishDate)
                .Take(MaxGrabsPerPodcast)
                .ToList();

            var grabbed = 0;

            foreach (var candidate in candidates)
            {
                try
                {
                    _releaseService.Grab(podcast, null, candidate.Release);
                    grabbed++;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to grab '{0}'", candidate.Release.Title);
                }
            }

            return grabbed;
        }
    }
}
