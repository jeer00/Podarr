using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NzbDrone.Common.Http;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Http;

namespace NzbDrone.Core.Podcasts
{
    public class PodcastRssParser : IPodcastRssParser
    {
        private const string ITUNES_NS = "{http://www.itunes.com/dtds/podcast-1.0.dtd}";
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public PodcastRssParser(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<PodcastEpisode>> ParseFeed(string rssUrl, Podcast podcast)
        {
            var request = new HttpRequest(rssUrl, HttpAccept.Rss);
            var response = await _httpClient.GetXmlAsync(request);

            var episodes = new List<PodcastEpisode>();
            var document = response.Content;
            var channel = document.Root?.Element("channel");

            if (channel == null)
            {
                throw new Exception("Invalid RSS feed format");
            }

            var items = channel.Elements("item");

            foreach (var item in items)
            {
                try
                {
                    var episode = ParseEpisode(item, podcast);
                    if (episode != null)
                    {
                        episodes.Add(episode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error parsing episode from RSS feed");
                }
            }

            return episodes;
        }

        private PodcastEpisode ParseEpisode(XElement item, Podcast podcast)
        {
            var episode = new PodcastEpisode
            {
                PodcastId = podcast.Id,
                Title = item.Element("title")?.Value,
                Description = item.Element("description")?.Value,
                Published = ParseDate(item.Element("pubDate")?.Value),
                Monitored = true
            };

            // Parse iTunes specific fields
            var itunesTitle = item.Element(ITUNES_NS + "title")?.Value;
            if (!string.IsNullOrWhiteSpace(itunesTitle))
            {
                episode.Title = itunesTitle;
            }

            episode.ItunesSubtitle = item.Element(ITUNES_NS + "subtitle")?.Value;
            episode.ItunesSummary = item.Element(ITUNES_NS + "summary")?.Value;
            episode.ItunesExplicit = item.Element(ITUNES_NS + "explicit")?.Value;

            // Parse enclosure (audio file)
            var enclosure = item.Element("enclosure");
            if (enclosure != null)
            {
                episode.AudioUrl = enclosure.Attribute("url")?.Value;
                episode.AudioType = enclosure.Attribute("type")?.Value;
                episode.AudioLength = enclosure.Attribute("length")?.Value;
            }

            // Parse duration
            var duration = item.Element(ITUNES_NS + "duration")?.Value;
            if (!string.IsNullOrWhiteSpace(duration))
            {
                episode.ItunesDuration = duration;
            }

            return episode;
        }

        private DateTime? ParseDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
            {
                return null;
            }

            try
            {
                return DateTime.Parse(dateStr);
            }
            catch
            {
                return null;
            }
        }
    }
}
