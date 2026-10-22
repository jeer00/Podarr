using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Tracker;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastTrackerImportServiceFixture : CoreTest<PodcastTrackerImportService>
    {
        private Podcast _podcast;
        private PodcastDownload _download;
        private List<PodcastEpisode> _episodes;
        private string _outputPath;
        private Dictionary<string, long> _files;

        [SetUp]
        public void Setup()
        {
            _podcast = new Podcast { Id = 1, Title = "Chapo Trap House", Path = "/podcasts/Chapo Trap House".AsOsAgnostic() };
            _download = new PodcastDownload { Id = 1, PodcastId = 1, Title = "Chapo Trap House 2024 Premium" };
            _outputPath = "/downloads/Chapo Trap House 2024".AsOsAgnostic();
            _files = new Dictionary<string, long>();

            _episodes = new List<PodcastEpisode>
            {
                new() { Id = 10, PodcastId = 1, FeedGuid = "a", Title = "The Big One", AirDateUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
                new() { Id = 11, PodcastId = 1, FeedGuid = "b", Title = "Another One (Preview)", AirDateUtc = new DateTime(2024, 3, 8, 0, 0, 0, DateTimeKind.Utc), RelativePath = "preview.mp3", FileSource = PodcastFileSource.Feed, Size = 100 }
            };

            Mocker.GetMock<IPodcastEpisodeRepository>()
                .Setup(s => s.GetByPodcastId(1))
                .Returns(() => _episodes.ToList());

            Mocker.GetMock<IPodcastEpisodeRepository>()
                .Setup(s => s.Insert(It.IsAny<PodcastEpisode>()))
                .Returns<PodcastEpisode>(e =>
                {
                    e.Id = 100 + _episodes.Count;
                    return e;
                });

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(_outputPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(_outputPath, true))
                .Returns(() => _files.Keys.ToList());

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFileSize(It.IsAny<string>()))
                .Returns<string>(path => _files.TryGetValue(path, out var size) ? size : 0);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FileExists(It.IsAny<string>()))
                .Returns<string>(path => _files.ContainsKey(path) || path.EndsWith("preview.mp3"));
        }

        private static bool IsExclusiveEpisode(PodcastEpisode episode)
        {
            return episode.Title == "Patreon Exclusive" &&
                   episode.AirDateUtc == new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc) &&
                   episode.RelativePath == "2024-03-15 - Patreon Exclusive.mp3";
        }

        private string GivenFile(string name, long size)
        {
            var path = Path.Combine(_outputPath, name);
            _files[path] = size;

            return path;
        }

        [Test]
        public void should_match_files_to_episodes_by_date_and_create_unknown_episodes()
        {
            GivenFile("Chapo.Trap.House.2024-03-01.The.Big.One.mp3", 1000);
            GivenFile("Chapo.Trap.House.2024-03-15.Patreon.Exclusive.mp3", 2000);
            GivenFile("cover.jpg", 50);

            Subject.Import(_podcast, _download, _outputPath).Should().Be(2);

            _episodes[0].RelativePath.Should().Be("2024-03-01 - The Big One.mp3");
            _episodes[0].Size.Should().Be(1000);

            Mocker.GetMock<IPodcastEpisodeRepository>()
                .Verify(v => v.Insert(It.Is<PodcastEpisode>(e => IsExclusiveEpisode(e))), Times.Once());

            Mocker.GetMock<IDiskTransferService>()
                .Verify(v => v.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.HardLinkOrCopy, false),
                        Times.Exactly(2));
        }

        [Test]
        public void should_replace_feed_preview_with_tracker_file()
        {
            GivenFile("Chapo Trap House - 2024-03-08 - Another One.mp3", 50);

            Subject.Import(_podcast, _download, _outputPath).Should().Be(1);

            _episodes[1].Size.Should().Be(50);
            _episodes[1].FileSource.Should().Be(PodcastFileSource.Tracker);
            _episodes[1].RelativePath.Should().Be("2024-03-08 - Another One (Preview).mp3");

            Mocker.GetMock<IRecycleBinProvider>()
                .Verify(v => v.DeleteFile(It.Is<string>(p => p.EndsWith("preview.mp3")), It.IsAny<string>()),
                        Times.Once());
        }

        [Test]
        public void should_not_replace_larger_existing_tracker_file()
        {
            _episodes[1].FileSource = PodcastFileSource.Tracker;
            _episodes[1].Size = 10000;
            GivenFile("Chapo Trap House - 2024-03-08 - Another One.mp3", 5000);

            Subject.Import(_podcast, _download, _outputPath).Should().Be(0);

            Mocker.GetMock<IDiskTransferService>()
                .Verify(v => v.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_assign_single_file_to_episode_it_was_grabbed_for()
        {
            _download.PodcastEpisodeId = 10;
            GivenFile("track01.mp3", 1000);

            Subject.Import(_podcast, _download, _outputPath).Should().Be(1);

            _episodes[0].RelativePath.Should().Be("2024-03-01 - The Big One.mp3");
        }

        [Test]
        public void should_import_file_to_the_episode_its_name_matches_not_the_one_it_was_grabbed_for()
        {
            _episodes.Add(new PodcastEpisode { Id = 12, PodcastId = 1, FeedGuid = "c", Title = "#409 - Something" });
            _episodes.Add(new PodcastEpisode { Id = 13, PodcastId = 1, FeedGuid = "d", Title = "#410 - Something Else" });
            _download.PodcastEpisodeId = 13;

            GivenFile("God_Ton-E409-PODCAST-PRiVATE-SE-WEB-DIF.mp3", 1000);

            Subject.Import(_podcast, _download, _outputPath).Should().Be(1);

            _episodes.Single(e => e.Id == 12).HasFile.Should().BeTrue();
            _episodes.Single(e => e.Id == 13).HasFile.Should().BeFalse();
        }

        [Test]
        public void should_throw_when_download_has_no_media_files()
        {
            GivenFile("readme.txt", 10);

            Assert.Throws<PodcastImportException>(() => Subject.Import(_podcast, _download, _outputPath));
        }
    }
}
