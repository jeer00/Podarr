import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Icon from 'Components/Icon';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import { icons, kinds } from 'Helpers/Props';
import formatDuration from 'Podcasts/formatDuration';
import {
  PodcastDownload,
  PodcastEpisode,
  PodcastEpisodeSource,
} from 'Podcasts/Podcast';
import stripHtml from 'Podcasts/stripHtml';
import {
  useDeleteEpisodeFile,
  useSetEpisodesMonitored,
} from 'Podcasts/usePodcasts';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import formatDate from 'Utilities/Date/formatDate';
import getQueryPath from 'Utilities/Fetch/getQueryPath';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './PodcastEpisodeRow.module.css';

interface PodcastEpisodeRowProps {
  episode: PodcastEpisode;
  episodeSource: PodcastEpisodeSource;
  activeDownload?: PodcastDownload;
  isPodcastMonitored: boolean;
  isPlaying: boolean;
  onPlayPress: (episodeId: number | null) => void;
  onSearchPress: (episode: PodcastEpisode) => void;
}

function PodcastEpisodeRow({
  episode,
  episodeSource,
  activeDownload,
  isPodcastMonitored,
  isPlaying,
  onPlayPress,
  onSearchPress,
}: PodcastEpisodeRowProps) {
  const {
    id,
    title,
    description,
    airDateUtc,
    seasonNumber,
    episodeNumber,
    episodeType,
    duration,
    enclosureUrl,
    monitored,
    hasFile,
    fileSource,
    size,
    lastDownloadError,
  } = episode;

  const { shortDateFormat } = useUiSettingsValues();
  const executeCommand = useExecuteCommand();
  const { setEpisodesMonitored, isSettingMonitored } =
    useSetEpisodesMonitored();
  const { deleteEpisodeFile, isDeletingFile } = useDeleteEpisodeFile(id);
  const [isExpanded, setIsExpanded] = useState(false);
  const [isConfirmDeleteOpen, setIsConfirmDeleteOpen] = useState(false);

  const downloadConstraints = useMemo(() => ({ episodeIds: [id] }), [id]);
  const isDownloading = useCommandExecuting(
    CommandNames.DownloadPodcastEpisodes,
    downloadConstraints
  );

  const plainDescription = useMemo(
    () => (isExpanded ? stripHtml(description) : ''),
    [description, isExpanded]
  );

  const handleMonitorPress = useCallback(
    (value: boolean) => {
      setEpisodesMonitored({ episodeIds: [id], monitored: value });
    },
    [id, setEpisodesMonitored]
  );

  const handleDownloadPress = useCallback(() => {
    executeCommand({
      name: CommandNames.DownloadPodcastEpisodes,
      episodeIds: [id],
    });
  }, [id, executeCommand]);

  const handleDeleteFilePress = useCallback(() => {
    setIsConfirmDeleteOpen(true);
  }, []);

  const handleConfirmDeleteCancel = useCallback(() => {
    setIsConfirmDeleteOpen(false);
  }, []);

  const handleConfirmDelete = useCallback(() => {
    setIsConfirmDeleteOpen(false);
    deleteEpisodeFile();
  }, [deleteEpisodeFile]);

  const handleSearchPress = useCallback(() => {
    onSearchPress(episode);
  }, [episode, onSearchPress]);

  const handlePlayPress = useCallback(() => {
    onPlayPress(isPlaying ? null : id);
  }, [id, isPlaying, onPlayPress]);

  const handleTitlePress = useCallback(() => {
    setIsExpanded((expanded) => !expanded);
  }, []);

  const numbering = [
    seasonNumber ? `S${seasonNumber}` : '',
    episodeNumber ? `E${episodeNumber}` : '',
  ].join('');

  const isWaitingForTracker =
    monitored && episodeSource === 'tracker' && !hasFile && !activeDownload;

  let status = (
    <span className={styles.statusMissing}>{translate('NotDownloaded')}</span>
  );

  if (activeDownload) {
    status = (
      <span className={styles.statusDownloading} title={activeDownload.title}>
        {activeDownload.status === 'queued'
          ? translate('Queued')
          : translate('DownloadingProgress', {
              progress: Math.round(activeDownload.progress),
            })}
      </span>
    );
  } else if (hasFile && fileSource === 'tracker') {
    status = (
      <span
        className={styles.statusDownloaded}
        title={translate('TrackerVersionTooltip')}
      >
        <span className={styles.sourceBadge}>{translate('Tracker')}</span>
        {formatBytes(size)}
      </span>
    );
  } else if (hasFile) {
    status = (
      <span className={styles.statusDownloaded}>{formatBytes(size)}</span>
    );
  } else if (isWaitingForTracker) {
    status = (
      <span className={styles.statusMissing}>
        {translate('WaitingForTracker')}
      </span>
    );
  } else if (lastDownloadError) {
    status = (
      <span className={styles.statusError} title={lastDownloadError}>
        <Icon name={icons.WARNING} size={12} /> {translate('DownloadFailed')}
      </span>
    );
  } else if (!monitored) {
    status = (
      <span className={styles.statusUnmonitored}>
        {translate('Unmonitored')}
      </span>
    );
  }

  const audioUrl = `${getQueryPath(
    `/podcastEpisode/${id}/audio`
  )}?apikey=${encodeURIComponent(window.Sonarr.apiKey)}`;

  return (
    <div className={classNames(styles.row, !monitored && styles.unmonitored)}>
      <div className={styles.main}>
        <MonitorToggleButton
          className={styles.monitorToggle}
          monitored={monitored}
          isDisabled={!isPodcastMonitored}
          isSaving={isSettingMonitored}
          onPress={handleMonitorPress}
        />

        <div className={styles.details}>
          <Link className={styles.title} onPress={handleTitlePress}>
            {title}
          </Link>

          <div className={styles.meta}>
            {airDateUtc ? (
              <span>{formatDate(airDateUtc, shortDateFormat)}</span>
            ) : null}
            {numbering ? <span>{numbering}</span> : null}
            {duration ? <span>{formatDuration(duration)}</span> : null}
            {episodeType && episodeType !== 'full' ? (
              <span className={styles.episodeType}>{episodeType}</span>
            ) : null}
          </div>
        </div>

        <div className={styles.status}>{status}</div>

        <div className={styles.actions}>
          <IconButton
            name={icons.SEARCH}
            title={translate('SearchTrackers')}
            size={14}
            onPress={handleSearchPress}
          />

          {hasFile ? (
            <>
              <IconButton
                name={isPlaying ? icons.PAUSED : icons.PLAY}
                title={translate('Play')}
                size={14}
                onPress={handlePlayPress}
              />
              <SpinnerIconButton
                name={icons.DELETE}
                kind={kinds.DANGER}
                title={translate('DeleteEpisodeFile')}
                size={14}
                isSpinning={isDeletingFile}
                onPress={handleDeleteFilePress}
              />
            </>
          ) : (
            <SpinnerIconButton
              name={icons.DOWNLOAD}
              title={translate('Download')}
              size={14}
              isDisabled={!enclosureUrl}
              isSpinning={isDownloading}
              onPress={handleDownloadPress}
            />
          )}
        </div>
      </div>

      {isPlaying && hasFile ? (
        <audio
          className={styles.player}
          src={audioUrl}
          controls={true}
          autoPlay={true}
          preload="metadata"
        />
      ) : null}

      {isExpanded && plainDescription ? (
        <p className={styles.description}>{plainDescription}</p>
      ) : null}

      <ConfirmModal
        isOpen={isConfirmDeleteOpen}
        kind={kinds.DANGER}
        title={translate('DeleteEpisodeFile')}
        message={translate('DeleteEpisodeFileConfirmation', { title })}
        confirmLabel={translate('Delete')}
        onConfirm={handleConfirmDelete}
        onCancel={handleConfirmDeleteCancel}
      />
    </div>
  );
}

export default PodcastEpisodeRow;
