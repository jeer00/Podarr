using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(240)]
    public class add_podcast_episode_source : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Podcasts").AddColumn("EpisodeSource").AsInt32().WithDefaultValue(1);
            Alter.Table("PodcastEpisodes").AddColumn("FileSource").AsInt32().WithDefaultValue(0);

            // Tracker imports carry the tracker download's id in the downloads table, everything else came from the feed.
            Execute.Sql(@"UPDATE ""PodcastEpisodes"" SET ""FileSource"" = 1 WHERE ""RelativePath"" IS NOT NULL");
            Execute.Sql(@"UPDATE ""PodcastEpisodes"" SET ""FileSource"" = 2 WHERE ""RelativePath"" IS NOT NULL AND ""Id"" IN (SELECT ""PodcastEpisodeId"" FROM ""PodcastDownloads"" WHERE ""Status"" = 2 AND ""PodcastEpisodeId"" IS NOT NULL)");
        }
    }
}
