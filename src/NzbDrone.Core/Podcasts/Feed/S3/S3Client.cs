using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Podcasts.Feed.S3
{
    public interface IS3Client
    {
        void PutObject(S3Settings settings, string key, byte[] data, string contentType);
        void DeleteObject(S3Settings settings, string key);
    }

    // A minimal S3 client, only what's needed to publish feed files. Uses path style URLs
    // (endpoint/bucket/key) since every S3 compatible service supports them.
    public class S3Client : IS3Client
    {
        private readonly IHttpClient _httpClient;

        public S3Client(IHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public void PutObject(S3Settings settings, string key, byte[] data, string contentType)
        {
            var request = BuildRequest(settings, HttpMethod.Put, key, data, contentType);
            request.Headers.Add("Cache-Control", "max-age=300");

            _httpClient.Execute(request);
        }

        public void DeleteObject(S3Settings settings, string key)
        {
            _httpClient.Execute(BuildRequest(settings, HttpMethod.Delete, key, null, null));
        }

        private static HttpRequest BuildRequest(S3Settings settings, HttpMethod method, string key, byte[] data, string contentType)
        {
            var endpoint = new Uri(settings.Endpoint.Trim().TrimEnd('/'));
            var path = $"{endpoint.AbsolutePath.TrimEnd('/')}/{settings.Bucket.Trim()}/{key}";
            var canonicalPath = AwsSignatureV4.EncodePath(path);
            var host = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
            var payloadHash = data == null ? AwsSignatureV4.EmptyPayloadHash : AwsSignatureV4.HashPayload(data);
            var timestamp = DateTime.UtcNow;

            var signedHeaders = new Dictionary<string, string>
            {
                { "host", host },
                { "x-amz-content-sha256", payloadHash },
                { "x-amz-date", timestamp.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) }
            };

            if (contentType != null)
            {
                signedHeaders.Add("content-type", contentType);
            }

            var request = new HttpRequest($"{endpoint.Scheme}://{host}{canonicalPath}")
            {
                Method = method,
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            request.Headers.Add("x-amz-content-sha256", payloadHash);
            request.Headers.Add("x-amz-date", signedHeaders["x-amz-date"]);
            request.Headers.Add("Authorization", AwsSignatureV4.GetAuthorizationHeader(method.Method, canonicalPath, signedHeaders, payloadHash, settings.AccessKey.Trim(), settings.SecretKey.Trim(), settings.GetRegion(), timestamp));

            if (data != null)
            {
                request.Headers.ContentType = contentType;
                request.SetContent(data);
            }

            return request;
        }
    }
}
