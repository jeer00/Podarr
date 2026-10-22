using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Podcasts.Feed.S3;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    // Examples from https://docs.aws.amazon.com/AmazonS3/latest/API/sig-v4-header-based-auth.html
    [TestFixture]
    public class AwsSignatureV4Fixture : CoreTest
    {
        private const string AccessKey = "AKIAIOSFODNN7EXAMPLE";
        private const string SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";
        private static readonly DateTime Timestamp = new(2013, 5, 24, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void should_sign_get_object_example()
        {
            var headers = new Dictionary<string, string>
            {
                { "host", "examplebucket.s3.amazonaws.com" },
                { "range", "bytes=0-9" },
                { "x-amz-content-sha256", AwsSignatureV4.EmptyPayloadHash },
                { "x-amz-date", "20130524T000000Z" }
            };

            AwsSignatureV4.GetAuthorizationHeader("GET", "/test.txt", headers, AwsSignatureV4.EmptyPayloadHash, AccessKey, SecretKey, "us-east-1", Timestamp)
                .Should().Be("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41");
        }

        [Test]
        public void should_sign_put_object_example()
        {
            var payloadHash = AwsSignatureV4.HashPayload(Encoding.UTF8.GetBytes("Welcome to Amazon S3."));

            payloadHash.Should().Be("44ce7dd67c959e0d3524ffac1771dfbba87d2b6b4b4e99e42034a8b803f8b072");

            var headers = new Dictionary<string, string>
            {
                { "date", "Fri, 24 May 2013 00:00:00 GMT" },
                { "host", "examplebucket.s3.amazonaws.com" },
                { "x-amz-content-sha256", payloadHash },
                { "x-amz-date", "20130524T000000Z" },
                { "x-amz-storage-class", "REDUCED_REDUNDANCY" }
            };

            AwsSignatureV4.GetAuthorizationHeader("PUT", AwsSignatureV4.EncodePath("/test$file.text"), headers, payloadHash, AccessKey, SecretKey, "us-east-1", Timestamp)
                .Should().EndWith("Signature=98ad721746da40c64f1a55b78f14c238d841ea1380cd77a1b5971af0ece108bd");
        }
    }
}
