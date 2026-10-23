using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Podcasts.Tracker.Commands
{
    public class ProcessPodcastDownloadsCommand : Command
    {
        public override bool RequiresDiskAccess => true;
    }
}
