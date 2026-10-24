using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Podcasts.Feed
{
    public class PublishPodcastFeedsCommand : Command
    {
        public List<int> PodcastIds { get; set; } = new();

        // Upload even if the feed didn't change since it was last uploaded, eg. after changing the bucket.
        public bool Force { get; set; }

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => false;

        public override string CompletionMessage => "Completed";
    }
}
