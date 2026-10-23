using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Podcasts.Feed.S3;

namespace NzbDrone.Core.Podcasts.Feed
{
    public interface IPodcastFeedUrlProvider
    {
        string GetFeedBaseUrl(string requestBaseUrl = null);
        string GetEpisodeFeedBaseUrl();
        S3Settings GetS3Settings();
        bool IsS3PublishingEnabled();
        string GetS3Key(int podcastId);
        string GetS3OpmlKey();
        string GetS3PublicFeedUrl(int podcastId);
        string GetS3PublicOpmlUrl();
    }

    public class PodcastFeedUrlProvider : IPodcastFeedUrlProvider
    {
        // Interfaces of containers, VMs and VPNs, a podcast app on the local network can't reach these addresses.
        private static readonly string[] VirtualInterfacePrefixes =
        {
            "docker", "br-", "veth", "virbr", "vmnet", "vboxnet", "lxc", "lxd", "cni", "flannel", "podman", "zt", "tun", "tap", "wg"
        };

        private readonly IConfigService _configService;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly Logger _logger;

        public PodcastFeedUrlProvider(IConfigService configService, IConfigFileProvider configFileProvider, Logger logger)
        {
            _configService = configService;
            _configFileProvider = configFileProvider;
            _logger = logger;
        }

        // Base URL of everything under /feed, the configured external URL or this machine's address on the local network.
        // The address the request came in on is only used when it isn't localhost, since a podcast app can't use that.
        public string GetFeedBaseUrl(string requestBaseUrl = null)
        {
            string baseUrl;

            if (_configService.PodcastFeedBaseUrl.IsNotNullOrWhiteSpace())
            {
                baseUrl = _configService.PodcastFeedBaseUrl.Trim();
            }
            else if (requestBaseUrl.IsNotNullOrWhiteSpace() && !IsLoopbackUrl(requestBaseUrl))
            {
                baseUrl = requestBaseUrl;
            }
            else
            {
                baseUrl = GetLocalNetworkBaseUrl();
            }

            return $"{baseUrl.TrimEnd('/')}/feed/{_configService.PodcastFeedToken}";
        }

        // Episode files in published feeds are always served by this Podarr.
        public string GetEpisodeFeedBaseUrl()
        {
            return GetFeedBaseUrl();
        }

        public S3Settings GetS3Settings()
        {
            return new S3Settings
            {
                Endpoint = _configService.PodcastFeedS3Endpoint,
                Region = _configService.PodcastFeedS3Region,
                Bucket = _configService.PodcastFeedS3Bucket,
                AccessKey = _configService.PodcastFeedS3AccessKey,
                SecretKey = _configService.PodcastFeedS3SecretKey
            };
        }

        public bool IsS3PublishingEnabled()
        {
            return _configService.PodcastFeedsEnabled && _configService.PodcastFeedS3Enabled && GetS3Settings().IsComplete();
        }

        // The secret feed token is part of the path so feed URLs on a public bucket can't be guessed.
        public string GetS3Key(int podcastId)
        {
            return $"{GetS3KeyPrefix()}{_configService.PodcastFeedToken}/{podcastId}.xml";
        }

        public string GetS3OpmlKey()
        {
            return $"{GetS3KeyPrefix()}{_configService.PodcastFeedToken}/podcasts.opml";
        }

        public string GetS3PublicFeedUrl(int podcastId)
        {
            return GetS3PublicUrl(GetS3Key(podcastId));
        }

        public string GetS3PublicOpmlUrl()
        {
            return GetS3PublicUrl(GetS3OpmlKey());
        }

        private string GetS3PublicUrl(string key)
        {
            var publicUrl = _configService.PodcastFeedS3PublicUrl;

            return publicUrl.IsNullOrWhiteSpace() ? null : $"{publicUrl.Trim().TrimEnd('/')}/{key}";
        }

        private string GetS3KeyPrefix()
        {
            var prefix = new string((_configService.PodcastFeedS3Prefix ?? string.Empty)
                .Where(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.')
                .ToArray())
                .Trim('/');

            return prefix.IsNullOrWhiteSpace() ? string.Empty : prefix + "/";
        }

        private string GetLocalNetworkBaseUrl()
        {
            var scheme = _configFileProvider.EnableSsl ? "https" : "http";
            var port = _configFileProvider.EnableSsl ? _configFileProvider.SslPort : _configFileProvider.Port;
            var bindAddress = _configFileProvider.BindAddress;

            var host = bindAddress.IsNotNullOrWhiteSpace() && bindAddress != "*" && IPAddress.TryParse(bindAddress, out var bound) && !IPAddress.IsLoopback(bound)
                ? bindAddress
                : GetLocalNetworkAddress()?.ToString() ?? "localhost";

            return $"{scheme}://{host}:{port}{_configFileProvider.UrlBase}";
        }

        private IPAddress GetLocalNetworkAddress()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel &&
                                !VirtualInterfacePrefixes.Any(p => n.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    .SelectMany(n =>
                    {
                        try
                        {
                            var properties = n.GetIPProperties();
                            var hasGateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

                            return properties.UnicastAddresses
                                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                                .Select(a => (Address: a.Address, HasGateway: hasGateway));
                        }
                        catch (NetworkInformationException)
                        {
                            return Enumerable.Empty<(IPAddress Address, bool HasGateway)>();
                        }
                    })
                    .OrderByDescending(a => a.HasGateway)
                    .ThenByDescending(a => IsPrivateAddress(a.Address))
                    .Select(a => a.Address)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to find the local network address");
                return null;
            }
        }

        private static bool IsPrivateAddress(IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        private static bool IsLoopbackUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return true;
            }

            return uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }
}
