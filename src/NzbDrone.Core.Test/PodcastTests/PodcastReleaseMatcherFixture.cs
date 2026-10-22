using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Podcasts;
using NzbDrone.Core.Podcasts.Tracker;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastReleaseMatcherFixture : CoreTest
    {
        [TestCase("Chapo Trap House", "Chapo.Trap.House.2024-03-01.Episode.800.MP3", true)]
        [TestCase("Chapo Trap House", "Chapo Trap House - Premium - Ep 800 [MP3]", true)]
        [TestCase("The Daily", "Daily 2024-03-01", true)]
        [TestCase("99% Invisible", "99 Percent Invisible 2024-03-01 MP3", true)]
        [TestCase("Rock & Roll Show", "Rock and Roll Show Ep 1", true)]
        [TestCase("Serial", "Serial Killers 2024-03-01", true)]
        [TestCase("Serial", "SerialKillers 2024-03-01", false)]
        [TestCase("Hardcore History", "Dan Carlins Hardcore History 70", true)]
        [TestCase("Hardcore History", "Hardcore Henry 2016 1080p", false)]
        public void should_match_podcast_title(string term, string releaseTitle, bool expected)
        {
            PodcastReleaseMatcher.IsPodcastMatch(term, releaseTitle).Should().Be(expected);
        }

        [TestCase("#410 - Nyliberal detox och moderat partikultur", 410)]
        [TestCase("#424 - Magnus Henrekson: Barnafödandet", 424)]
        [TestCase("57. A Title", 57)]
        [TestCase("Avsnitt 12: Något", 12)]
        [TestCase("Episode 7 - Title", 7)]
        [TestCase("2024: The year in review", null)]
        [TestCase("Top 10 things", null)]
        [TestCase("Sverige efter #MeToo", null)]
        public void should_parse_episode_number_from_feed_title(string title, int? expected)
        {
            PodcastReleaseMatcher.ParseEpisodeNumberFromTitle(title).Should().Be(expected);
        }

        [TestCase("God Ton™", "God_Ton-E410-PODCAST-PRiVATE-SE-WEB-DIF")]
        [TestCase("Sista Måltiden", "Sista_Maltiden-E424-PODCAST-PRiVATE-SE-WEB-MustafaGPT")]
        public void should_match_scene_style_podcast_releases(string term, string releaseTitle)
        {
            PodcastReleaseMatcher.IsPodcastMatch(term, releaseTitle).Should().BeTrue();
        }

        [Test]
        public void should_match_episode_number_in_feed_title_to_release()
        {
            var episode = new PodcastEpisode { Title = "#410 - Nyliberal detox och moderat partikultur" };

            PodcastReleaseMatcher.GetEpisodeScore("God Ton™", episode, "God_Ton-E410-PODCAST-PRiVATE-SE-WEB-DIF").Should().Be(80);
            PodcastReleaseMatcher.GetEpisodeScore("God Ton™", episode, "God_Ton-E409-PODCAST-PRiVATE-SE-WEB-DIF").Should().Be(0);
        }

        [TestCase("God Ton™", "God Ton")]
        [TestCase("Rock & Roll ®", "Rock & Roll")]
        [TestCase("Sista Måltiden", "Sista Måltiden")]
        public void should_clean_search_query(string term, string expected)
        {
            PodcastReleaseMatcher.CleanQuery(term).Should().Be(expected);
        }

        [TestCase("God ton gratis", "God ton")]
        [TestCase("God ton (gratis)", "God ton")]
        [TestCase("God Ton - Gratisversion", "God Ton")]
        [TestCase("Chapo Trap House (Free Feed)", "Chapo Trap House")]
        [TestCase("The Big Show: Free Preview", "The Big Show")]
        [TestCase("Some Show Podcast", "Some Show")]
        [TestCase("Free Speech Weekly", "Free Speech Weekly")]
        [TestCase("Born Free", "Born Free")]
        [TestCase("Previews", "Previews")]
        [TestCase("Hardcore History", "Hardcore History")]
        public void should_clean_free_feed_words_from_podcast_title(string title, string expected)
        {
            PodcastReleaseMatcher.CleanPodcastTitle(title).Should().Be(expected);
        }

        [Test]
        public void should_search_for_cleaned_and_original_title()
        {
            new Podcast { Title = "God ton gratis" }.GetTrackerSearchTerms().Should().Equal("God ton", "God ton gratis");
            new Podcast { Title = "Hardcore History" }.GetTrackerSearchTerms().Should().Equal("Hardcore History");
            new Podcast { Title = "God ton gratis", TrackerSearchTerm = " Godton " }.GetTrackerSearchTerms().Should().Equal("Godton");
        }

        [TestCase("God ton", "GodTon.2024.10.21.MP3", true)]
        [TestCase("God ton", "God.Ton.Avsnitt.12.MP3", true)]
        [TestCase("Ton", "Godton 2024", false)]
        [TestCase("Chapo Trap House", "ChapoTrapHouse.E800", true)]
        public void should_match_squashed_release_names(string term, string releaseTitle, bool expected)
        {
            PodcastReleaseMatcher.IsPodcastMatch(term, releaseTitle).Should().Be(expected);
        }

        [TestCase("God ton 21 oktober 2024", "2024-10-21")]
        [TestCase("God ton - 3 okt. 2024", "2024-10-03")]
        [TestCase("Show Oct 21, 2024", "2024-10-21")]
        [TestCase("Show 21st March 2024", "2024-03-21")]
        [TestCase("Show 1 maj 2024", "2024-05-01")]
        [TestCase("Show 5 Things 2024", null)]
        [TestCase("Show.2024-03-01.mp3", "2024-03-01")]
        [TestCase("Show 2024.03.01 Title", "2024-03-01")]
        [TestCase("Show_20240301_Title", "2024-03-01")]
        [TestCase("Show 2024-13-45", null)]
        [TestCase("Show 1080p 320kbps", null)]
        public void should_parse_date(string value, string expected)
        {
            PodcastReleaseMatcher.ParseDate(value).Should().Be(expected == null ? null : DateTime.Parse(expected + "T00:00:00Z").ToUniversalTime());
        }

        [TestCase("Show - Ep 123 - Title", 123)]
        [TestCase("Show Episode 7", 7)]
        [TestCase("Show #042 Title", 42)]
        [TestCase("Show E15", 15)]
        [TestCase("God ton Avsnitt 12", 12)]
        [TestCase("God.Ton.Avs.12", 12)]
        [TestCase("Show Folge 3", 3)]
        [TestCase("Show 2024 Title", null)]
        public void should_parse_episode_number(string value, int? expected)
        {
            PodcastReleaseMatcher.ParseEpisodeNumber(value).Should().Be(expected);
        }

        [Test]
        public void should_score_by_date_number_and_title()
        {
            var episode = new PodcastEpisode
            {
                Title = "The Big Story About Things",
                AirDateUtc = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                EpisodeNumber = 800
            };

            PodcastReleaseMatcher.GetEpisodeScore("Show", episode, "Show 2024-03-01").Should().Be(100);
            PodcastReleaseMatcher.GetEpisodeScore("Show", episode, "Show Ep 800").Should().Be(80);
            PodcastReleaseMatcher.GetEpisodeScore("Show", episode, "Show - The Big Story About Things [MP3]").Should().Be(90);
            PodcastReleaseMatcher.GetEpisodeScore("Show", episode, "Show Big Story Things About").Should().Be(60);
            PodcastReleaseMatcher.GetEpisodeScore("Show", episode, "Show - Something Else 2023-01-01").Should().Be(0);
        }

        [TestCase("The Big Story (Free Preview)", "Show - The Big Story [Premium MP3]", 90)]
        [TestCase("PREVIEW: The Big Story", "Show.The.Big.Story.MP3", 90)]
        [TestCase("Teaser - Big Story Things", "Show Big Story Things", 90)]
        public void should_ignore_preview_markers_when_matching_titles(string episodeTitle, string releaseTitle, int expected)
        {
            var episode = new PodcastEpisode { Title = episodeTitle };

            PodcastReleaseMatcher.GetEpisodeScore("Show", episode, releaseTitle).Should().Be(expected);
        }

        [TestCase("Chapo.Trap.House.2024-03-01.The.Big.One", "Chapo Trap House", "The Big One")]
        [TestCase("Chapo Trap House - 2024-03-01 - The Big One", "Chapo Trap House", "The Big One")]
        [TestCase("01 - Intro", "Some Show", "01 - Intro")]
        public void should_build_title_from_file_name(string fileName, string term, string expected)
        {
            PodcastReleaseMatcher.GetTitleFromFileName(term, fileName).Should().Be(expected);
        }
    }
}
