using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Podcasts.Feed
{
    // Builds the RSS feeds Podarr serves of the episodes it has downloaded, so podcast apps can subscribe to them.
    public static class PodcastFeedBuilder
    {
        private static readonly XNamespace ItunesNamespace = "http://www.itunes.com/dtds/podcast-1.0.dtd";
        private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";

        private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".mp3", "audio/mpeg" },
            { ".m4a", "audio/mp4" },
            { ".m4b", "audio/mp4" },
            { ".aac", "audio/aac" },
            { ".ogg", "audio/ogg" },
            { ".oga", "audio/ogg" },
            { ".opus", "audio/ogg" },
            { ".flac", "audio/flac" },
            { ".wav", "audio/wav" },
            { ".mp4", "video/mp4" },
            { ".m4v", "video/x-m4v" },
            { ".mov", "video/quicktime" },
            { ".webm", "video/webm" },
            { ".mkv", "video/x-matroska" }
        };

        // Once a podcast has episodes from a tracker its feed only lists those, the feed's own files
        // are previews of them. Podcasts without tracker downloads list every downloaded episode.
        public static List<PodcastEpisode> GetFeedEpisodes(IEnumerable<PodcastEpisode> episodes)
        {
            var downloaded = episodes.Where(e => e.HasFile).ToList();

            if (downloaded.Any(e => e.FileSource == PodcastFileSource.Tracker))
            {
                downloaded = downloaded.Where(e => e.FileSource == PodcastFileSource.Tracker).ToList();
            }

            return downloaded
                .OrderByDescending(GetPublishDate)
                .ThenByDescending(e => e.Id)
                .ToList();
        }

        public static string GetMimeType(string path)
        {
            return MimeTypes.TryGetValue(Path.GetExtension(path) ?? string.Empty, out var mimeType)
                ? mimeType
                : "application/octet-stream";
        }

        public static string GetFeedUrl(string feedBaseUrl, int podcastId)
        {
            return $"{feedBaseUrl}/podcast/{podcastId}.xml";
        }

        public static string GetEpisodeUrl(string feedBaseUrl, PodcastEpisode episode)
        {
            return $"{feedBaseUrl}/episode/{episode.Id}/{Uri.EscapeDataString(Path.GetFileName(episode.RelativePath))}";
        }

        // selfUrl is where the feed is published, when that's somewhere else than Podarr (eg. an S3 bucket).
        public static XDocument BuildRss(Podcast podcast, IEnumerable<PodcastEpisode> episodes, string feedBaseUrl, string selfUrl = null)
        {
            var feedUrl = selfUrl ?? GetFeedUrl(feedBaseUrl, podcast.Id);

            var channel = new XElement("channel",
                new XElement("title", podcast.Title),
                new XElement("link", podcast.WebsiteUrl.IsNotNullOrWhiteSpace() ? podcast.WebsiteUrl : feedUrl),
                new XElement("description", podcast.Description ?? podcast.Title),
                new XElement("generator", "Podarr"),
                new XElement("lastBuildDate", DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture)),
                new XElement(AtomNamespace + "link",
                    new XAttribute("href", feedUrl),
                    new XAttribute("rel", "self"),
                    new XAttribute("type", "application/rss+xml")),
                new XElement(ItunesNamespace + "explicit", podcast.Explicit ? "true" : "false"));

            // Keep private feeds out of podcast directories if the URL ever gets submitted to one.
            channel.Add(new XElement(ItunesNamespace + "block", "Yes"));

            if (podcast.Language.IsNotNullOrWhiteSpace())
            {
                channel.Add(new XElement("language", podcast.Language));
            }

            if (podcast.Author.IsNotNullOrWhiteSpace())
            {
                channel.Add(new XElement(ItunesNamespace + "author", podcast.Author));
            }

            if (podcast.ImageUrl.IsNotNullOrWhiteSpace())
            {
                channel.Add(new XElement(ItunesNamespace + "image", new XAttribute("href", podcast.ImageUrl)));
                channel.Add(new XElement("image",
                    new XElement("url", podcast.ImageUrl),
                    new XElement("title", podcast.Title),
                    new XElement("link", podcast.WebsiteUrl.IsNotNullOrWhiteSpace() ? podcast.WebsiteUrl : feedUrl)));
            }

            foreach (var category in podcast.Categories ?? new List<string>())
            {
                channel.Add(new XElement(ItunesNamespace + "category", new XAttribute("text", category)));
            }

            foreach (var episode in GetFeedEpisodes(episodes))
            {
                channel.Add(BuildItem(episode, feedBaseUrl));
            }

            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("rss",
                    new XAttribute("version", "2.0"),
                    new XAttribute(XNamespace.Xmlns + "itunes", ItunesNamespace),
                    new XAttribute(XNamespace.Xmlns + "atom", AtomNamespace),
                    channel));
        }

        public static XDocument BuildOpml(IEnumerable<Podcast> podcasts, string feedBaseUrl)
        {
            return BuildOpml(podcasts, p => GetFeedUrl(feedBaseUrl, p.Id));
        }

        public static XDocument BuildOpml(IEnumerable<Podcast> podcasts, Func<Podcast, string> getFeedUrl)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("opml",
                    new XAttribute("version", "2.0"),
                    new XElement("head", new XElement("title", "Podarr")),
                    new XElement("body",
                        podcasts.OrderBy(p => p.SortTitle ?? p.Title).Select(p => new XElement("outline",
                            new XAttribute("type", "rss"),
                            new XAttribute("text", p.Title),
                            new XAttribute("title", p.Title),
                            new XAttribute("xmlUrl", getFeedUrl(p)))))));
        }

        public static string ToXml(XDocument document)
        {
            return document.Declaration + "\n" + document.ToString();
        }

        private static XElement BuildItem(PodcastEpisode episode, string feedBaseUrl)
        {
            // Changes when a feed preview is replaced by the tracker release so apps download the new file.
            var guid = $"podarr-{episode.Id}-{episode.FileSource.ToString().ToLowerInvariant()}-{episode.Size}";

            var item = new XElement("item",
                new XElement("title", episode.Title),
                new XElement("guid", new XAttribute("isPermaLink", "false"), guid),
                new XElement("pubDate", GetPublishDate(episode).ToString("r", CultureInfo.InvariantCulture)),
                new XElement("enclosure",
                    new XAttribute("url", GetEpisodeUrl(feedBaseUrl, episode)),
                    new XAttribute("length", episode.Size),
                    new XAttribute("type", GetMimeType(episode.RelativePath))));

            if (episode.Description.IsNotNullOrWhiteSpace())
            {
                item.Add(new XElement("description", episode.Description));
            }

            if (episode.Duration.HasValue)
            {
                item.Add(new XElement(ItunesNamespace + "duration", episode.Duration.Value));
            }

            if (episode.SeasonNumber.HasValue)
            {
                item.Add(new XElement(ItunesNamespace + "season", episode.SeasonNumber.Value));
            }

            if (episode.EpisodeNumber.HasValue)
            {
                item.Add(new XElement(ItunesNamespace + "episode", episode.EpisodeNumber.Value));
            }

            if (episode.ImageUrl.IsNotNullOrWhiteSpace())
            {
                item.Add(new XElement(ItunesNamespace + "image", new XAttribute("href", episode.ImageUrl)));
            }

            return item;
        }

        private static DateTime GetPublishDate(PodcastEpisode episode)
        {
            return DateTime.SpecifyKind(episode.AirDateUtc ?? episode.DateDownloaded ?? episode.Added, DateTimeKind.Utc);
        }
    }
}
