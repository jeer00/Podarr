using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts.Events;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public interface IPodcastTrackerImportService
    {
        int Import(Podcast podcast, PodcastDownload download, string outputPath);
    }

    public class PodcastTrackerImportService : IPodcastTrackerImportService
    {
        private readonly IPodcastEpisodeRepository _episodeRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public PodcastTrackerImportService(IPodcastEpisodeRepository episodeRepository,
                                           IDiskProvider diskProvider,
                                           IDiskTransferService diskTransferService,
                                           IRecycleBinProvider recycleBinProvider,
                                           IEventAggregator eventAggregator,
                                           Logger logger)
        {
            _episodeRepository = episodeRepository;
            _diskProvider = diskProvider;
            _diskTransferService = diskTransferService;
            _recycleBinProvider = recycleBinProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public int Import(Podcast podcast, PodcastDownload download, string outputPath)
        {
            var files = GetMediaFiles(outputPath);

            if (files.Empty())
            {
                throw new PodcastImportException("No audio or video files found in {0}", outputPath);
            }

            var term = podcast.GetTrackerSearchTerm();
            var episodes = _episodeRepository.GetByPodcastId(podcast.Id);
            var targetEpisode = download.PodcastEpisodeId.HasValue
                ? episodes.FirstOrDefault(e => e.Id == download.PodcastEpisodeId.Value)
                : null;

            var updated = new List<PodcastEpisode>();
            var assigned = new HashSet<int>();

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var size = _diskProvider.GetFileSize(file);

                var unassigned = episodes.Where(e => !assigned.Contains(e.Id)).ToList();

                // The file's own name decides which episode it is. The episode it was grabbed for is only used for
                // a single file nothing else matches, a search started from one episode can grab others too.
                var episode = PodcastReleaseMatcher.FindBestEpisode(term, unassigned, fileName)
                              ?? PodcastReleaseMatcher.FindBestEpisode(term, unassigned, $"{download.Title} {fileName}")
                              ?? (files.Count == 1 ? targetEpisode : null);

                if (episode == null)
                {
                    var created = CreateEpisodeFromFile(podcast, fileName);

                    // Importing the same file again (eg. a re-grab) should reuse the episode created the first time.
                    episode = episodes.FirstOrDefault(e => e.FeedGuid == created.FeedGuid);

                    if (episode == null)
                    {
                        episode = created;
                        episodes.Add(episode);
                    }
                }
                else if (episode.HasFile)
                {
                    // The tracker release is the full episode where the feed only has a preview,
                    // so it always replaces a feed file. Between tracker releases the larger one wins.
                    if (episode.FileSource == PodcastFileSource.Tracker && size <= episode.Size)
                    {
                        _logger.Debug("Skipping {0}, episode '{1}' already has an equal or larger file", file, episode.Title);
                        assigned.Add(episode.Id);
                        continue;
                    }

                    var existingPath = PodcastService.GetEpisodePath(podcast, episode);

                    if (_diskProvider.FileExists(existingPath))
                    {
                        _recycleBinProvider.DeleteFile(existingPath);
                    }
                }

                var relativePath = BuildRelativePath(podcast, episode, Path.GetExtension(file));
                var destination = Path.Combine(podcast.Path, relativePath);

                _diskProvider.EnsureFolder(podcast.Path);
                _diskTransferService.TransferFile(file, destination, TransferMode.HardLinkOrCopy);

                episode.RelativePath = relativePath;
                episode.FileSource = PodcastFileSource.Tracker;
                episode.Size = size;
                episode.DateDownloaded = DateTime.UtcNow;
                episode.LastDownloadError = null;
                episode.Monitored = true;

                if (episode.Id == 0)
                {
                    _episodeRepository.Insert(episode);
                }
                else
                {
                    _episodeRepository.Update(episode);
                }

                assigned.Add(episode.Id);
                updated.Add(episode);

                _logger.Info("Imported {0} as '{1}'", file, episode.Title);
                _eventAggregator.PublishEvent(new PodcastEpisodeDownloadedEvent(podcast, episode));
            }

            if (updated.Any())
            {
                _eventAggregator.PublishEvent(new PodcastEpisodesUpdatedEvent(podcast.Id, updated));
            }

            return updated.Count;
        }

        private List<string> GetMediaFiles(string outputPath)
        {
            if (_diskProvider.FileExists(outputPath))
            {
                return PodcastDownloadService.IsMediaFile(outputPath) ? new List<string> { outputPath } : new List<string>();
            }

            if (!_diskProvider.FolderExists(outputPath))
            {
                throw new PodcastImportException("Download path {0} doesn't exist, check Remote Path Mappings if the download client runs on another machine", outputPath);
            }

            return _diskProvider.GetFiles(outputPath, true)
                .Where(PodcastDownloadService.IsMediaFile)
                .Where(f => !Path.GetFileName(f).StartsWith("._"))
                .OrderBy(f => f)
                .ToList();
        }

        private static PodcastEpisode CreateEpisodeFromFile(Podcast podcast, string fileName)
        {
            var term = podcast.GetTrackerSearchTerm();

            return new PodcastEpisode
            {
                PodcastId = podcast.Id,
                FeedGuid = $"tracker:{PodcastReleaseMatcher.Normalize(fileName)}",
                Title = PodcastReleaseMatcher.GetTitleFromFileName(term, fileName),
                AirDateUtc = PodcastReleaseMatcher.ParseDate(fileName),
                EpisodeNumber = PodcastReleaseMatcher.ParseEpisodeNumber(fileName),
                Added = DateTime.UtcNow,
                Monitored = true
            };
        }

        private string BuildRelativePath(Podcast podcast, PodcastEpisode episode, string extension)
        {
            var name = PodcastDownloadService.BuildFileName(episode);
            var relativePath = name + extension.ToLowerInvariant();

            if (_diskProvider.FileExists(Path.Combine(podcast.Path, relativePath)))
            {
                relativePath = $"{name} ({Guid.NewGuid().ToString("N").Substring(0, 6)}){extension.ToLowerInvariant()}";
            }

            return relativePath;
        }
    }

    public class PodcastImportException : NzbDrone.Common.Exceptions.NzbDroneException
    {
        public PodcastImportException(string message, params object[] args)
            : base(message, args)
        {
        }
    }
}
