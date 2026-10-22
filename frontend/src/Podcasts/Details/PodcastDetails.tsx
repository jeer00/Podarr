import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, kinds } from 'Helpers/Props';
import DeletePodcastModal from 'Podcasts/Delete/DeletePodcastModal';
import EditPodcastModal from 'Podcasts/Edit/EditPodcastModal';
import { Podcast, PodcastDownload, PodcastEpisode } from 'Podcasts/Podcast';
import PodcastImage from 'Podcasts/PodcastImage';
import PodcastSearchModal from 'Podcasts/Search/PodcastSearchModal';
import stripHtml from 'Podcasts/stripHtml';
import { usePodcastFeedUrl } from 'Podcasts/usePodcastFeeds';
import {
  usePodcastDownloads,
  usePodcastEpisodes,
  useSavePodcast,
  useSetEpisodesMonitored,
} from 'Podcasts/usePodcasts';
import formatBytes from 'Utilities/Number/formatBytes';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import PodcastDownloads from './PodcastDownloads';
import PodcastEpisodeRow from './PodcastEpisodeRow';
import styles from './PodcastDetails.module.css';

const PAGE_SIZE = 100;

interface PodcastDetailsProps {
  podcast: Podcast;
}

function PodcastDetails({ podcast }: PodcastDetailsProps) {
  const {
    id,
    title,
    author,
    description,
    imageUrl,
    websiteUrl,
    feedUrl,
    path,
    categories,
    explicit,
    monitored,
    lastRefreshError,
    episodeSource,
    statistics,
  } = podcast;

  const {
    episodeCount = 0,
    downloadedEpisodeCount = 0,
    monitoredEpisodeCount = 0,
    sizeOnDisk = 0,
  } = statistics ?? {};

  const navigate = useNavigate();
  const executeCommand = useExecuteCommand();
  const { savePodcast, isSaving } = useSavePodcast();
  const { setEpisodesMonitored, isSettingMonitored } =
    useSetEpisodesMonitored();
  const { data: episodes, isFetched, error, refetch } = usePodcastEpisodes(id);

  const { data: downloads } = usePodcastDownloads(id);
  const podarrFeedUrl = usePodcastFeedUrl(id);

  const activeDownloadsByEpisode = useMemo(() => {
    return downloads.reduce((acc, download) => {
      if (
        download.podcastEpisodeId &&
        (download.status === 'queued' || download.status === 'downloading')
      ) {
        acc.set(download.podcastEpisodeId, download);
      }

      return acc;
    }, new Map<number, PodcastDownload>());
  }, [downloads]);

  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);
  const [playingEpisodeId, setPlayingEpisodeId] = useState<number | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isSearchModalOpen, setIsSearchModalOpen] = useState(false);
  const [searchEpisode, setSearchEpisode] = useState<
    PodcastEpisode | undefined
  >();

  const commandConstraints = useMemo(() => ({ podcastIds: [id] }), [id]);
  const isRefreshing = useCommandExecuting(
    CommandNames.RefreshPodcast,
    commandConstraints
  );
  const downloadConstraints = useMemo(() => ({ podcastId: id }), [id]);
  const isDownloading = useCommandExecuting(
    CommandNames.DownloadPodcastEpisodes,
    downloadConstraints
  );
  const isSearchingWanted = useCommandExecuting(
    CommandNames.SearchPodcastEpisodes,
    commandConstraints
  );
  const wasRefreshing = usePrevious(isRefreshing);

  useEffect(() => {
    // New episodes are only known after a refresh, the rest arrive over SignalR.
    if (wasRefreshing && !isRefreshing) {
      refetch();
    }
  }, [isRefreshing, wasRefreshing, refetch]);

  const plainDescription = useMemo(() => stripHtml(description), [description]);

  const handleRefreshPress = useCallback(() => {
    executeCommand({ name: CommandNames.RefreshPodcast, podcastIds: [id] });
  }, [id, executeCommand]);

  const handleDownloadPress = useCallback(() => {
    executeCommand({
      name: CommandNames.DownloadPodcastEpisodes,
      podcastId: id,
    });
  }, [id, executeCommand]);

  const handleMonitorTogglePress = useCallback(
    (value: boolean) => {
      savePodcast({ ...podcast, monitored: value });
    },
    [podcast, savePodcast]
  );

  const handleMonitorAllPress = useCallback(() => {
    setEpisodesMonitored({
      episodeIds: episodes.map((e) => e.id),
      monitored: true,
    });
  }, [episodes, setEpisodesMonitored]);

  const handleUnmonitorAllPress = useCallback(() => {
    setEpisodesMonitored({
      episodeIds: episodes.map((e) => e.id),
      monitored: false,
    });
  }, [episodes, setEpisodesMonitored]);

  const handleEditPress = useCallback(() => {
    setIsEditModalOpen(true);
  }, []);

  const handleEditModalClose = useCallback(() => {
    setIsEditModalOpen(false);
  }, []);

  const handleSearchWantedPress = useCallback(() => {
    executeCommand({
      name: CommandNames.SearchPodcastEpisodes,
      podcastIds: [id],
    });
  }, [id, executeCommand]);

  const handleSearchPress = useCallback(() => {
    setSearchEpisode(undefined);
    setIsSearchModalOpen(true);
  }, []);

  const handleEpisodeSearchPress = useCallback((episode: PodcastEpisode) => {
    setSearchEpisode(episode);
    setIsSearchModalOpen(true);
  }, []);

  const handleSearchModalClose = useCallback(() => {
    setIsSearchModalOpen(false);
  }, []);

  const handleDeletePress = useCallback(() => {
    setIsDeleteModalOpen(true);
  }, []);

  const handleShowMorePress = useCallback(() => {
    setVisibleCount((count) => count + PAGE_SIZE);
  }, []);

  const handleDeleteModalClose = useCallback(
    (wasDeleted?: boolean) => {
      setIsDeleteModalOpen(false);

      if (wasDeleted) {
        navigate('/podcasts');
      }
    },
    [navigate]
  );

  return (
    <PageContent title={title}>
      <PageToolbar>
        <ToolbarItem
          id="refresh"
          priority={1}
          groupId="left"
          label={translate('Refresh')}
          iconName={icons.REFRESH}
          spinningName={icons.REFRESH}
          isSpinning={isRefreshing}
          onPress={handleRefreshPress}
        />

        <ToolbarItem
          id="download"
          priority={1}
          groupId="left"
          label={translate('DownloadMonitored')}
          iconName={icons.DOWNLOAD}
          isSpinning={isDownloading}
          isDisabled={!monitored}
          onPress={handleDownloadPress}
        />

        <ToolbarItem
          id="search-trackers"
          priority={1}
          groupId="left"
          label={translate('SearchTrackers')}
          iconName={icons.SEARCH}
          onPress={handleSearchPress}
        />

        <ToolbarItem
          id="search-wanted"
          priority={2}
          groupId="left"
          label={translate('FindFullEpisodes')}
          iconName={icons.DOWNLOADING}
          isSpinning={isSearchingWanted}
          title={translate('FindFullEpisodesHelpText')}
          onPress={handleSearchWantedPress}
        />

        <PageToolbarSpacer />

        <ToolbarItem
          id="edit"
          priority={1}
          groupId="right"
          label={translate('Edit')}
          iconName={icons.EDIT}
          onPress={handleEditPress}
        />

        <ToolbarItem
          id="delete"
          priority={1}
          groupId="right"
          label={translate('Delete')}
          iconName={icons.DELETE}
          onPress={handleDeletePress}
        />
      </PageToolbar>

      <PageContentBody innerClassName={styles.innerContentBody}>
        <div className={styles.header}>
          {imageUrl ? (
            <div
              className={styles.backdrop}
              style={{ backgroundImage: `url("${encodeURI(imageUrl)}")` }}
            />
          ) : null}
          <div className={styles.backdropOverlay} />

          <div className={styles.headerContent}>
            <div className={styles.artwork}>
              <PodcastImage imageUrl={imageUrl} title={title} lazy={false} />
            </div>

            <div className={styles.info}>
              <div className={styles.titleRow}>
                <MonitorToggleButton
                  className={styles.monitorToggle}
                  monitored={monitored}
                  isSaving={isSaving}
                  size={28}
                  onPress={handleMonitorTogglePress}
                />

                <div className={styles.title}>{title}</div>
              </div>

              {author ? <div className={styles.author}>{author}</div> : null}

              <div className={styles.chips}>
                <span className={styles.chip}>
                  <Icon name={icons.DOWNLOADED} size={13} />
                  {translate('DownloadedOfTotalEpisodes', {
                    downloaded: downloadedEpisodeCount,
                    total: episodeCount,
                  })}
                </span>

                <span className={styles.chip}>
                  <Icon name={icons.MONITORED} size={13} />
                  {translate('CountMonitored', {
                    count: monitoredEpisodeCount,
                  })}
                </span>

                <span className={styles.chip}>
                  <Icon name={icons.DRIVE} size={13} />
                  {formatBytes(sizeOnDisk)}
                </span>

                {categories.map((category) => (
                  <span key={category} className={styles.chip}>
                    {category}
                  </span>
                ))}

                {explicit ? (
                  <span className={styles.chip}>{translate('Explicit')}</span>
                ) : null}
              </div>

              <div className={styles.links}>
                <span className={styles.path} title={path}>
                  <Icon name={icons.FOLDER} size={12} />
                  {path}
                </span>

                {websiteUrl ? (
                  <Link to={websiteUrl}>
                    <Icon name={icons.EXTERNAL_LINK} size={12} />{' '}
                    {translate('Website')}
                  </Link>
                ) : null}

                <Link to={feedUrl}>
                  <Icon name={icons.RSS} size={12} /> {translate('Feed')}
                </Link>

                {podarrFeedUrl ? (
                  <Link
                    to={podarrFeedUrl}
                    title={translate('PodarrFeedTooltip')}
                  >
                    <Icon name={icons.RSS} size={12} />{' '}
                    {translate('PodarrFeed')}
                  </Link>
                ) : null}
              </div>
            </div>
          </div>
        </div>

        <div className={styles.contentContainer}>
          {lastRefreshError ? (
            <Alert kind={kinds.WARNING}>
              {translate('PodcastFeedError', { error: lastRefreshError })}
            </Alert>
          ) : null}

          {plainDescription ? (
            <p className={styles.description}>{plainDescription}</p>
          ) : null}

          <PodcastDownloads podcastId={id} />

          <div className={styles.episodesHeader}>
            <div className={styles.episodesTitle}>
              {translate('Episodes')}
              <span className={styles.episodesCount}>{episodes.length}</span>
            </div>

            <div className={styles.episodesActions}>
              <Button
                isDisabled={!episodes.length || isSettingMonitored}
                onPress={handleMonitorAllPress}
              >
                {translate('MonitorAll')}
              </Button>
              <Button
                isDisabled={!episodes.length || isSettingMonitored}
                onPress={handleUnmonitorAllPress}
              >
                {translate('UnmonitorAll')}
              </Button>
            </div>
          </div>

          {!isFetched && !error ? <LoadingIndicator /> : null}

          {error ? (
            <Alert kind={kinds.DANGER}>{getErrorMessage(error)}</Alert>
          ) : null}

          {isFetched && !episodes.length ? (
            <Alert kind={kinds.INFO}>{translate('NoPodcastEpisodes')}</Alert>
          ) : null}

          <div className={styles.episodes}>
            {episodes.slice(0, visibleCount).map((episode) => (
              <PodcastEpisodeRow
                key={episode.id}
                episode={episode}
                episodeSource={episodeSource}
                activeDownload={activeDownloadsByEpisode.get(episode.id)}
                isPodcastMonitored={monitored}
                isPlaying={playingEpisodeId === episode.id}
                onPlayPress={setPlayingEpisodeId}
                onSearchPress={handleEpisodeSearchPress}
              />
            ))}
          </div>

          {episodes.length > visibleCount ? (
            <div className={styles.showMore}>
              <Button onPress={handleShowMorePress}>
                {translate('ShowMoreCount', {
                  count: Math.min(PAGE_SIZE, episodes.length - visibleCount),
                })}
              </Button>
            </div>
          ) : null}
        </div>
      </PageContentBody>

      <PodcastSearchModal
        isOpen={isSearchModalOpen}
        podcast={podcast}
        episode={searchEpisode}
        onModalClose={handleSearchModalClose}
      />

      <EditPodcastModal
        isOpen={isEditModalOpen}
        podcast={podcast}
        onModalClose={handleEditModalClose}
      />

      <DeletePodcastModal
        isOpen={isDeleteModalOpen}
        podcast={podcast}
        onModalClose={handleDeleteModalClose}
      />
    </PageContent>
  );
}

export default PodcastDetails;
