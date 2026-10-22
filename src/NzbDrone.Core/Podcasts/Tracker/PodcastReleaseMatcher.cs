using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Podcasts.Tracker
{
    // Matches tracker release titles and downloaded file names against podcasts and their episodes.
    // Release naming on trackers is inconsistent, so this relies on a few strong signals
    // (air date, episode number, episode title) and reports a score rather than a yes/no answer.
    public static class PodcastReleaseMatcher
    {
        public const int ConfidentScore = 60;

        private static readonly Regex NonAlphanumericRegex = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);
        private static readonly Regex LeadingArticleRegex = new(@"^(?:the|a|an)\s+", RegexOptions.Compiled);
        private static readonly Regex DateRegex = new(@"(?<!\d)(?<year>(?:19|20)\d{2})[-._ ]?(?<month>0[1-9]|1[0-2])[-._ ]?(?<day>0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.Compiled);

        // Free feeds of paywalled shows label their episodes, eg. "The Big Story (Free Preview)",
        // these words never appear in the full release so they're ignored when comparing titles.
        private static readonly HashSet<string> PreviewWords = new(StringComparer.Ordinal)
        {
            "preview", "previews", "teaser", "trailer", "free", "sample", "clip", "excerpt", "snippet", "unlocked", "public", "version"
        };

        // Free feeds often add a word to the show's name ("God ton gratis", "Show (Free Feed)") that the
        // tracker releases don't have, in the languages trackers commonly use. Matched after Normalize.
        private static readonly HashSet<string> FreeFeedTitleWords = new(StringComparer.Ordinal)
        {
            "free", "gratis", "gratisversion", "gratisversionen", "gratisavsnitt", "gratispodd", "gratisfeed",
            "preview", "previews", "teaser", "teasers", "trailer", "trailers", "sample", "samples", "sampler",
            "smakprov", "forhandstitt", "forsmak", "smagsprove", "smagsprøve", "kostenlos", "vorschau",
            "public", "feed", "version", "edition", "unlocked", "podcast", "podcasts"
        };

        // Month names and abbreviations in English, Swedish, Norwegian, Danish and German, after Normalize.
        private static readonly Dictionary<string, int> MonthNames = BuildMonthNames();

        private static readonly Regex BracketsRegex = new(@"[\(\[\{](?<inner>[^\)\]\}]*)[\)\]\}]", RegexOptions.Compiled);
        private static readonly Regex SegmentSeparatorRegex = new(@"\s+[-–—|:]\s+|:\s+", RegexOptions.Compiled);
        private static readonly Regex DayMonthYearRegex = new(@"(?<!\d)(?<day>\d{1,2})(?:st|nd|rd|th|e|a|:e)?\.?\s+(?<month>\p{L}{3,9})\.?,?\s+(?<year>(?:19|20)\d{2})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MonthDayYearRegex = new(@"\b(?<month>\p{L}{3,9})\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?,?\s+(?<year>(?:19|20)\d{2})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LeadingEpisodeNumberRegex = new(@"^\s*(?:#\s*)?0*(?<number>\d{1,4})\s*[-–—.:|)]\s+", RegexOptions.Compiled);
        private static readonly Regex QueryUnsafeCharactersRegex = new(@"[^\p{L}\p{N}\s'&.-]+", RegexOptions.Compiled);
        private static readonly Regex EpisodeNumberRegex = new(@"(?:\b(?:ep|episode|avsnitt|avs|afsnit|folge|e)\s*\.?\s*|#)0*(?<number>\d{1,5})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Normalize(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            var normalized = value.RemoveDiacritics().ToLowerInvariant().Replace("&", " and ").Replace("%", " percent ");
            normalized = NonAlphanumericRegex.Replace(normalized, " ").Trim();

            return LeadingArticleRegex.Replace(normalized, string.Empty);
        }

        public static bool IsPodcastMatch(string searchTerm, string releaseTitle)
        {
            var term = Normalize(searchTerm);
            var title = Normalize(releaseTitle);

            if (term.IsNullOrWhiteSpace() || title.IsNullOrWhiteSpace())
            {
                return false;
            }

            // Whole word match so "Serial" doesn't match "Serialized".
            if ($" {title} ".Contains($" {term} "))
            {
                return true;
            }

            // Some uploaders squash multi-word names together ("GodTon.2024.10.21"), only trusted for longer names.
            var compactTerm = term.Replace(" ", string.Empty);

            return term.Contains(' ') && compactTerm.Length >= 6 && title.Replace(" ", string.Empty).Contains(compactTerm);
        }

        public static bool IsPodcastMatch(IEnumerable<string> searchTerms, string releaseTitle)
        {
            return searchTerms.Any(term => IsPodcastMatch(term, releaseTitle));
        }

        // Removes the words free feeds add to a show's name, "God ton gratis" -> "God ton".
        // Returns the title unchanged when what's left would be too short to search for.
        public static string CleanPodcastTitle(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return title;
            }

            var cleaned = BracketsRegex.Replace(title, m => IsOnlyFreeFeedWords(m.Groups["inner"].Value) ? " " : m.Value);

            cleaned = string.Join(" ", SegmentSeparatorRegex.Split(cleaned).Where(s => !IsOnlyFreeFeedWords(s)));

            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            while (words.Count > 1 && FreeFeedTitleWords.Contains(Normalize(words[^1])))
            {
                words.RemoveAt(words.Count - 1);
            }

            cleaned = string.Join(' ', words).Trim(' ', '-', '–', '—', '|', ':', ',');

            return Normalize(cleaned).Length >= 5 ? cleaned : title.Trim();
        }

        private static bool IsOnlyFreeFeedWords(string value)
        {
            var words = Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return words.Length > 0 && words.All(FreeFeedTitleWords.Contains);
        }

        public static DateTime? ParseDate(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            foreach (Match match in DateRegex.Matches(value))
            {
                var date = ToDate(match.Groups["year"].Value, int.Parse(match.Groups["month"].Value), match.Groups["day"].Value);

                if (date.HasValue)
                {
                    return date;
                }
            }

            // Written out dates, eg. "21 oktober 2024", "21 okt. 2024", "Oct 21, 2024"
            foreach (var regex in new[] { DayMonthYearRegex, MonthDayYearRegex })
            {
                foreach (Match match in regex.Matches(value))
                {
                    if (!MonthNames.TryGetValue(Normalize(match.Groups["month"].Value), out var month))
                    {
                        continue;
                    }

                    var date = ToDate(match.Groups["year"].Value, month, match.Groups["day"].Value);

                    if (date.HasValue)
                    {
                        return date;
                    }
                }
            }

            return null;
        }

        private static DateTime? ToDate(string year, int month, string day)
        {
            var text = $"{year}-{month:00}-{int.Parse(day):00}";

            return DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date
                : null;
        }

        private static Dictionary<string, int> BuildMonthNames()
        {
            var names = new[]
            {
                new[] { "jan", "january", "januari", "januar" },
                new[] { "feb", "february", "februari", "februar" },
                new[] { "mar", "march", "mars", "marts", "marz", "maerz" },
                new[] { "apr", "april" },
                new[] { "may", "maj", "mai" },
                new[] { "jun", "june", "juni" },
                new[] { "jul", "july", "juli" },
                new[] { "aug", "august", "augusti" },
                new[] { "sep", "sept", "september" },
                new[] { "oct", "okt", "october", "oktober" },
                new[] { "nov", "november" },
                new[] { "dec", "des", "dez", "december", "desember", "dezember" }
            };

            var result = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < names.Length; i++)
            {
                foreach (var name in names[i])
                {
                    result[name] = i + 1;
                }
            }

            return result;
        }

        public static int? ParseEpisodeNumber(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            var match = EpisodeNumberRegex.Match(value);

            return match.Success ? int.Parse(match.Groups["number"].Value) : null;
        }

        // Feeds without itunes:episode often have the number in the title instead, eg. "#424 - Guest: Topic" or "57. Title".
        public static int? ParseEpisodeNumberFromTitle(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var leading = LeadingEpisodeNumberRegex.Match(title);

            if (leading.Success)
            {
                var number = int.Parse(leading.Groups["number"].Value);

                // A leading year ("2024: The year in review") isn't an episode number.
                if (number is < 1900 or > 2099)
                {
                    return number;
                }
            }

            return ParseEpisodeNumber(title);
        }

        // Trackers search for every word, symbols like ™ in a feed title would make them find nothing.
        public static string CleanQuery(string term)
        {
            return Regex.Replace(QueryUnsafeCharactersRegex.Replace(term ?? string.Empty, " "), @"\s{2,}", " ").Trim();
        }

        public static int GetEpisodeScore(string podcastTerm, PodcastEpisode episode, string releaseTitle)
        {
            var score = 0;

            if (episode.AirDateUtc.HasValue)
            {
                var releaseDate = ParseDate(releaseTitle);

                if (releaseDate.HasValue && Math.Abs((releaseDate.Value.Date - episode.AirDateUtc.Value.Date).TotalDays) <= 1)
                {
                    score = Math.Max(score, 100);
                }
            }

            var episodeNumber = episode.EpisodeNumber ?? ParseEpisodeNumberFromTitle(episode.Title);

            if (episodeNumber.HasValue && ParseEpisodeNumber(releaseTitle) == episodeNumber)
            {
                score = Math.Max(score, 80);
            }

            var episodeTitle = RemovePreviewWords(RemoveTerm(Normalize(episode.Title), Normalize(podcastTerm)));
            var release = RemoveTerm(Normalize(releaseTitle), Normalize(podcastTerm));

            if (episodeTitle.Length >= 8 && release.Contains(episodeTitle))
            {
                score = Math.Max(score, 90);
            }
            else
            {
                var episodeTokens = Tokens(episodeTitle);

                if (episodeTokens.Count >= 3)
                {
                    var releaseTokens = Tokens(release);
                    var overlap = episodeTokens.Count(releaseTokens.Contains) / (double)episodeTokens.Count;

                    if (overlap >= 0.8)
                    {
                        score = Math.Max(score, 60);
                    }
                }
            }

            return score;
        }

        public static PodcastEpisode FindBestEpisode(string podcastTerm, IEnumerable<PodcastEpisode> episodes, string releaseTitle)
        {
            return episodes
                .Select(e => new { Episode = e, Score = GetEpisodeScore(podcastTerm, e, releaseTitle) })
                .Where(e => e.Score >= ConfidentScore)
                .OrderByDescending(e => e.Score)
                .ThenByDescending(e => e.Episode.AirDateUtc ?? DateTime.MinValue)
                .Select(e => e.Episode)
                .FirstOrDefault();
        }

        // Builds a readable episode title from a downloaded file name, eg.
        // "Show.Name.2024-03-01.The.Big.Story.mp3" -> "The Big Story"
        public static string GetTitleFromFileName(string podcastTerm, string fileNameWithoutExtension)
        {
            var title = fileNameWithoutExtension.Replace('_', ' ');

            if (!title.Contains(' '))
            {
                title = title.Replace('.', ' ');
            }

            title = DateRegex.Replace(title, " ");

            var term = podcastTerm?.Trim();

            if (term.IsNotNullOrWhiteSpace())
            {
                title = Regex.Replace(title, "^" + Regex.Escape(term).Replace(@"\ ", @"[\s._-]+"), " ", RegexOptions.IgnoreCase);
            }

            title = Regex.Replace(title, @"^[\s\-–—:|.]+|[\s\-–—:|.]+$", string.Empty);
            title = Regex.Replace(title, @"\s{2,}", " ");

            return title.IsNullOrWhiteSpace() ? fileNameWithoutExtension : title;
        }

        private static string RemovePreviewWords(string value)
        {
            return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !PreviewWords.Contains(w)));
        }

        private static string RemoveTerm(string value, string term)
        {
            if (term.IsNullOrWhiteSpace())
            {
                return value;
            }

            return $" {value} ".Replace($" {term} ", " ").Trim();
        }

        private static HashSet<string> Tokens(string value)
        {
            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1)
                .ToHashSet();
        }
    }
}
