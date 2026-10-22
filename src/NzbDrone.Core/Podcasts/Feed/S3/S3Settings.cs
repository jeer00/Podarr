using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Podcasts.Feed.S3
{
    public class S3Settings
    {
        // eg. https://<account id>.r2.cloudflarestorage.com for Cloudflare R2
        public string Endpoint { get; set; }
        public string Region { get; set; }
        public string Bucket { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }

        public string GetRegion()
        {
            // Cloudflare R2 uses "auto", most other S3 compatible services ignore the region.
            return Region.IsNotNullOrWhiteSpace() ? Region.Trim() : "auto";
        }

        public bool IsComplete()
        {
            return Endpoint.IsNotNullOrWhiteSpace() &&
                   Bucket.IsNotNullOrWhiteSpace() &&
                   AccessKey.IsNotNullOrWhiteSpace() &&
                   SecretKey.IsNotNullOrWhiteSpace();
        }
    }
}
