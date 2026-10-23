using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Podcasts.Commands
{
    public class RefreshPodcastCommand : Command
    {
        public List<int> PodcastIds { get; set; }

        public RefreshPodcastCommand()
        {
            PodcastIds = new List<int>();
        }

        public RefreshPodcastCommand(List<int> podcastIds)
        {
            PodcastIds = podcastIds;
        }

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => PodcastIds.Empty();

        public override bool IsLongRunning => true;

        public override string CompletionMessage => "Completed";
    }
}
