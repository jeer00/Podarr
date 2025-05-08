using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Extensions;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastEpisodeRepository : IBasicRepository<PodcastEpisode>
    {
        List<PodcastEpisode> GetByPodcastId(int podcastId);
    }

    public class PodcastEpisodeRepository : BasicRepository<PodcastEpisode>, IPodcastEpisodeRepository
    {
        public PodcastEpisodeRepository(IMainDatabase database)
            : base(database)
        {
        }

        public List<PodcastEpisode> GetByPodcastId(int podcastId)
        {
            return Query(e => e.PodcastId == podcastId);
        }
    }
}
