using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastFeedParserFixture : CoreTest<PodcastFeedParser>
    {
        private const string Feed = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"" xmlns:itunes=""http://www.itunes.com/dtds/podcast-1.0.dtd"" xmlns:content=""http://purl.org/rss/1.0/modules/content/"">
  <channel>
    <title> Test Podcast </title>
    <link>https://example.com</link>
    <language>en-us</language>
    <description>A podcast about tests</description>
    <itunes:author>Jane Doe</itunes:author>
    <itunes:image href=""https://example.com/art.jpg"" />
    <itunes:explicit>yes</itunes:explicit>
    <itunes:new-feed-url>https://example.com/new-feed.xml</itunes:new-feed-url>
    <itunes:category text=""Technology"">
      <itunes:category text=""Software How-To"" />
    </itunes:category>
    <item>
      <title>Episode 2</title>
      <guid isPermaLink=""false"">ep-2</guid>
      <pubDate>Tue, 02 Jan 2024 10:00:00 PST</pubDate>
      <itunes:duration>1:02:03</itunes:duration>
      <itunes:season>1</itunes:season>
      <itunes:episode>2</itunes:episode>
      <itunes:episodeType>Full</itunes:episodeType>
      <content:encoded><![CDATA[<p>Second</p>]]></content:encoded>
      <enclosure url=""https://cdn.example.com/ep2.mp3?token=abc"" type=""audio/mpeg"" length=""12345"" />
    </item>
    <item>
      <title>Episode 1</title>
      <pubDate>Mon, 01 Jan 2024 10:00:00 +0000</pubDate>
      <itunes:duration>754</itunes:duration>
      <description>First</description>
      <enclosure url=""https://cdn.example.com/ep1.m4a"" type=""audio/x-m4a"" length=""0"" />
    </item>
    <item>
      <title>Episode 2 duplicate</title>
      <guid>ep-2</guid>
      <enclosure url=""https://cdn.example.com/ep2-dupe.mp3"" type=""audio/mpeg"" />
    </item>
  </channel>
</rss>";

        [Test]
        public void should_parse_channel_metadata()
        {
            var feed = Subject.Parse(Feed);

            feed.Title.Should().Be("Test Podcast");
            feed.Author.Should().Be("Jane Doe");
            feed.WebsiteUrl.Should().Be("https://example.com");
            feed.ImageUrl.Should().Be("https://example.com/art.jpg");
            feed.Language.Should().Be("en-us");
            feed.Explicit.Should().BeTrue();
            feed.NewFeedUrl.Should().Be("https://example.com/new-feed.xml");
            feed.Categories.Should().BeEquivalentTo(new[] { "Technology", "Software How-To" });
        }

        [Test]
        public void should_parse_items_and_drop_duplicate_guids()
        {
            var feed = Subject.Parse(Feed);

            feed.Items.Should().HaveCount(2);

            var episode = feed.Items.First();
            episode.Guid.Should().Be("ep-2");
            episode.Title.Should().Be("Episode 2");
            episode.Duration.Should().Be(3723);
            episode.SeasonNumber.Should().Be(1);
            episode.EpisodeNumber.Should().Be(2);
            episode.EpisodeType.Should().Be("full");
            episode.Description.Should().Be("<p>Second</p>");
            episode.EnclosureUrl.Should().Be("https://cdn.example.com/ep2.mp3?token=abc");
            episode.EnclosureLength.Should().Be(12345);
            episode.AirDateUtc.Should().Be(new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        }

        [Test]
        public void should_use_enclosure_url_as_guid_when_guid_is_missing()
        {
            var episode = Subject.Parse(Feed).Items.Last();

            episode.Guid.Should().Be("https://cdn.example.com/ep1.m4a");
            episode.Duration.Should().Be(754);
            episode.EnclosureLength.Should().BeNull();
            episode.AirDateUtc.Should().Be(new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        }

        [TestCase("<feed xmlns=\"http://www.w3.org/2005/Atom\"><title>Atom</title></feed>")]
        [TestCase("<html><body>Not found</body></html>")]
        [TestCase("not xml at all")]
        [TestCase("")]
        public void should_throw_for_content_that_is_not_a_podcast_feed(string content)
        {
            Assert.Throws<PodcastFeedException>(() => Subject.Parse(content));
        }

        [TestCase("01:02:03", 3723)]
        [TestCase("62:03", 3723)]
        [TestCase("3723", 3723)]
        [TestCase("3723.5", 3723)]
        [TestCase("", null)]
        [TestCase("about an hour", null)]
        public void should_parse_duration(string value, int? expected)
        {
            PodcastFeedParser.ParseDuration(value).Should().Be(expected);
        }

        [TestCase("Wed, 03 Jul 2024 09:00:00 EDT")]
        [TestCase("Wed, 3 Jul 2024 09:00:00 GMT")]
        [TestCase("Mon, 03 Jul 2024 09:00:00 +0000")]
        [TestCase("2024-07-03T09:00:00Z")]
        public void should_parse_dates(string value)
        {
            PodcastFeedParser.ParseDate(value).Should().HaveValue();
        }

        [TestCase("https://cdn.example.com/ep.mp3?token=1", "audio/mpeg", ".mp3")]
        [TestCase("https://cdn.example.com/ep.M4A", null, ".m4a")]
        [TestCase("https://cdn.example.com/download/123", "audio/x-m4a", ".m4a")]
        [TestCase("https://cdn.example.com/download/123", "video/mp4; codecs=avc1", ".mp4")]
        [TestCase("https://cdn.example.com/download/123", null, ".mp3")]
        public void should_get_extension_from_url_or_mime_type(string url, string type, string expected)
        {
            PodcastDownloadService.GetExtension(new PodcastEpisode { EnclosureUrl = url, EnclosureType = type })
                .Should().Be(expected);
        }
    }
}
