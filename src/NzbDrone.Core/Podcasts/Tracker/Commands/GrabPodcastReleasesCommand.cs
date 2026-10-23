using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Podcasts.Tracker.Commands
{
    public class GrabPodcastReleasesCommand : Command
    {
        public List<int> PodcastIds { get; set; } = new();

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => PodcastIds.Empty();

        public override string CompletionMessage => "Completed";
    }
}
