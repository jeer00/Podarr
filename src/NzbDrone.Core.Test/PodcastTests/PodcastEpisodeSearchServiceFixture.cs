using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Tracker;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastEpisodeSearchServiceFixture : CoreTest<PodcastEpisodeSearchService>
    {
        private Podcast _podcast;
        private List<PodcastEpisode> _episodes;
        private List<PodcastRelease> _releases;
        private List<PodcastDownload> _downloads;

        [SetUp]
        public void Setup()
        {
            _podcast = new Podcast { Id = 1, Title = "Chapo Trap House", Monitored = true, EpisodeSource = PodcastEpisodeSource.FeedAndTracker };

            var recent = DateTime.UtcNow.Date.AddDays(-2);

            _episodes = new List<PodcastEpisode>
            {
                new() { Id = 1, PodcastId = 1, Title = "The Big One (Preview)", AirDateUtc = recent, Monitored = true, FileSource = PodcastFileSource.Feed, RelativePath = "preview.mp3", Size = 5_000_000 },
                new() { Id = 2, PodcastId = 1, Title = "Already Full", AirDateUtc = recent.AddDays(-7), Monitored = true, FileSource = PodcastFileSource.Tracker, RelativePath = "full.mp3" },
                new() { Id = 3, PodcastId = 1, Title = "Not Monitored", AirDateUtc = recent.AddDays(-14), Monitored = false },
                new() { Id = 4, PodcastId = 1, Title = "Old Episode Preview", AirDateUtc = DateTime.UtcNow.AddDays(-120), Monitored = true }
            };

            _releases = new List<PodcastRelease>
            {
                Release("full-1", $"Chapo Trap House {recent:yyyy-MM-dd} The Big One MP3"),
                Release("full-2", $"Chapo Trap House {recent.AddDays(-7):yyyy-MM-dd} Already Full MP3"),
                Release("full-4", "Chapo Trap House - Old Episode [MP3]"),
                Release("other", "Chapo Trap House 2015-2020 Complete Collection")
            };

            _downloads = new List<PodcastDownload>();

            Mocker.GetMock<IPodcastEpisodeService>()
                .Setup(s => s.GetEpisodesByPodcast(1))
                .Returns(() => _episodes);

            Mocker.GetMock<IPodcastReleaseService>()
                .Setup(s => s.Search(_podcast, null, false))
                .Returns(() => _releases);

            Mocker.GetMock<IPodcastDownloadRepository>()
                .Setup(s => s.GetByPodcastId(1))
                .Returns(() => _downloads);
        }

        private static PodcastRelease Release(string guid, string title, long size = 60_000_000)
        {
            return new PodcastRelease
            {
                Release = new TorrentInfo { Guid = guid, Title = title, Size = size, Seeders = 5, PublishDate = DateTime.UtcNow },
                MatchesPodcast = true
            };
        }

        private void VerifyGrabbed(int episodeId, string guid)
        {
            Mocker.GetMock<IPodcastReleaseService>()
                .Verify(v => v.Grab(_podcast, It.Is<PodcastEpisode>(e => e.Id == episodeId), It.Is<ReleaseInfo>(r => r.Guid == guid)), Times.Once());
        }

        private int GrabCount()
        {
            return Mocker.GetMock<IPodcastReleaseService>().Invocations.Count(i => i.Method.Name == nameof(IPodcastReleaseService.Grab));
        }

        [Test]
        public void should_grab_full_release_for_recent_preview_episode()
        {
            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(1);

            VerifyGrabbed(1, "full-1");
            GrabCount().Should().Be(1);
        }

        [Test]
        public void should_include_old_episodes_when_searching_manually()
        {
            Subject.SearchWantedEpisodes(_podcast, false).Should().Be(2);

            VerifyGrabbed(1, "full-1");
            VerifyGrabbed(4, "full-4");
        }

        [Test]
        public void should_not_search_episode_with_active_download()
        {
            _downloads.Add(new PodcastDownload { PodcastEpisodeId = 1, Status = PodcastDownloadStatus.Downloading });

            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(0);

            Mocker.GetMock<IPodcastReleaseService>()
                .Verify(v => v.Search(It.IsAny<Podcast>(), It.IsAny<PodcastEpisode>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_skip_release_that_failed_before()
        {
            _downloads.Add(new PodcastDownload { PodcastEpisodeId = 1, Status = PodcastDownloadStatus.Failed, ReleaseGuid = "full-1" });

            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(0);
        }

        [Test]
        public void should_not_grab_releases_that_only_match_the_podcast_name()
        {
            _releases.RemoveAll(r => r.Release.Guid == "full-1");

            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(0);
        }

        [TestCase(1_000_000)]
        [TestCase(5_000_000)]
        [TestCase(60_000_000)]
        public void should_prefer_tracker_release_whatever_its_size(long size)
        {
            _releases[0].Release.Size = size;

            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(1);
        }

        [Test]
        public void should_grab_release_that_only_has_the_episode_number_from_the_feed_title()
        {
            _podcast.Title = "God Ton™ - Gratis";
            _episodes[0].Title = "#410 - Nyliberal detox och moderat partikultur";
            _releases = new List<PodcastRelease>
            {
                Release("e409", "God_Ton-E409-PODCAST-PRiVATE-SE-WEB-DIF"),
                Release("e410", "God_Ton-E410-PODCAST-PRiVATE-SE-WEB-DIF")
            };

            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(1);

            VerifyGrabbed(1, "e410");
        }

        [Test]
        public void should_not_use_the_same_release_for_two_episodes()
        {
            _episodes.Add(new PodcastEpisode { Id = 5, PodcastId = 1, Title = "The Big One (Preview)", AirDateUtc = _episodes[0].AirDateUtc, Monitored = true });

            Subject.SearchWantedEpisodes(_podcast, true).Should().Be(1);
        }
    }
}
