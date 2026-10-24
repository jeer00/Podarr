using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Tracker;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastReleaseServiceFixture : CoreTest<PodcastReleaseService>
    {
        private const string Hash = "ABCDEF0123456789ABCDEF0123456789ABCDEF01";

        private Podcast _podcast;
        private PodcastEpisode _episode;
        private TorrentInfo _release;
        private Mock<IDownloadClient> _downloadClient;

        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());

            _podcast = new Podcast { Id = 12, Title = "Example Podcast", Tags = new HashSet<int>() };
            _episode = new PodcastEpisode { Id = 42, PodcastId = 12, Title = "Episode 424" };
            _release = new TorrentInfo
            {
                IndexerId = 1,
                DownloadProtocol = DownloadProtocol.Torrent,
                InfoHash = Hash,
                Guid = "episode-424",
                Title = "Example.Podcast.E424"
            };

            _downloadClient = Mocker.GetMock<IDownloadClient>();
            _downloadClient.SetupGet(c => c.Definition)
                .Returns(new DownloadClientDefinition { Id = 7, Name = "qBittorrent" });
            _downloadClient.Setup(c => c.GetItems()).Returns(Array.Empty<DownloadClientItem>());
            _downloadClient.Setup(c => c.Download(It.IsAny<RemoteEpisode>(), It.IsAny<IIndexer>()))
                .ThrowsAsync(new DownloadClientException("Download client failed to add torrent"));

            Mocker.GetMock<IProvideDownloadClient>()
                .Setup(c => c.GetDownloadClient(DownloadProtocol.Torrent, 1, false, It.IsAny<HashSet<int>>()))
                .Returns(_downloadClient.Object);

            var definition = new IndexerDefinition { Id = 1 };
            Mocker.GetMock<IIndexerFactory>().Setup(i => i.Get(1)).Returns(definition);
            Mocker.GetMock<IIndexerFactory>().Setup(i => i.GetInstance(definition)).Returns(Mocker.GetMock<IIndexer>().Object);
            Mocker.GetMock<IPodcastDownloadRepository>()
                .Setup(r => r.Insert(It.IsAny<PodcastDownload>()))
                .Returns((PodcastDownload download) => download);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void should_reuse_existing_torrent_when_client_rejects_regrab(bool conflict)
        {
            if (conflict)
            {
                _downloadClient.Setup(c => c.Download(It.IsAny<RemoteEpisode>(), It.IsAny<IIndexer>()))
                    .ThrowsAsync(new DownloadClientRejectedReleaseException(_release, "Torrent already exists"));
            }

            _downloadClient.Setup(c => c.GetItems()).Returns(new[]
            {
                new DownloadClientItem { DownloadId = Hash.ToLowerInvariant() }
            });

            var download = Subject.Grab(_podcast, _episode, _release);

            download.DownloadId.Should().Be(Hash);
            download.PodcastId.Should().Be(_podcast.Id);
            download.PodcastEpisodeId.Should().Be(_episode.Id);
            download.Status.Should().Be(PodcastDownloadStatus.Queued);
        }

        [Test]
        public void should_not_adopt_a_different_torrent_after_add_failure()
        {
            _downloadClient.Setup(c => c.GetItems()).Returns(new[]
            {
                new DownloadClientItem { DownloadId = new string('B', 40) }
            });

            AssertFailedGrab();
        }

        [Test]
        public void should_not_treat_conflict_as_success_when_torrent_is_missing()
        {
            _downloadClient.Setup(c => c.Download(It.IsAny<RemoteEpisode>(), It.IsAny<IIndexer>()))
                .ThrowsAsync(new DownloadClientRejectedReleaseException(_release, "Torrent rejected"));

            AssertFailedGrab();
        }

        [Test]
        public void should_keep_add_failure_when_download_client_cannot_be_queried()
        {
            _downloadClient.Setup(c => c.GetItems()).Throws(new DownloadClientException("Client unavailable"));

            AssertFailedGrab();
        }

        private void AssertFailedGrab()
        {
            Action grab = () => Subject.Grab(_podcast, _episode, _release);

            grab.Should().Throw<NzbDroneClientException>();
            Mocker.GetMock<IPodcastDownloadRepository>()
                .Verify(r => r.Insert(It.IsAny<PodcastDownload>()), Times.Never());
        }
    }
}
