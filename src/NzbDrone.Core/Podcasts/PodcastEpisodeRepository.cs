using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastEpisodeRepository : IBasicRepository<PodcastEpisode>
    {
        List<PodcastEpisode> GetByPodcastId(int podcastId);
        List<PodcastEpisode> GetEpisodesToDownload(int? podcastId);
        void DeleteByPodcastIds(List<int> podcastIds);
        List<PodcastStatistics> GetStatistics(int? podcastId = null);
    }

    public class PodcastEpisodeRepository : BasicRepository<PodcastEpisode>, IPodcastEpisodeRepository
    {
        public PodcastEpisodeRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<PodcastEpisode> GetByPodcastId(int podcastId)
        {
            return Query(e => e.PodcastId == podcastId);
        }

        public List<PodcastEpisode> GetEpisodesToDownload(int? podcastId)
        {
            var builder = Builder()
                .Join<PodcastEpisode, Podcast>((e, p) => e.PodcastId == p.Id)
                .Where<PodcastEpisode>(e => e.Monitored == true && e.RelativePath == null && e.EnclosureUrl != null)
                .Where<Podcast>(p => p.Monitored == true);

            if (podcastId.HasValue)
            {
                builder = builder.Where<PodcastEpisode>(e => e.PodcastId == podcastId.Value);
            }

            return Query(builder);
        }

        public void DeleteByPodcastIds(List<int> podcastIds)
        {
            Delete(e => podcastIds.Contains(e.PodcastId));
        }

        public List<PodcastStatistics> GetStatistics(int? podcastId = null)
        {
            var trueIndicator = _database.DatabaseType == DatabaseType.PostgreSQL ? "true" : "1";

            var sql = $@"SELECT ""PodcastId"",
                                COUNT(*) AS EpisodeCount,
                                SUM(CASE WHEN ""Monitored"" = {trueIndicator} THEN 1 ELSE 0 END) AS MonitoredEpisodeCount,
                                SUM(CASE WHEN ""RelativePath"" IS NOT NULL THEN 1 ELSE 0 END) AS DownloadedEpisodeCount,
                                SUM(CASE WHEN ""RelativePath"" IS NOT NULL THEN ""Size"" ELSE 0 END) AS SizeOnDisk,
                                MAX(""AirDateUtc"") AS LastAiredString
                         FROM ""PodcastEpisodes""
                         {(podcastId.HasValue ? @"WHERE ""PodcastId"" = @PodcastId" : string.Empty)}
                         GROUP BY ""PodcastId""";

            using (var conn = _database.OpenConnection())
            {
                return conn.Query<PodcastStatistics>(sql, new { PodcastId = podcastId ?? 0 }).ToList();
            }
        }
    }
}
