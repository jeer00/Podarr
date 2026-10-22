using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class RefreshPodcastServiceFixture : CoreTest<RefreshPodcastService>
    {
        private Podcast _podcast;
        private PodcastFeed _feed;
        private List<PodcastEpisode> _existing;
        private List<PodcastEpisode> _inserted;
        private List<PodcastEpisode> _updated;

        [SetUp]
        public void Setup()
        {
            _podcast = new Podcast { Id = 1, Title = "Podcast", Path = "/podcasts/Podcast".AsOsAgnostic(), MonitorNewEpisodes = true };

            _feed = new PodcastFeed
            {
                Title = "Podcast",
                Items = new List<PodcastFeedItem>
                {
                    new() { Guid = "3", Title = "Three", EnclosureUrl = "https://example.com/3.mp3", AirDateUtc = new DateTime(2024, 3, 1) },
                    new() { Guid = "2", Title = "Two", EnclosureUrl = "https://example.com/2.mp3", AirDateUtc = new DateTime(2024, 2, 1) },
                    new() { Guid = "1", Title = "One", EnclosureUrl = "https://example.com/1.mp3", AirDateUtc = new DateTime(2024, 1, 1) }
                }
            };

            _existing = new List<PodcastEpisode>();
            _inserted = new List<PodcastEpisode>();
            _updated = new List<PodcastEpisode>();

            Mocker.GetMock<IPodcastEpisodeRepository>()
                .Setup(s => s.GetByPodcastId(_podcast.Id))
                .Returns(() => _existing);

            Mocker.GetMock<IPodcastEpisodeRepository>()
                .Setup(s => s.InsertMany(It.IsAny<IList<PodcastEpisode>>()))
                .Callback<IList<PodcastEpisode>>(e => _inserted.AddRange(e));

            Mocker.GetMock<IPodcastEpisodeRepository>()
                .Setup(s => s.UpdateMany(It.IsAny<IList<PodcastEpisode>>()))
                .Callback<IList<PodcastEpisode>>(e => _updated.AddRange(e));

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileExists(It.IsAny<string>()))
                .Returns(true);
        }

        [TestCase(PodcastMonitorType.All, 3)]
        [TestCase(PodcastMonitorType.Future, 0)]
        [TestCase(PodcastMonitorType.None, 0)]
        public void should_apply_initial_monitoring(PodcastMonitorType monitor, int monitoredCount)
        {
            Subject.SyncEpisodes(_podcast, _feed, monitor);

            _inserted.Should().HaveCount(3);
            _inserted.Count(e => e.Monitored).Should().Be(monitoredCount);
        }

        [Test]
        public void should_only_monitor_latest_episode()
        {
            Subject.SyncEpisodes(_podcast, _feed, PodcastMonitorType.Latest);

            _inserted.Where(e => e.Monitored).Select(e => e.FeedGuid).Should().BeEquivalentTo(new[] { "3" });
        }

        [Test]
        public void should_only_insert_new_episodes_and_keep_existing_state()
        {
            _existing.Add(new PodcastEpisode { Id = 10, PodcastId = 1, FeedGuid = "2", Title = "Old title", Monitored = false, RelativePath = "two.mp3" });

            Subject.SyncEpisodes(_podcast, _feed);

            _inserted.Select(e => e.FeedGuid).Should().BeEquivalentTo(new[] { "3", "1" });
            _inserted.Should().OnlyContain(e => e.Monitored);

            var existing = _updated.Single();
            existing.Title.Should().Be("Two");
            existing.Monitored.Should().BeFalse();
            existing.RelativePath.Should().Be("two.mp3");
        }

        [Test]
        public void should_not_monitor_new_episodes_when_disabled()
        {
            _podcast.MonitorNewEpisodes = false;

            Subject.SyncEpisodes(_podcast, _feed);

            _inserted.Should().OnlyContain(e => !e.Monitored);
        }

        [Test]
        public void should_clear_and_unmonitor_episodes_whose_file_was_deleted()
        {
            _existing.Add(new PodcastEpisode { Id = 20, PodcastId = 1, FeedGuid = "old", Title = "Old", Monitored = true, RelativePath = "old.mp3", Size = 100 });

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.SyncEpisodes(_podcast, _feed);

            var episode = _updated.Single(e => e.Id == 20);
            episode.RelativePath.Should().BeNull();
            episode.Size.Should().Be(0);
            episode.Monitored.Should().BeFalse();
        }
    }
}
