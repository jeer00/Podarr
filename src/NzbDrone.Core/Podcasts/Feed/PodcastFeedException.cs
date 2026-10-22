using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Podcasts.Feed
{
    public class PodcastFeedException : NzbDroneException
    {
        public PodcastFeedException(string message, params object[] args)
            : base(message, args)
        {
        }

        public PodcastFeedException(string message, System.Exception innerException, params object[] args)
            : base(message, innerException, args)
        {
        }
    }
}
