namespace NzbDrone.Core.Podcasts
{
    // Where a podcast's episode audio comes from.
    public enum PodcastEpisodeSource
    {
        // Only download from the podcast's feed.
        Feed = 0,

        // The default: download from the feed straight away, and replace it with the tracker release
        // of the episode once one is found, the feed often only has a preview.
        FeedAndTracker = 1,

        // The feed is only used as the list of episodes, audio comes from trackers.
        Tracker = 2
    }

    public enum PodcastFileSource
    {
        None = 0,
        Feed = 1,
        Tracker = 2
    }
}
