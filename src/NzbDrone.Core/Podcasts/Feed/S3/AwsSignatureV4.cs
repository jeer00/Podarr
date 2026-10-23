using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NzbDrone.Core.Podcasts.Feed.S3
{
    // AWS Signature Version 4, which S3 and every S3 compatible service (Cloudflare R2, MinIO, Backblaze, ...) accept.
    // https://docs.aws.amazon.com/AmazonS3/latest/API/sig-v4-header-based-auth.html
    public static class AwsSignatureV4
    {
        public const string EmptyPayloadHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        public static string HashPayload(byte[] payload)
        {
            return ToHex(SHA256.HashData(payload ?? Array.Empty<byte>()));
        }

        // Path segments are encoded individually, keeping the slashes between them.
        public static string EncodePath(string path)
        {
            return string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        }

        // headers: lowercase header names to values, every header in it is signed.
        public static string GetAuthorizationHeader(string method,
                                                    string canonicalPath,
                                                    IDictionary<string, string> headers,
                                                    string payloadHash,
                                                    string accessKey,
                                                    string secretKey,
                                                    string region,
                                                    DateTime timestamp)
        {
            var amzDate = timestamp.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var dateStamp = timestamp.ToUniversalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            var sortedHeaders = headers.OrderBy(h => h.Key, StringComparer.Ordinal).ToList();
            var canonicalHeaders = string.Concat(sortedHeaders.Select(h => $"{h.Key}:{h.Value.Trim()}\n"));
            var signedHeaders = string.Join(";", sortedHeaders.Select(h => h.Key));

            var canonicalRequest = $"{method}\n{canonicalPath}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
            var scope = $"{dateStamp}/{region}/s3/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

            var signingKey = Hmac(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
            signingKey = Hmac(signingKey, region);
            signingKey = Hmac(signingKey, "s3");
            signingKey = Hmac(signingKey, "aws4_request");

            var signature = ToHex(Hmac(signingKey, stringToSign));

            return $"AWS4-HMAC-SHA256 Credential={accessKey}/{scope},SignedHeaders={signedHeaders},Signature={signature}";
        }

        private static byte[] Hmac(byte[] key, string data)
        {
            return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
        }

        private static string ToHex(byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
