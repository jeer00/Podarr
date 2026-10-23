using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Podcasts.Feed;

namespace NzbDrone.Core.Podcasts
{
    public interface IPodcastLookupService
    {
        List<Podcast> Search(string term);
    }

    public class PodcastLookupService : IPodcastLookupService
    {
        private const string ItunesSearchUrl = "https://itunes.apple.com/search";
        private const string ItunesLookupUrl = "https://itunes.apple.com/lookup";

        private static readonly Regex ItunesIdRegex = new(@"^(?:itunes:|https?://(?:podcasts|itunes)\.apple\.com/.*/id)(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly IPodcastFeedParser _feedParser;
        private readonly IRefreshPodcastService _refreshPodcastService;
        private readonly Logger _logger;

        public PodcastLookupService(IHttpClient httpClient,
                                    IPodcastFeedParser feedParser,
                                    IRefreshPodcastService refreshPodcastService,
                                    Logger logger)
        {
            _httpClient = httpClient;
            _feedParser = feedParser;
            _refreshPodcastService = refreshPodcastService;
            _logger = logger;
        }

        public List<Podcast> Search(string term)
        {
            term = term?.Trim();

            if (term.IsNullOrWhiteSpace())
            {
                return new List<Podcast>();
            }

            var itunesIdMatch = ItunesIdRegex.Match(term);

            if (itunesIdMatch.Success)
            {
                return QueryItunes(new HttpRequestBuilder(ItunesLookupUrl)
                    .AddQueryParam("id", itunesIdMatch.Groups["id"].Value)
                    .AddQueryParam("entity", "podcast")
                    .Build());
            }

            if (Uri.TryCreate(term, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return new List<Podcast> { LookupFeed(term) };
            }

            return QueryItunes(new HttpRequestBuilder(ItunesSearchUrl)
                .AddQueryParam("term", term)
                .AddQueryParam("media", "podcast")
                .AddQueryParam("entity", "podcast")
                .AddQueryParam("limit", 30)
                .Build());
        }

        private Podcast LookupFeed(string feedUrl)
        {
            var feed = _feedParser.Fetch(feedUrl);
            var podcast = new Podcast { FeedUrl = feedUrl };

            _refreshPodcastService.ApplyFeedMetadata(podcast, feed);

            return podcast;
        }

        private List<Podcast> QueryItunes(HttpRequest request)
        {
            request.AllowAutoRedirect = true;

            var response = _httpClient.Get<ItunesSearchResponse>(request);

            return response.Resource.Results
                .Where(r => r.FeedUrl.IsNotNullOrWhiteSpace())
                .Select(r => new Podcast
                {
                    Title = r.CollectionName ?? r.TrackName,
                    SortTitle = PodcastTitleNormalizer.Normalize(r.CollectionName ?? r.TrackName),
                    Author = r.ArtistName,
                    FeedUrl = r.FeedUrl,
                    ImageUrl = r.ArtworkUrl600 ?? r.ArtworkUrl100,
                    ItunesId = r.CollectionId,
                    Explicit = string.Equals(r.CollectionExplicitness, "explicit", StringComparison.OrdinalIgnoreCase),
                    Categories = r.Genres?.Where(g => !string.Equals(g, "Podcasts", StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>()
                })
                .ToList();
        }

        private class ItunesSearchResponse
        {
            public List<ItunesSearchResult> Results { get; set; } = new();
        }

        private class ItunesSearchResult
        {
            public long? CollectionId { get; set; }
            public string CollectionName { get; set; }
            public string TrackName { get; set; }
            public string ArtistName { get; set; }
            public string FeedUrl { get; set; }
            public string ArtworkUrl100 { get; set; }
            public string ArtworkUrl600 { get; set; }
            public string CollectionExplicitness { get; set; }
            public List<string> Genres { get; set; }
        }
    }
}
