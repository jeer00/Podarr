using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Podcasts.Events
{
    public class PodcastAddedEvent : IEvent
    {
        public Podcast Podcast { get; }

        public PodcastAddedEvent(Podcast podcast)
        {
            Podcast = podcast;
        }
    }

    public class PodcastUpdatedEvent : IEvent
    {
        public Podcast Podcast { get; }

        public PodcastUpdatedEvent(Podcast podcast)
        {
            Podcast = podcast;
        }
    }

    public class PodcastDeletedEvent : IEvent
    {
        public List<Podcast> Podcasts { get; }
        public bool DeleteFiles { get; }

        public PodcastDeletedEvent(List<Podcast> podcasts, bool deleteFiles)
        {
            Podcasts = podcasts;
            DeleteFiles = deleteFiles;
        }
    }

    public class PodcastEpisodesUpdatedEvent : IEvent
    {
        public int PodcastId { get; }
        public List<PodcastEpisode> Episodes { get; }

        public PodcastEpisodesUpdatedEvent(int podcastId, List<PodcastEpisode> episodes)
        {
            PodcastId = podcastId;
            Episodes = episodes;
        }
    }

    public class PodcastEpisodeDownloadedEvent : IEvent
    {
        public Podcast Podcast { get; }
        public PodcastEpisode Episode { get; }

        public PodcastEpisodeDownloadedEvent(Podcast podcast, PodcastEpisode episode)
        {
            Podcast = podcast;
            Episode = episode;
        }
    }
}
