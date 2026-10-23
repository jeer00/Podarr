using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Podcasts.Feed
{
    public interface IPodcastFeedParser
    {
        PodcastFeed Fetch(string feedUrl);
        PodcastFeed Parse(string content);
    }

    public class PodcastFeedParser : IPodcastFeedParser
    {
        private static readonly XNamespace ItunesNamespace = "http://www.itunes.com/dtds/podcast-1.0.dtd";
        private static readonly XNamespace ContentNamespace = "http://purl.org/rss/1.0/modules/content/";

        private static readonly Regex TrailingTimeZoneRegex = new(@"\s[A-Z]{2,5}$", RegexOptions.Compiled);
        private static readonly Regex DurationRegex = new(@"^(?:(?:(?<hours>\d+):)?(?<minutes>\d+):)?(?<seconds>\d+)(?:\.\d+)?$", RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public PodcastFeedParser(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public PodcastFeed Fetch(string feedUrl)
        {
            HttpResponse response;

            try
            {
                var request = new HttpRequest(feedUrl, HttpAccept.Rss)
                {
                    AllowAutoRedirect = true,
                    RequestTimeout = TimeSpan.FromSeconds(60)
                };

                response = _httpClient.Get(request);
            }
            catch (HttpException ex)
            {
                throw new PodcastFeedException("Unable to fetch podcast feed {0}: {1}", ex, feedUrl, ex.Message);
            }
            catch (Exception ex) when (ex is not PodcastFeedException)
            {
                throw new PodcastFeedException("Unable to fetch podcast feed {0}: {1}", ex, feedUrl, ex.Message);
            }

            return Parse(response.Content);
        }

        public PodcastFeed Parse(string content)
        {
            if (content.IsNullOrWhiteSpace())
            {
                throw new PodcastFeedException("Podcast feed is empty{0}", string.Empty);
            }

            XDocument document;

            try
            {
                using var reader = XmlReader.Create(new StringReader(content.Trim()), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true, XmlResolver = null });
                document = XDocument.Load(reader);
            }
            catch (XmlException ex)
            {
                throw new PodcastFeedException("Podcast feed is not valid XML: {0}", ex, ex.Message);
            }

            var channel = document.Root?.Element("channel");

            if (document.Root?.Name.LocalName != "rss" || channel == null)
            {
                throw new PodcastFeedException("Not a podcast RSS feed (expected <rss><channel>, found <{0}>)", document.Root?.Name.LocalName);
            }

            var feed = new PodcastFeed
            {
                Title = CleanText(channel.Element("title")?.Value),
                Description = FirstNotEmpty(channel.Element("description")?.Value,
                                            channel.Element(ItunesNamespace + "summary")?.Value,
                                            channel.Element(ItunesNamespace + "subtitle")?.Value),
                Author = FirstNotEmpty(channel.Element(ItunesNamespace + "author")?.Value,
                                       channel.Element(ItunesNamespace + "owner")?.Element(ItunesNamespace + "name")?.Value,
                                       channel.Element("managingEditor")?.Value),
                WebsiteUrl = FirstNotEmpty(channel.Elements("link").Select(e => e.Value).ToArray()),
                ImageUrl = FirstNotEmpty(channel.Element(ItunesNamespace + "image")?.Attribute("href")?.Value,
                                         channel.Element("image")?.Element("url")?.Value),
                Language = CleanText(channel.Element("language")?.Value),
                Explicit = ParseExplicit(channel.Element(ItunesNamespace + "explicit")?.Value),
                NewFeedUrl = CleanText(channel.Element(ItunesNamespace + "new-feed-url")?.Value),
                Categories = channel.Descendants(ItunesNamespace + "category")
                                    .Select(c => CleanText(c.Attribute("text")?.Value))
                                    .Where(c => c.IsNotNullOrWhiteSpace())
                                    .Distinct()
                                    .ToList()
            };

            if (feed.Title.IsNullOrWhiteSpace())
            {
                throw new PodcastFeedException("Podcast feed is missing a title{0}", string.Empty);
            }

            foreach (var item in channel.Elements("item"))
            {
                try
                {
                    var feedItem = ParseItem(item);

                    if (feedItem != null)
                    {
                        feed.Items.Add(feedItem);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to parse item in podcast feed {0}", feed.Title);
                }
            }

            // Duplicate GUIDs do happen in the wild, keep the first (newest) one.
            feed.Items = feed.Items
                .GroupBy(i => i.Guid)
                .Select(g => g.First())
                .ToList();

            return feed;
        }

        private PodcastFeedItem ParseItem(XElement item)
        {
            var enclosure = item.Element("enclosure");
            var enclosureUrl = CleanText(enclosure?.Attribute("url")?.Value);

            var title = FirstNotEmpty(item.Element(ItunesNamespace + "title")?.Value, item.Element("title")?.Value);
            var airDate = ParseDate(item.Element("pubDate")?.Value);

            if (title.IsNullOrWhiteSpace() && enclosureUrl.IsNullOrWhiteSpace())
            {
                return null;
            }

            var guid = FirstNotEmpty(item.Element("guid")?.Value, enclosureUrl, $"{title}|{airDate:O}");

            return new PodcastFeedItem
            {
                Guid = guid,
                Title = title ?? airDate?.ToString("yyyy-MM-dd") ?? "Untitled",
                Description = FirstNotEmpty(item.Element("description")?.Value,
                                            item.Element(ContentNamespace + "encoded")?.Value,
                                            item.Element(ItunesNamespace + "summary")?.Value,
                                            item.Element(ItunesNamespace + "subtitle")?.Value),
                AirDateUtc = airDate,
                SeasonNumber = ParseInt(item.Element(ItunesNamespace + "season")?.Value),
                EpisodeNumber = ParseInt(item.Element(ItunesNamespace + "episode")?.Value),
                EpisodeType = CleanText(item.Element(ItunesNamespace + "episodeType")?.Value)?.ToLowerInvariant(),
                Duration = ParseDuration(item.Element(ItunesNamespace + "duration")?.Value),
                EnclosureUrl = enclosureUrl,
                EnclosureType = CleanText(enclosure?.Attribute("type")?.Value),
                EnclosureLength = ParseLong(enclosure?.Attribute("length")?.Value),
                ImageUrl = CleanText(item.Element(ItunesNamespace + "image")?.Attribute("href")?.Value),
                Explicit = ParseExplicit(item.Element(ItunesNamespace + "explicit")?.Value)
            };
        }

        public static DateTime? ParseDate(string value)
        {
            value = CleanText(value);

            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

            if (DateTime.TryParse(value, DateTimeFormatInfo.InvariantInfo, styles, out var result))
            {
                return result;
            }

            // RFC 822 dates often end in a time zone abbreviation (PST, EDT, ...) that .NET can't parse.
            var withoutZone = TrailingTimeZoneRegex.Replace(value, string.Empty);

            if (DateTime.TryParse(withoutZone, DateTimeFormatInfo.InvariantInfo, styles, out result))
            {
                return result;
            }

            // Some feeds include the day of week with the wrong value or in a non-English language, drop it.
            var commaIndex = withoutZone.IndexOf(',');

            if (commaIndex >= 0 && DateTime.TryParse(withoutZone.AsSpan(commaIndex + 1), DateTimeFormatInfo.InvariantInfo, styles, out result))
            {
                return result;
            }

            return null;
        }

        public static int? ParseDuration(string value)
        {
            value = CleanText(value);

            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            var match = DurationRegex.Match(value);

            if (!match.Success)
            {
                return null;
            }

            var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
            var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;
            var seconds = long.Parse(match.Groups["seconds"].Value);

            var total = (hours * 3600L) + (minutes * 60L) + seconds;

            return total is > 0 and <= int.MaxValue ? (int)total : null;
        }

        private static bool ParseExplicit(string value)
        {
            value = CleanText(value)?.ToLowerInvariant();

            return value is "yes" or "true" or "explicit";
        }

        private static int? ParseInt(string value)
        {
            return int.TryParse(CleanText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
        }

        private static long? ParseLong(string value)
        {
            return long.TryParse(CleanText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && result > 0 ? result : null;
        }

        private static string FirstNotEmpty(params string[] values)
        {
            return values.Select(CleanText).FirstOrDefault(v => v.IsNotNullOrWhiteSpace());
        }

        private static string CleanText(string value)
        {
            return value.IsNullOrWhiteSpace() ? null : value.Trim();
        }
    }
}
