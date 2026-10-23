using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Podcasts
{
    public static class PodcastTitleNormalizer
    {
        private static readonly Regex LeadingArticleRegex = new(@"^(?:the|a|an)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Normalize(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return string.Empty;
            }

            return LeadingArticleRegex.Replace(title.Trim(), string.Empty).ToLowerInvariant();
        }
    }
}
