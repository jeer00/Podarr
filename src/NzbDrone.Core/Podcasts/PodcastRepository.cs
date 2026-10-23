using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastRepository : IBasicRepository<Podcast>
    {
        Podcast FindByFeedUrl(string feedUrl);
        List<string> AllPodcastPaths();
    }

    public class PodcastRepository : BasicRepository<Podcast>, IPodcastRepository
    {
        public PodcastRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Podcast FindByFeedUrl(string feedUrl)
        {
            return Query(p => p.FeedUrl == feedUrl).SingleOrDefault();
        }

        public List<string> AllPodcastPaths()
        {
            return All().Select(p => p.Path).ToList();
        }
    }
}
