using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(238)]
    public class add_podcasts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Podcasts")
                .WithColumn("Title").AsString()
                .WithColumn("SortTitle").AsString().Nullable()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("Author").AsString().Nullable()
                .WithColumn("FeedUrl").AsString().Unique()
                .WithColumn("WebsiteUrl").AsString().Nullable()
                .WithColumn("ImageUrl").AsString().Nullable()
                .WithColumn("Language").AsString().Nullable()
                .WithColumn("Categories").AsString().WithDefaultValue("[]")
                .WithColumn("Explicit").AsBoolean().WithDefaultValue(false)
                .WithColumn("ItunesId").AsInt64().Nullable()
                .WithColumn("Path").AsString().Unique()
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(true)
                .WithColumn("MonitorNewEpisodes").AsBoolean().WithDefaultValue(true)
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("LastRefreshed").AsDateTimeOffset().Nullable()
                .WithColumn("LastRefreshError").AsString().Nullable()
                .WithColumn("Tags").AsString().WithDefaultValue("[]");

            Create.TableForModel("PodcastEpisodes")
                .WithColumn("PodcastId").AsInt32().Indexed()
                .WithColumn("FeedGuid").AsString()
                .WithColumn("Title").AsString()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("AirDateUtc").AsDateTimeOffset().Nullable()
                .WithColumn("SeasonNumber").AsInt32().Nullable()
                .WithColumn("EpisodeNumber").AsInt32().Nullable()
                .WithColumn("EpisodeType").AsString().Nullable()
                .WithColumn("Duration").AsInt32().Nullable()
                .WithColumn("EnclosureUrl").AsString().Nullable()
                .WithColumn("EnclosureType").AsString().Nullable()
                .WithColumn("EnclosureLength").AsInt64().Nullable()
                .WithColumn("ImageUrl").AsString().Nullable()
                .WithColumn("Explicit").AsBoolean().WithDefaultValue(false)
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(true)
                .WithColumn("RelativePath").AsString().Nullable()
                .WithColumn("Size").AsInt64().WithDefaultValue(0)
                .WithColumn("DateDownloaded").AsDateTimeOffset().Nullable()
                .WithColumn("LastDownloadError").AsString().Nullable()
                .WithColumn("Added").AsDateTimeOffset();

            Create.Index("IX_PodcastEpisodes_PodcastId_FeedGuid")
                .OnTable("PodcastEpisodes")
                .OnColumn("PodcastId").Ascending()
                .OnColumn("FeedGuid").Ascending()
                .WithOptions().Unique();
        }
    }
}
