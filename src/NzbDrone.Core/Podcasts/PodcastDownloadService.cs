using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Podcasts.Commands;
using NzbDrone.Core.Podcasts.Events;

namespace NzbDrone.Core.Podcasts
{
    public class PodcastDownloadService : IExecute<DownloadPodcastEpisodesCommand>
    {
        private const int MaxTitleLength = 150;

        private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".m4a", ".m4b", ".mp4", ".m4v", ".aac", ".ogg", ".oga", ".opus", ".flac", ".wav", ".webm", ".mov", ".mkv"
        };

        private static readonly Dictionary<string, string> MimeTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "audio/mpeg", ".mp3" },
            { "audio/mp3", ".mp3" },
            { "audio/x-m4a", ".m4a" },
            { "audio/m4a", ".m4a" },
            { "audio/mp4", ".m4a" },
            { "audio/aac", ".aac" },
            { "audio/ogg", ".ogg" },
            { "audio/opus", ".opus" },
            { "audio/flac", ".flac" },
            { "audio/wav", ".wav" },
            { "audio/x-wav", ".wav" },
            { "video/mp4", ".mp4" },
            { "video/x-m4v", ".m4v" },
            { "video/quicktime", ".mov" },
            { "video/webm", ".webm" }
        };

        private readonly IPodcastService _podcastService;
        private readonly IPodcastEpisodeService _episodeService;
        private readonly IHttpClient _httpClient;
        private readonly IDiskProvider _diskProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public PodcastDownloadService(IPodcastService podcastService,
                                      IPodcastEpisodeService episodeService,
                                      IHttpClient httpClient,
                                      IDiskProvider diskProvider,
                                      IEventAggregator eventAggregator,
                                      Logger logger)
        {
            _podcastService = podcastService;
            _episodeService = episodeService;
            _httpClient = httpClient;
            _diskProvider = diskProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Execute(DownloadPodcastEpisodesCommand message)
        {
            List<PodcastEpisode> episodes;

            if (message.EpisodeIds.Any())
            {
                // Explicitly requested episodes are downloaded even if they aren't monitored yet.
                episodes = _episodeService.GetEpisodes(message.EpisodeIds)
                                          .Where(e => !e.HasFile && e.EnclosureUrl.IsNotNullOrWhiteSpace())
                                          .ToList();
            }
            else
            {
                var now = DateTime.UtcNow;

                episodes = _episodeService.GetEpisodesToDownload(message.PodcastId)
                                          .Where(e => !e.AirDateUtc.HasValue || e.AirDateUtc <= now)
                                          .ToList();
            }

            if (episodes.Empty())
            {
                _logger.ProgressInfo("No podcast episodes to download");
                return;
            }

            var podcasts = _podcastService.GetPodcasts(episodes.Select(e => e.PodcastId).Distinct())
                                          .ToDictionary(p => p.Id);

            if (message.EpisodeIds.Empty())
            {
                // The feed of a tracker-only podcast is just the list of episodes (eg. previews), don't download it.
                episodes = episodes.Where(e => podcasts.TryGetValue(e.PodcastId, out var p) && p.EpisodeSource != PodcastEpisodeSource.Tracker).ToList();

                if (episodes.Empty())
                {
                    _logger.ProgressInfo("No podcast episodes to download");
                    return;
                }
            }

            var ordered = episodes.OrderByDescending(e => e.AirDateUtc ?? DateTime.MinValue).ToList();
            var downloaded = 0;

            for (var i = 0; i < ordered.Count; i++)
            {
                var episode = ordered[i];

                if (!podcasts.TryGetValue(episode.PodcastId, out var podcast))
                {
                    continue;
                }

                _logger.ProgressInfo("Downloading {0} - {1} ({2}/{3})", podcast.Title, episode.Title, i + 1, ordered.Count);

                if (DownloadEpisode(podcast, episode, message.EpisodeIds.Any()))
                {
                    downloaded++;
                }
            }

            _logger.ProgressInfo("Downloaded {0} of {1} podcast episode(s)", downloaded, ordered.Count);
        }

        private bool DownloadEpisode(Podcast podcast, PodcastEpisode episode, bool monitor)
        {
            var rootFolder = Path.GetDirectoryName(podcast.Path);

            if (rootFolder.IsNotNullOrWhiteSpace() && !_diskProvider.FolderExists(rootFolder))
            {
                _logger.Warn("Root folder {0} for podcast {1} doesn't exist, skipping download", rootFolder, podcast.Title);
                return false;
            }

            var relativePath = BuildRelativePath(podcast, episode);
            var fullPath = Path.Combine(podcast.Path, relativePath);
            var partPath = fullPath + ".partial";

            try
            {
                _diskProvider.EnsureFolder(podcast.Path);

                using (var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write))
                {
                    var request = new HttpRequest(episode.EnclosureUrl)
                    {
                        AllowAutoRedirect = true,
                        ResponseStream = fileStream,
                        RequestTimeout = TimeSpan.FromHours(2)
                    };

                    var response = _httpClient.Get(request);

                    if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
                    {
                        throw new HttpException(request, response, "Server responded with an html page instead of media");
                    }
                }

                _diskProvider.MoveFile(partPath, fullPath, true);

                episode.RelativePath = relativePath;
                episode.FileSource = PodcastFileSource.Feed;
                episode.Size = _diskProvider.GetFileSize(fullPath);
                episode.DateDownloaded = DateTime.UtcNow;
                episode.LastDownloadError = null;

                if (monitor)
                {
                    episode.Monitored = true;
                }

                _episodeService.UpdateEpisode(episode);
                _eventAggregator.PublishEvent(new PodcastEpisodeDownloadedEvent(podcast, episode));

                _logger.Info("Downloaded {0} - {1} to {2}", podcast.Title, episode.Title, fullPath);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to download {0} - {1}", podcast.Title, episode.Title);

                if (_diskProvider.FileExists(partPath))
                {
                    _diskProvider.DeleteFile(partPath);
                }

                episode.LastDownloadError = ex.Message;
                _episodeService.UpdateEpisode(episode);

                return false;
            }
        }

        private string BuildRelativePath(Podcast podcast, PodcastEpisode episode)
        {
            var name = BuildFileName(episode);
            var extension = GetExtension(episode);
            var relativePath = name + extension;

            // Two episodes with the same title on the same day would otherwise overwrite each other.
            if (_diskProvider.FileExists(Path.Combine(podcast.Path, relativePath)))
            {
                relativePath = $"{name} ({episode.Id}){extension}";
            }

            return relativePath;
        }

        public static bool IsMediaFile(string path)
        {
            return KnownExtensions.Contains(Path.GetExtension(path));
        }

        public static string BuildFileName(PodcastEpisode episode)
        {
            var title = episode.Title ?? "Episode";

            if (title.Length > MaxTitleLength)
            {
                title = title.Substring(0, MaxTitleLength).Trim();
            }

            var name = episode.AirDateUtc.HasValue
                ? $"{episode.AirDateUtc.Value:yyyy-MM-dd} - {title}"
                : title;

            return FileNameBuilder.CleanFileName(name);
        }

        public static string GetExtension(PodcastEpisode episode)
        {
            if (Uri.TryCreate(episode.EnclosureUrl, UriKind.Absolute, out var uri))
            {
                var extension = Path.GetExtension(uri.AbsolutePath);

                if (KnownExtensions.Contains(extension))
                {
                    return extension.ToLowerInvariant();
                }
            }

            if (episode.EnclosureType.IsNotNullOrWhiteSpace() && MimeTypeExtensions.TryGetValue(episode.EnclosureType.Split(';')[0].Trim(), out var mimeExtension))
            {
                return mimeExtension;
            }

            return ".mp3";
        }
    }
}
