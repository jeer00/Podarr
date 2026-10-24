using System;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Podcasts
{
    public class PodcastEpisode : ModelBase
    {
        public int PodcastId { get; set; }
        public string FeedGuid { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? AirDateUtc { get; set; }
        public int? SeasonNumber { get; set; }
        public int? EpisodeNumber { get; set; }
        public string EpisodeType { get; set; }
        public int? Duration { get; set; }
        public string EnclosureUrl { get; set; }
        public string EnclosureType { get; set; }
        public long? EnclosureLength { get; set; }
        public string ImageUrl { get; set; }
        public bool Explicit { get; set; }
        public bool Monitored { get; set; }
        public string RelativePath { get; set; }
        public PodcastFileSource FileSource { get; set; }
        public long Size { get; set; }
        public DateTime? DateDownloaded { get; set; }
        public string LastDownloadError { get; set; }
        public DateTime Added { get; set; }

        public bool HasFile => RelativePath.IsNotNullOrWhiteSpace();

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", Id, Title.NullSafe());
        }
    }
}
