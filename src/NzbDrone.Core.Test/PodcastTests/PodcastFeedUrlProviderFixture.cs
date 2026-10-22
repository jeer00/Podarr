using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Podcasts.Feed;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.PodcastTests
{
    [TestFixture]
    public class PodcastFeedUrlProviderFixture : CoreTest<PodcastFeedUrlProvider>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PodcastFeedToken).Returns("secret");
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PodcastFeedBaseUrl).Returns(string.Empty);
            Mocker.GetMock<IConfigFileProvider>().SetupGet(s => s.Port).Returns(8989);
            Mocker.GetMock<IConfigFileProvider>().SetupGet(s => s.UrlBase).Returns(string.Empty);
            Mocker.GetMock<IConfigFileProvider>().SetupGet(s => s.BindAddress).Returns("*");
        }

        [Test]
        public void should_use_external_url_when_set()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PodcastFeedBaseUrl).Returns("https://pods.example.com/");

            Subject.GetFeedBaseUrl("http://localhost:8989").Should().Be("https://pods.example.com/feed/secret");
        }

        [Test]
        public void should_use_request_address_when_it_is_not_localhost()
        {
            Subject.GetFeedBaseUrl("http://192.168.1.20:8989").Should().Be("http://192.168.1.20:8989/feed/secret");
        }

        [TestCase("http://localhost:8989")]
        [TestCase("http://127.0.0.1:8989")]
        [TestCase(null)]
        public void should_not_use_localhost(string requestBaseUrl)
        {
            Subject.GetFeedBaseUrl(requestBaseUrl).Should().NotContain("localhost").And.NotContain("127.0.0.1").And.EndWith(":8989/feed/secret");
        }

        [Test]
        public void should_use_bind_address_when_bound_to_a_specific_address()
        {
            Mocker.GetMock<IConfigFileProvider>().SetupGet(s => s.BindAddress).Returns("10.0.0.5");

            Subject.GetFeedBaseUrl().Should().Be("http://10.0.0.5:8989/feed/secret");
        }

        [TestCase("", "secret/7.xml")]
        [TestCase("podarr", "podarr/secret/7.xml")]
        [TestCase("/feeds/podarr/", "feeds/podarr/secret/7.xml")]
        [TestCase("bad ../ prefix?", "bad../prefix/secret/7.xml")]
        public void should_build_s3_key_with_token(string prefix, string expected)
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PodcastFeedS3Prefix).Returns(prefix);

            Subject.GetS3Key(7).Should().Be(expected);
        }

        [Test]
        public void should_build_public_bucket_url()
        {
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PodcastFeedS3Prefix).Returns("podarr");
            Mocker.GetMock<IConfigService>().SetupGet(s => s.PodcastFeedS3PublicUrl).Returns("https://pub-abc.r2.dev/");

            Subject.GetS3PublicFeedUrl(7).Should().Be("https://pub-abc.r2.dev/podarr/secret/7.xml");
        }
    }
}
