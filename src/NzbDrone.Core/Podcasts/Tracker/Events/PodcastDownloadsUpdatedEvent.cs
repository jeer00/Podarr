using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Podcasts.Tracker.Events
{
    public class PodcastDownloadsUpdatedEvent : IEvent
    {
        public int PodcastId { get; }

        public PodcastDownloadsUpdatedEvent(int podcastId)
        {
            PodcastId = podcastId;
        }
    }
}
