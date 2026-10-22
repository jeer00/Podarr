using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Podcasts.Commands
{
    public class DownloadPodcastEpisodesCommand : Command
    {
        public int? PodcastId { get; set; }
        public List<int> EpisodeIds { get; set; }

        public DownloadPodcastEpisodesCommand()
        {
            EpisodeIds = new List<int>();
        }

        public override bool SendUpdatesToClient => true;

        public override bool UpdateScheduledTask => !PodcastId.HasValue && EpisodeIds.Empty();

        public override bool RequiresDiskAccess => true;

        public override bool IsLongRunning => true;

        public override string CompletionMessage => "Completed";
    }
}
