using System;
using System.Globalization;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Podcasts
{
    public class PodcastStatistics : ResultSet
    {
        public int PodcastId { get; set; }
        public int EpisodeCount { get; set; }
        public int MonitoredEpisodeCount { get; set; }
        public int DownloadedEpisodeCount { get; set; }
        public long SizeOnDisk { get; set; }
        public string LastAiredString { get; set; }

        public DateTime? LastAired
        {
            get
            {
                if (DateTime.TryParse(LastAiredString, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lastAired))
                {
                    return lastAired;
                }

                return null;
            }
        }
    }
}
