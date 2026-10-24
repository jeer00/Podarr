using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Podcasts.Tracker.Commands
{
    public class SearchPodcastEpisodesCommand : Command
    {
        public List<int> PodcastIds { get; set; } = new();

        // Automatic searches only look for recent episodes, a manual search covers every wanted episode.
        public bool Automatic { get; set; }

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => false;

        public override string CompletionMessage => "Completed";
    }
}
