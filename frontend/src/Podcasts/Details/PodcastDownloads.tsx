import React, { useCallback, useState } from 'react';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ProgressBar from 'Components/ProgressBar';
import { icons, kinds, sizes } from 'Helpers/Props';
import { PodcastDownload, PodcastDownloadStatus } from 'Podcasts/Podcast';
import {
  usePodcastDownloads,
  useReimportPodcastDownload,
  useRemovePodcastDownload,
} from 'Podcasts/usePodcasts';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import styles from './PodcastDownloads.module.css';

const COLLAPSED_COUNT = 5;

const statusKinds: Record<
  PodcastDownloadStatus,
  'info' | 'primary' | 'success' | 'danger'
> = {
  queued: kinds.INFO,
  downloading: kinds.PRIMARY,
  imported: kinds.SUCCESS,
  failed: kinds.DANGER,
};

const statusLabels: Record<PodcastDownloadStatus, () => string> = {
  queued: () => translate('Queued'),
  downloading: () => translate('Downloading'),
  imported: () => translate('Imported'),
  failed: () => translate('Failed'),
};

function PodcastDownloadRow({ download }: { download: PodcastDownload }) {
  const { id, title, indexer, size, status, progress, message } = download;
  const { removeDownload, isRemoving } = useRemovePodcastDownload(id);
  const { reimportDownload, isReimporting } = useReimportPodcastDownload(id);
  const isActive = status === 'queued' || status === 'downloading';

  const handleRemovePress = useCallback(() => {
    removeDownload({ removeFromClient: false });
  }, [removeDownload]);

  const handleReimportPress = useCallback(() => {
    reimportDownload();
  }, [reimportDownload]);

  return (
    <div className={styles.row}>
      <div className={styles.info}>
        <div className={styles.title} title={title}>
          {title}
        </div>
        <div className={styles.meta}>
          <Label kind={statusKinds[status]} size={sizes.SMALL}>
            {statusLabels[status]()}
          </Label>
          {indexer ? <span>{indexer}</span> : null}
          <span>{formatBytes(size)}</span>
          {message ? (
            <span className={status === 'failed' ? styles.error : undefined}>
              {message}
            </span>
          ) : null}
        </div>

        {isActive ? (
          <ProgressBar
            containerClassName={styles.progress}
            progress={progress}
            kind={kinds.PRIMARY}
            size={sizes.SMALL}
            showText={true}
          />
        ) : null}
      </div>

      {isActive ? null : (
        <IconButton
          name={icons.REFRESH}
          title={translate('ImportAgain')}
          isDisabled={isReimporting}
          onPress={handleReimportPress}
        />
      )}

      <IconButton
        name={icons.REMOVE}
        title={translate('RemoveFromPodarrHelpText')}
        isDisabled={isRemoving}
        onPress={handleRemovePress}
      />
    </div>
  );
}

interface PodcastDownloadsProps {
  podcastId: number;
}

function PodcastDownloads({ podcastId }: PodcastDownloadsProps) {
  const { data } = usePodcastDownloads(podcastId);
  const [isExpanded, setIsExpanded] = useState(false);

  const handleToggleExpand = useCallback(() => {
    setIsExpanded((expanded) => !expanded);
  }, []);

  if (!data.length) {
    return null;
  }

  const visible = isExpanded ? data : data.slice(0, COLLAPSED_COUNT);

  return (
    <div className={styles.container}>
      <div className={styles.heading}>{translate('TrackerDownloads')}</div>

      {visible.map((download) => (
        <PodcastDownloadRow key={download.id} download={download} />
      ))}

      {data.length > COLLAPSED_COUNT ? (
        <button
          className={styles.toggle}
          type="button"
          onClick={handleToggleExpand}
        >
          {isExpanded
            ? translate('ShowLess')
            : translate('ShowMoreCount', {
                count: data.length - COLLAPSED_COUNT,
              })}
        </button>
      ) : null}
    </div>
  );
}

export default PodcastDownloads;
