# Podarr

Podarr manages a local podcast library. Find podcasts by name or RSS URL, download episodes from their feeds or through a tracker, and subscribe to your downloaded episodes in a podcast app.

This is a podcast-focused fork of [Sonarr](https://github.com/Sonarr/Sonarr). The backend, web interface, indexer integrations and download clients come from Sonarr; many project names, namespaces and executable names still use `Sonarr` or `NzbDrone`.

## Features

- Search the Apple Podcasts directory, paste an Apple Podcasts link, or add an RSS feed directly.
- Monitor all episodes, future episodes, the latest episode, or none.
- Download media directly from RSS enclosures.
- Search configured indexers and send releases to a download client such as qBittorrent. Jackett can provide Torznab indexers.
- Choose feed downloads, tracker downloads, or feed downloads followed by a tracker upgrade for each podcast.
- Import tracker files into the library using hardlinks when possible, with a copy as the fallback. Completed torrents can keep seeding.
- Serve RSS feeds and an OPML subscription list for downloaded episodes, with support for media range requests.
- Optionally publish feed XML and OPML to S3-compatible storage, including Cloudflare R2. Media files stay on the Podarr server.

Podcast feeds refresh hourly. Download clients are checked for completed imports every minute; scheduled work can take longer when other tasks are running.

## Build and run

The instructions below are for Linux x64. This fork currently uses source builds; the inherited Docker and distribution files are Sonarr packaging infrastructure, rather than a ready-to-run Podarr deployment.

### Requirements

- The .NET SDK version in [global.json](global.json).
- Node.js and Yarn Classic, using the versions pinned in [package.json](package.json).
- A writable application data directory and a separate podcast library directory.
- For tracker downloads: an indexer, a download client, and access to the completed download files from Podarr.

### Compile

Run these commands from the repository root, on the machine you want to use for builds:

```sh
yarn install --frozen-lockfile
yarn build

dotnet build src/NzbDrone.Console/Sonarr.Console.csproj \
  -c Debug -p:RuntimeIdentifiers=linux-x64 \
  -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false
```

The frontend is written to `_output/UI`; the backend is written to `_output/net10.0`. Keep both directories together when copying the build to another machine. This is a framework-dependent build, so the destination also needs the .NET 10 runtime and its operating system dependencies.

The analyzer properties above work around existing StyleCop failures in the inherited source. They disable build analyzers for this invocation; omit them to run those checks. Other operating systems and architectures need their corresponding runtime identifier and have not been verified by these instructions.

### Start Podarr

```sh
mkdir -p data
dotnet _output/net10.0/Sonarr.dll -nobrowser -data="$PWD/data"
```

Open `http://localhost:8989`. On a fresh installation with forms authentication, Podarr creates an initial user and logs the generated password. Change the credentials under **Settings → General** after signing in.

The `-data` argument keeps application state in an explicit location. Without it, Podarr uses its own `Podarr` application data directory, typically `~/.config/Podarr` on Linux. Database and executable filenames still use Sonarr names. Keep a Podarr data directory separate from any existing Sonarr installation.

## Configure your library

1. Add a writable library folder under **Settings → Root Folders**.
2. For tracker downloads, configure and test your indexers under **Settings → Indexers** and your client under **Settings → Download Clients**. Use a dedicated category such as `podarr`.
3. Add a podcast by searching its name or pasting its feed URL or Apple Podcasts link.
4. Choose its library folder, episode source and monitoring policy. Search for tracker releases or download episodes from the feed as needed.

| Episode source | Behavior |
| --- | --- |
| Feed | Download audio or video from the podcast's RSS feed. |
| Feed and tracker | Download from the feed, then replace that file when a matching tracker release becomes available. |
| Tracker | Use the feed as the episode catalogue and download media through trackers. |

Podarr and the download client must agree on where completed files live. In Docker, mount the download directory into both containers at the same path, for example `/downloads`, and mount the library separately at `/podcasts`. The Podarr user needs permission to read downloads and write to the library. Use a remote path mapping if the client reports a different path.

Removing an entry from Podarr can leave its torrent in the client. Grabbing the same torrent again reuses the existing client item when Podarr can confirm its hash, allowing the files to be imported for the current podcast entry.

## Subscribe in a podcast app

Enable **Serve Podcast Feeds** under **Settings → Podcast Feeds**, then copy a podcast's generated feed link or import the OPML list.

The generated feed contains downloaded episodes. Once a podcast has tracker episodes, its generated feed lists those episodes and omits feed previews.

### Set the address used inside RSS

Set **External URL** to the address your podcast app uses to reach Podarr, for example:

```text
https://podcasts.example.com
```

This setting controls the episode media links inside the XML as well as Podarr's own feed links. It is especially useful behind a reverse proxy or in Docker, where automatic address detection can pick an internal container address. Leave it blank to use automatic address detection.

Changing this field does not configure DNS, TLS, a reverse proxy or a VPN. Configure those separately so the chosen address reaches Podarr.

### Private access with Tailscale

Podarr can stay private behind Tailscale. Make the chosen hostname resolve to the service's Tailscale address and route HTTPS through a reverse proxy reachable on that network. Set the same HTTPS address as the External URL.

The phone or computer fetching the media must be connected to the tailnet and allowed to reach the service. A service used this way does not need to be an exit node. Keep the backend reachable only on the intended private network; a domain name and HTTPS certificate alone do not restrict access.

### Optional S3 or R2 publishing

Under **Settings → Podcast Feeds**, enable **Upload Feeds to S3** and enter the endpoint, bucket, access key, secret key and public bucket URL. Set the region and object prefix as required by the storage provider. Test the connection, save, and publish the feeds.

Podarr uploads only the RSS XML and OPML. Episode media URLs continue to point at the configured External URL. A public feed XML file can therefore reference media that is only reachable through Tailscale.

Public bucket objects are accessible to anyone who has their URLs. Those XML files include podcast metadata and token-bearing media links. Treat subscription URLs as credentials, even when network access to the media is private.

## Data and backups

Preserve the complete application data directory when moving or backing up Podarr. It contains `config.xml`, `sonarr.db`, authentication state and keys, indexer and download client settings, feed tokens, and storage credentials. Also back up the library and any download client state needed to resume or seed torrents.

Stop Podarr before copying its data directory, or use a consistent SQLite backup for a live database. Copying only `sonarr.db` while it is running can miss changes in the database's WAL file. Preserve ownership and permissions when restoring.

Jackett and qBittorrent have their own configuration, credentials and state. Back them up separately from Podarr, including Jackett's indexer configuration and data protection keys, and qBittorrent's torrent and resume data. A container image alone does not contain mounted application state.

## Development

Build the backend as above, then run the frontend dev server in another terminal:

```sh
yarn start
```

Keep using the Podarr backend at `http://localhost:8989`; its development middleware uses Vite on port `8959`. Frontend output can also be rebuilt with `yarn build`.

Available checks:

```sh
yarn typecheck
yarn lint
yarn stylelint

dotnet test src/NzbDrone.Core.Test/Sonarr.Core.Test.csproj \
  -c Debug -p:RuntimeIdentifiers=linux-x64 \
  -p:RunAnalyzers=false -p:RunAnalyzersDuringBuild=false \
  --filter 'FullyQualifiedName~PodcastTests'
```

Podcast code lives in `src/NzbDrone.Core/Podcasts`, API controllers in `src/Sonarr.Api.V5/Podcasts`, and UI components in `frontend/src/Podcasts`. Podcast regression tests live in `src/NzbDrone.Core.Test/PodcastTests`. API routes use `/api/v5` and require the configured API key.

### Keep deployment data out of commits

Configure hostnames, addresses and credentials through the running application or private deployment files. Use reserved example domains and clearly fake values in documentation and tests.

The repository ignores local environment files, application databases, keys, deployment data directories, media, build output and `_artifacts`. Migration backups under `_artifacts` can contain live credentials and must remain private. Git ignore rules do not remove files already tracked or prevent force-adding them. Review both `git status --short` and `git diff --cached` before committing.

## Contributing and license

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [AGENTS.md](AGENTS.md) before submitting changes. They retain the upstream project's contribution and human review requirements.

Podarr is licensed under [GNU GPL v3](LICENSE.md) and retains Sonarr's copyright and license notices. See [COPYRIGHT.md](COPYRIGHT.md).
