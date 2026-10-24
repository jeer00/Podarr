using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Podcasts.Tracker
{
    public interface IPodcastDownloadRepository : IBasicRepository<PodcastDownload>
    {
        List<PodcastDownload> GetByPodcastId(int podcastId);
        List<PodcastDownload> GetActive();
        void DeleteByPodcastId(int podcastId);
    }

    public class PodcastDownloadRepository : BasicRepository<PodcastDownload>, IPodcastDownloadRepository
    {
        public PodcastDownloadRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<PodcastDownload> GetByPodcastId(int podcastId)
        {
            return Query(d => d.PodcastId == podcastId);
        }

        public List<PodcastDownload> GetActive()
        {
            return Query(d => d.Status == PodcastDownloadStatus.Queued || d.Status == PodcastDownloadStatus.Downloading);
        }

        public void DeleteByPodcastId(int podcastId)
        {
            Delete(d => d.PodcastId == podcastId);
        }
    }
}
