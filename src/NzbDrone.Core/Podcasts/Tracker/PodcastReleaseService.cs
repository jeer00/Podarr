using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MonoTorrent;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Podcasts.Tracker.Events;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public class PodcastRelease
    {
        public ReleaseInfo Release { get; set; }
        public bool MatchesPodcast { get; set; }
        public int EpisodeScore { get; set; }
        public bool IsGrabbed { get; set; }
    }

    public interface IPodcastReleaseService
    {
        List<PodcastRelease> Search(Podcast podcast, PodcastEpisode episode, bool interactive);
        PodcastDownload Grab(int podcastId, int? episodeId, int indexerId, string guid);
        PodcastDownload Grab(Podcast podcast, PodcastEpisode episode, ReleaseInfo release);
    }

    public class PodcastReleaseService : IPodcastReleaseService
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly ISeedConfigProvider _seedConfigProvider;
        private readonly IPodcastService _podcastService;
        private readonly IPodcastEpisodeService _episodeService;
        private readonly IPodcastDownloadRepository _downloadRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICached<ReleaseInfo> _releaseCache;
        private readonly IHttpClient _httpClient;
        private readonly ITorrentFileInfoReader _torrentFileInfoReader;
        private readonly Logger _logger;

        public PodcastReleaseService(IIndexerFactory indexerFactory,
                                     IProvideDownloadClient downloadClientProvider,
                                     ISeedConfigProvider seedConfigProvider,
                                     IPodcastService podcastService,
                                     IPodcastEpisodeService episodeService,
                                     IPodcastDownloadRepository downloadRepository,
                                     IEventAggregator eventAggregator,
                                     ICacheManager cacheManager,
                                     IHttpClient httpClient,
                                     ITorrentFileInfoReader torrentFileInfoReader,
                                     Logger logger)
        {
            _httpClient = httpClient;
            _torrentFileInfoReader = torrentFileInfoReader;
            _indexerFactory = indexerFactory;
            _downloadClientProvider = downloadClientProvider;
            _seedConfigProvider = seedConfigProvider;
            _podcastService = podcastService;
            _episodeService = episodeService;
            _downloadRepository = downloadRepository;
            _eventAggregator = eventAggregator;
            _releaseCache = cacheManager.GetCache<ReleaseInfo>(GetType(), "podcastReleases");
            _logger = logger;
        }

        public List<PodcastRelease> Search(Podcast podcast, PodcastEpisode episode, bool interactive)
        {
            var indexers = interactive
                ? _indexerFactory.InteractiveSearchEnabled()
                : _indexerFactory.AutomaticSearchEnabled();

            if (indexers.Empty())
            {
                _logger.Debug("No indexers enabled for {0} search", interactive ? "interactive" : "automatic");
                return new List<PodcastRelease>();
            }

            var terms = podcast.GetTrackerSearchTerms();

            // Searching with the episode title too rarely returns anything on trackers,
            // so search by podcast and rank the results against the episode instead.
            var criteria = new PodcastSearchCriteria
            {
                PodcastTitle = podcast.Title,
                EpisodeQueryTitles = terms.Select(PodcastReleaseMatcher.CleanQuery).Where(t => t.IsNotNullOrWhiteSpace()).Distinct().ToArray(),
                InteractiveSearch = interactive,
                UserInvokedSearch = interactive
            };

            var tasks = indexers.Select(indexer => FetchFromIndexer(indexer, criteria)).ToArray();
            Task.WaitAll(tasks);

            var grabbedGuids = _downloadRepository.GetByPodcastId(podcast.Id)
                .Where(d => d.ReleaseGuid.IsNotNullOrWhiteSpace())
                .Select(d => d.ReleaseGuid)
                .ToHashSet();

            var releases = tasks.SelectMany(t => t.Result)
                .DistinctBy(r => (r.IndexerId, r.Guid))
                .Select(release =>
                {
                    _releaseCache.Set(GetCacheKey(release.IndexerId, release.Guid), release, TimeSpan.FromMinutes(30));

                    return new PodcastRelease
                    {
                        Release = release,
                        MatchesPodcast = PodcastReleaseMatcher.IsPodcastMatch(terms, release.Title),
                        EpisodeScore = episode == null ? 0 : terms.Max(term => PodcastReleaseMatcher.GetEpisodeScore(term, episode, release.Title)),
                        IsGrabbed = grabbedGuids.Contains(release.Guid)
                    };
                })
                .OrderByDescending(r => r.EpisodeScore)
                .ThenByDescending(r => r.MatchesPodcast)
                .ThenByDescending(r => r.Release.PublishDate)
                .ToList();

            _logger.Debug("Found {0} releases for {1}", releases.Count, criteria);

            return releases;
        }

        public PodcastDownload Grab(int podcastId, int? episodeId, int indexerId, string guid)
        {
            var release = _releaseCache.Find(GetCacheKey(indexerId, guid));

            if (release == null)
            {
                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Release not found or expired, search again");
            }

            var podcast = _podcastService.GetPodcast(podcastId);
            var episode = episodeId.HasValue ? _episodeService.GetEpisode(episodeId.Value) : null;

            return Grab(podcast, episode, release);
        }

        public PodcastDownload Grab(Podcast podcast, PodcastEpisode episode, ReleaseInfo release)
        {
            var downloadClient = _downloadClientProvider.GetDownloadClient(release.DownloadProtocol, release.IndexerId, false, podcast.Tags);

            if (downloadClient == null)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, "No {0} download client is available, add one in Settings: Download Clients", release.DownloadProtocol);
            }

            var indexer = _indexerFactory.GetInstance(_indexerFactory.Get(release.IndexerId));

            // Download clients are built around TV releases, give them a minimal stand-in for the series.
            var remoteEpisode = new RemoteEpisode
            {
                Release = release,
                Series = new Series { Title = podcast.Title, Tags = new HashSet<int>() },
                ReleaseSource = ReleaseSourceType.InteractiveSearch,
                SeedConfiguration = _seedConfigProvider.GetSeedConfiguration(release.IndexerId, false)
            };

            _logger.Info("Grabbing '{0}' for podcast {1} from {2}", release.Title, podcast.Title, release.Indexer);

            string downloadId;

            try
            {
                downloadId = downloadClient.Download(remoteEpisode, indexer).GetAwaiter().GetResult();
            }
            catch (Exception ex) when ((ex is DownloadClientRejectedReleaseException or DownloadClientException) &&
                                       release.DownloadProtocol == DownloadProtocol.Torrent &&
                                       GetExistingTorrentHash(release, indexer, downloadClient) is { } existingHash)
            {
                // Already in the download client, eg. grabbed before. Track it so its files still get imported.
                _logger.Info("'{0}' is already in {1}, importing it from there", release.Title, downloadClient.Definition.Name);
                downloadId = existingHash;
            }
            catch (Exception ex) when (ex is ReleaseDownloadException or DownloadClientException)
            {
                throw new NzbDroneClientException(HttpStatusCode.Conflict, "Unable to send release to {0}: {1}", downloadClient.Definition.Name, ex.Message);
            }

            var download = _downloadRepository.Insert(new PodcastDownload
            {
                PodcastId = podcast.Id,
                PodcastEpisodeId = episode?.Id,
                DownloadClientId = downloadClient.Definition.Id,
                DownloadId = downloadId,
                Protocol = release.DownloadProtocol,
                IndexerId = release.IndexerId,
                Indexer = release.Indexer,
                ReleaseGuid = release.Guid,
                Title = release.Title,
                Size = release.Size,
                Status = PodcastDownloadStatus.Queued,
                Added = DateTime.UtcNow
            });

            _eventAggregator.PublishEvent(new PodcastDownloadsUpdatedEvent(podcast.Id));

            return download;
        }

        private string GetExistingTorrentHash(ReleaseInfo release, IIndexer indexer, IDownloadClient downloadClient)
        {
            var hash = GetTorrentHash(release, indexer);

            if (hash.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                return downloadClient.GetItems().Any(item => string.Equals(item.DownloadId, hash, StringComparison.OrdinalIgnoreCase))
                    ? hash
                    : null;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to check whether '{0}' is already in {1}", release.Title, downloadClient.Definition.Name);
                return null;
            }
        }

        private string GetTorrentHash(ReleaseInfo release, IIndexer indexer)
        {
            try
            {
                if (release is TorrentInfo torrent && torrent.InfoHash.IsNotNullOrWhiteSpace())
                {
                    return torrent.InfoHash.ToUpperInvariant();
                }

                var magnetUrl = (release as TorrentInfo)?.MagnetUrl ?? release.DownloadUrl;

                if (magnetUrl.IsNotNullOrWhiteSpace() && magnetUrl.StartsWith("magnet:"))
                {
                    return MagnetLink.Parse(magnetUrl).InfoHashes.V1OrV2.ToHex().ToUpperInvariant();
                }

                var response = _httpClient.Get(indexer.GetDownloadRequest(release.DownloadUrl));

                return _torrentFileInfoReader.GetHashFromTorrentFile(response.ResponseData).ToUpperInvariant();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to get the torrent hash of '{0}'", release.Title);
                return null;
            }
        }

        private async Task<IList<ReleaseInfo>> FetchFromIndexer(IIndexer indexer, PodcastSearchCriteria criteria)
        {
            try
            {
                return await indexer.Fetch(criteria);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while searching {0} on {1}", criteria, indexer.Definition.Name);
                return Array.Empty<ReleaseInfo>();
            }
        }

        private static string GetCacheKey(int indexerId, string guid)
        {
            return $"{indexerId}_{guid}";
        }
    }
}
