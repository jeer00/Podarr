using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public class PodcastDownload : ModelBase
    {
        public int PodcastId { get; set; }
        public int? PodcastEpisodeId { get; set; }
        public int DownloadClientId { get; set; }
        public string DownloadId { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public int IndexerId { get; set; }
        public string Indexer { get; set; }
        public string ReleaseGuid { get; set; }
        public string Title { get; set; }
        public long Size { get; set; }
        public PodcastDownloadStatus Status { get; set; }
        public double Progress { get; set; }
        public string Message { get; set; }
        public DateTime Added { get; set; }
        public DateTime? Completed { get; set; }

        public bool IsActive => Status is PodcastDownloadStatus.Queued or PodcastDownloadStatus.Downloading;
    }

    public enum PodcastDownloadStatus
    {
        Queued = 0,
        Downloading = 1,
        Imported = 2,
        Failed = 3
    }
}
