using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(49)]
    public class AddPodcastTables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Podcasts")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("Title").AsString().NotNullable()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("RssUrl").AsString().NotNullable()
                .WithColumn("WebsiteUrl").AsString().Nullable()
                .WithColumn("ImageUrl").AsString().Nullable()
                .WithColumn("ItunesAuthor").AsString().Nullable()
                .WithColumn("ItunesCategory").AsString().Nullable()
                .WithColumn("ItunesExplicit").AsString().Nullable()
                .WithColumn("ItunesOwnerName").AsString().Nullable()
                .WithColumn("ItunesOwnerEmail").AsString().Nullable()
                .WithColumn("LastUpdated").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentUTCDateTime)
                .WithColumn("AddOptions").AsString().Nullable()
                .WithColumn("RootFolderPath").AsString().NotNullable()
                .WithColumn("QualityProfileId").AsInt32().NotNullable()
                .WithColumn("Monitored").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("Tags").AsString().Nullable()
                .WithColumn("LastRefresh").AsDateTime().Nullable()
                .WithColumn("LastRefreshFailure").AsString().Nullable();

            Create.TableForModel("PodcastEpisodes")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("PodcastId").AsInt32().NotNullable()
                .WithColumn("Title").AsString().NotNullable()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("Published").AsDateTime().Nullable()
                .WithColumn("AudioUrl").AsString().NotNullable()
                .WithColumn("AudioType").AsString().Nullable()
                .WithColumn("AudioLength").AsString().Nullable()
                .WithColumn("ItunesSubtitle").AsString().Nullable()
                .WithColumn("ItunesSummary").AsString().Nullable()
                .WithColumn("ItunesExplicit").AsString().Nullable()
                .WithColumn("ItunesDuration").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("Downloaded").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("Path").AsString().Nullable()
                .WithColumn("FileSize").AsInt64().Nullable()
                .WithColumn("DownloadId").AsString().Nullable()
                .WithColumn("Tags").AsString().Nullable();

            Create.ForeignKey()
                .FromTable("PodcastEpisodes").ForeignColumn("PodcastId")
                .ToTable("Podcasts").PrimaryColumn("Id");

            Create.Index("IX_PodcastEpisodes_PodcastId")
                .OnTable("PodcastEpisodes")
                .OnColumn("PodcastId");

            Create.Index("IX_PodcastEpisodes_Downloaded")
                .OnTable("PodcastEpisodes")
                .OnColumn("Downloaded");
        }
    }
}
