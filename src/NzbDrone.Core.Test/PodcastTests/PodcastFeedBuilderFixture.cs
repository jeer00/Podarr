using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastFeedBuilderFixture : CoreTest
    {
        private const string BaseUrl = "http://podarr.local:8989/feed/secret";
        private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

        private Podcast _podcast;
        private List<PodcastEpisode> _episodes;

        [SetUp]
        public void Setup()
        {
            _podcast = new Podcast { Id = 3, Title = "God ton gratis", Description = "Desc", ImageUrl = "https://example.com/art.jpg", Language = "sv" };

            _episodes = new List<PodcastEpisode>
            {
                new() { Id = 1, Title = "Preview 1", RelativePath = "2024-03-01 - Preview 1.mp3", FileSource = PodcastFileSource.Feed, Size = 100, AirDateUtc = new DateTime(2024, 3, 1) },
                new() { Id = 2, Title = "Full 2", RelativePath = "2024-03-08 - Full 2.m4a", FileSource = PodcastFileSource.Tracker, Size = 5000, AirDateUtc = new DateTime(2024, 3, 8), Duration = 3600, Description = "<p>Show notes & more</p>" },
                new() { Id = 3, Title = "Not downloaded", AirDateUtc = new DateTime(2024, 3, 15) }
            };
        }

        [Test]
        public void should_only_list_tracker_episodes_once_a_podcast_has_them()
        {
            PodcastFeedBuilder.GetFeedEpisodes(_episodes).Select(e => e.Id).Should().Equal(2);
        }

        [Test]
        public void should_list_all_downloaded_episodes_without_tracker_downloads()
        {
            _episodes[1].FileSource = PodcastFileSource.Feed;

            PodcastFeedBuilder.GetFeedEpisodes(_episodes).Select(e => e.Id).Should().Equal(2, 1);
        }

        [Test]
        public void should_build_rss_with_enclosures()
        {
            var rss = PodcastFeedBuilder.BuildRss(_podcast, _episodes, BaseUrl);
            var channel = rss.Root.Element("channel");

            rss.Root.Name.LocalName.Should().Be("rss");
            channel.Element("title").Value.Should().Be("God ton gratis");
            channel.Element(Itunes + "block").Value.Should().Be("Yes");
            channel.Element(Itunes + "image").Attribute("href").Value.Should().Be("https://example.com/art.jpg");

            var item = channel.Elements("item").Single();
            var enclosure = item.Element("enclosure");

            enclosure.Attribute("url").Value.Should().Be($"{BaseUrl}/episode/2/2024-03-08%20-%20Full%202.m4a");
            enclosure.Attribute("length").Value.Should().Be("5000");
            enclosure.Attribute("type").Value.Should().Be("audio/mp4");
            item.Element("description").Value.Should().Be("<p>Show notes & more</p>");
            item.Element(Itunes + "duration").Value.Should().Be("3600");
            item.Element("pubDate").Value.Should().Be("Fri, 08 Mar 2024 00:00:00 GMT");

            // Round trips as valid XML
            XDocument.Parse(rss.ToString()).Root.Element("channel").Elements("item").Should().HaveCount(1);
        }

        [Test]
        public void should_change_guid_when_file_is_replaced()
        {
            var episode = _episodes[0];
            var before = PodcastFeedBuilder.BuildRss(_podcast, new[] { episode }, BaseUrl).Root.Element("channel").Element("item").Element("guid").Value;

            episode.FileSource = PodcastFileSource.Tracker;
            episode.Size = 9000;

            var after = PodcastFeedBuilder.BuildRss(_podcast, new[] { episode }, BaseUrl).Root.Element("channel").Element("item").Element("guid").Value;

            after.Should().NotBe(before);
        }

        [Test]
        public void should_build_opml_with_feed_urls()
        {
            var opml = PodcastFeedBuilder.BuildOpml(new[] { _podcast }, BaseUrl);

            opml.Root.Element("body").Element("outline").Attribute("xmlUrl").Value.Should().Be($"{BaseUrl}/podcast/3.xml");
        }
    }
}
