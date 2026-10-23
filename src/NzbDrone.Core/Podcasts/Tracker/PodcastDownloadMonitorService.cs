using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Podcasts.Events;
using NzbDrone.Core.Podcasts.Tracker.Commands;
using NzbDrone.Core.Podcasts.Tracker.Events;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public interface IPodcastDownloadService
    {
        List<PodcastDownload> GetDownloads(int podcastId);
        void Remove(int id, bool removeFromClient);
        PodcastDownload Reimport(int id);
    }

    public class PodcastDownloadMonitorService : IPodcastDownloadService,
                                                 IExecute<ProcessPodcastDownloadsCommand>,
                                                 IHandle<PodcastDeletedEvent>
    {
        // Downloads that disappear from the client before completing are given some slack,
        // clients don't always list a torrent straight away after it was added.
        private static readonly TimeSpan MissingGracePeriod = TimeSpan.FromMinutes(10);

        private readonly IPodcastDownloadRepository _downloadRepository;
        private readonly IPodcastService _podcastService;
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IPodcastTrackerImportService _importService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public PodcastDownloadMonitorService(IPodcastDownloadRepository downloadRepository,
                                             IPodcastService podcastService,
                                             IProvideDownloadClient downloadClientProvider,
                                             IPodcastTrackerImportService importService,
                                             IEventAggregator eventAggregator,
                                             IManageCommandQueue commandQueueManager,
                                             Logger logger)
        {
            _commandQueueManager = commandQueueManager;
            _downloadRepository = downloadRepository;
            _podcastService = podcastService;
            _downloadClientProvider = downloadClientProvider;
            _importService = importService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<PodcastDownload> GetDownloads(int podcastId)
        {
            return _downloadRepository.GetByPodcastId(podcastId)
                .OrderByDescending(d => d.Added)
                .ToList();
        }

        public void Remove(int id, bool removeFromClient)
        {
            var download = _downloadRepository.Get(id);

            if (removeFromClient && download.IsActive)
            {
                var client = GetClient(download.DownloadClientId);
                var item = client?.GetItems().FirstOrDefault(i => IsSameDownload(i, download));

                if (item != null)
                {
                    client.RemoveItem(item, true);
                }
            }

            _downloadRepository.Delete(id);
            _eventAggregator.PublishEvent(new PodcastDownloadsUpdatedEvent(download.PodcastId));
        }

        // Imports the files of a finished (or failed) download again, they're still in the download client.
        public PodcastDownload Reimport(int id)
        {
            var download = _downloadRepository.Get(id);

            download.Status = PodcastDownloadStatus.Queued;
            download.Progress = 0;
            download.Message = "Waiting to import again";
            download.Completed = null;
            download.Added = DateTime.UtcNow;

            _downloadRepository.Update(download);
            _eventAggregator.PublishEvent(new PodcastDownloadsUpdatedEvent(download.PodcastId));
            _commandQueueManager.Push(new ProcessPodcastDownloadsCommand());

            return download;
        }

        public void Execute(ProcessPodcastDownloadsCommand message)
        {
            var active = _downloadRepository.GetActive();

            if (active.Empty())
            {
                return;
            }

            var changedPodcasts = new HashSet<int>();

            foreach (var group in active.GroupBy(d => d.DownloadClientId))
            {
                var client = GetClient(group.Key);

                if (client == null)
                {
                    foreach (var download in group)
                    {
                        Fail(download, "Download client is no longer available");
                        changedPodcasts.Add(download.PodcastId);
                    }

                    continue;
                }

                List<DownloadClientItem> items;

                try
                {
                    items = client.GetItems().ToList();
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unable to get downloads from {0}", client.Definition.Name);
                    continue;
                }

                foreach (var download in group)
                {
                    if (Process(client, download, items.FirstOrDefault(i => IsSameDownload(i, download))))
                    {
                        changedPodcasts.Add(download.PodcastId);
                    }
                }
            }

            foreach (var podcastId in changedPodcasts)
            {
                _eventAggregator.PublishEvent(new PodcastDownloadsUpdatedEvent(podcastId));
            }
        }

        private bool Process(IDownloadClient client, PodcastDownload download, DownloadClientItem item)
        {
            if (item == null)
            {
                if (DateTime.UtcNow - download.Added > MissingGracePeriod)
                {
                    Fail(download, "Removed from download client before it finished");
                    return true;
                }

                return false;
            }

            var previousStatus = download.Status;
            var previousProgress = download.Progress;

            download.Progress = item.TotalSize > 0
                ? Math.Round((item.TotalSize - item.RemainingSize) / (double)item.TotalSize * 100, 1)
                : 0;

            switch (item.Status)
            {
                case DownloadItemStatus.Failed:
                    Fail(download, item.Message.IsNotNullOrWhiteSpace() ? item.Message : "Download failed in download client");
                    return true;

                case DownloadItemStatus.Completed:
                    Import(client, download, item);
                    return true;

                case DownloadItemStatus.Downloading:
                    download.Status = PodcastDownloadStatus.Downloading;
                    download.Message = item.Message;
                    break;

                default:
                    download.Message = item.Message;
                    break;
            }

            if (download.Status != previousStatus || Math.Abs(download.Progress - previousProgress) >= 0.1)
            {
                _downloadRepository.Update(download);
                return true;
            }

            return false;
        }

        private void Import(IDownloadClient client, PodcastDownload download, DownloadClientItem item)
        {
            if (item.OutputPath.IsEmpty)
            {
                download.Message = "Waiting for the download client to report where the files are";
                _downloadRepository.Update(download);
                return;
            }

            try
            {
                var podcast = _podcastService.GetPodcast(download.PodcastId);
                var imported = _importService.Import(podcast, download, item.OutputPath.FullPath);

                download.Status = PodcastDownloadStatus.Imported;
                download.Progress = 100;
                download.Completed = DateTime.UtcNow;
                download.Message = imported == 1 ? "Imported 1 file" : $"Imported {imported} files";
                _downloadRepository.Update(download);

                try
                {
                    client.MarkItemAsImported(item);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to mark {0} as imported in {1}", item.Title, client.Definition.Name);
                }
            }
            catch (ModelNotFoundException)
            {
                _downloadRepository.Delete(download);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to import {0}", item.Title);
                Fail(download, ex.Message);
            }
        }

        private void Fail(PodcastDownload download, string message)
        {
            _logger.Warn("Podcast download '{0}' failed: {1}", download.Title, message);

            download.Status = PodcastDownloadStatus.Failed;
            download.Message = message;
            download.Completed = DateTime.UtcNow;
            _downloadRepository.Update(download);
        }

        private IDownloadClient GetClient(int downloadClientId)
        {
            return _downloadClientProvider.GetDownloadClients().FirstOrDefault(c => c.Definition.Id == downloadClientId);
        }

        private static bool IsSameDownload(DownloadClientItem item, PodcastDownload download)
        {
            return download.DownloadId.IsNotNullOrWhiteSpace() &&
                   string.Equals(item.DownloadId, download.DownloadId, StringComparison.OrdinalIgnoreCase);
        }

        public void Handle(PodcastDeletedEvent message)
        {
            foreach (var podcast in message.Podcasts)
            {
                _downloadRepository.DeleteByPodcastId(podcast.Id);
            }
        }
    }
}
