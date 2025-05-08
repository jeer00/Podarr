using System.Collections.Generic;
using System.Threading.Tasks;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastRssParser
    {
        Task<List<PodcastEpisode>> ParseFeed(string rssUrl, Podcast podcast);
    }
}
