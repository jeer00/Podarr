using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Extensions;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastRepository : IBasicRepository<Podcast>
    {
    }

    public class PodcastRepository : BasicRepository<Podcast>, IPodcastRepository
    {
        public PodcastRepository(IMainDatabase database)
            : base(database)
        {
        }
    }
}
