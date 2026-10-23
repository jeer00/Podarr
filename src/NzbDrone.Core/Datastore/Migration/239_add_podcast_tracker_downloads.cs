using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(239)]
    public class add_podcast_tracker_downloads : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Podcasts")
                .AddColumn("TrackerSearchTerm").AsString().Nullable()
                .AddColumn("GrabTrackerReleases").AsBoolean().WithDefaultValue(false)
                .AddColumn("GrabTrackerReleasesSince").AsDateTimeOffset().Nullable();

            Create.TableForModel("PodcastDownloads")
                .WithColumn("PodcastId").AsInt32().Indexed()
                .WithColumn("PodcastEpisodeId").AsInt32().Nullable()
                .WithColumn("DownloadClientId").AsInt32()
                .WithColumn("DownloadId").AsString().Nullable()
                .WithColumn("Protocol").AsInt32()
                .WithColumn("IndexerId").AsInt32()
                .WithColumn("Indexer").AsString().Nullable()
                .WithColumn("ReleaseGuid").AsString().Nullable()
                .WithColumn("Title").AsString()
                .WithColumn("Size").AsInt64().WithDefaultValue(0)
                .WithColumn("Status").AsInt32()
                .WithColumn("Progress").AsDouble().WithDefaultValue(0)
                .WithColumn("Message").AsString().Nullable()
                .WithColumn("Added").AsDateTimeOffset()
                .WithColumn("Completed").AsDateTimeOffset().Nullable();
        }
    }
}
