import React, { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarSpacer from 'Components/Page/Toolbar/PageToolbarSpacer';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import { icons, kinds } from 'Helpers/Props';
import PodcastImage from 'Podcasts/PodcastImage';
import { useSortedPodcasts } from 'Podcasts/usePodcasts';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import styles from './PodcastIndex.module.css';

function PodcastIndex() {
  const { data: podcasts, isFetched, error } = useSortedPodcasts();
  const executeCommand = useExecuteCommand();
  const navigate = useNavigate();
  const isRefreshing = useCommandExecuting(CommandNames.RefreshPodcast);
  const isDownloading = useCommandExecuting(
    CommandNames.DownloadPodcastEpisodes
  );

  const handleRefreshPress = useCallback(() => {
    executeCommand({ name: CommandNames.RefreshPodcast });
  }, [executeCommand]);

  const handleAddPress = useCallback(() => {
    navigate('/podcasts/add');
  }, [navigate]);

  const handleDownloadPress = useCallback(() => {
    executeCommand({ name: CommandNames.DownloadPodcastEpisodes });
  }, [executeCommand]);

  return (
    <PageContent title={translate('Podcasts')}>
      <PageToolbar>
        <ToolbarItem
          id="refresh"
          priority={1}
          groupId="left"
          label={translate('RefreshAll')}
          iconName={icons.REFRESH}
          spinningName={icons.REFRESH}
          isSpinning={isRefreshing}
          isDisabled={!podcasts.length}
          onPress={handleRefreshPress}
        />

        <ToolbarItem
          id="download"
          priority={1}
          groupId="left"
          label={translate('DownloadMonitored')}
          iconName={icons.DOWNLOAD}
          isSpinning={isDownloading}
          isDisabled={!podcasts.length}
          onPress={handleDownloadPress}
        />

        <PageToolbarSpacer />

        <ToolbarItem
          id="add"
          priority={1}
          groupId="right"
          label={translate('AddPodcast')}
          iconName={icons.ADD}
          onPress={handleAddPress}
        />
      </PageToolbar>

      <PageContentBody>
        {!isFetched && !error ? <LoadingIndicator /> : null}

        {error ? (
          <Alert kind={kinds.DANGER}>{getErrorMessage(error)}</Alert>
        ) : null}

        {isFetched && !podcasts.length ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('NoPodcastsAdded')}
            </div>
            <div className={styles.emptyBody}>
              {translate('NoPodcastsAddedHelpText')}
            </div>
            <Button to="/podcasts/add" kind={kinds.PRIMARY}>
              {translate('AddPodcast')}
            </Button>
          </div>
        ) : null}

        {podcasts.length ? (
          <div className={styles.grid}>
            {podcasts.map((podcast) => {
              const { downloadedEpisodeCount = 0, episodeCount = 0 } =
                podcast.statistics ?? {};

              return (
                <Link
                  key={podcast.id}
                  className={styles.card}
                  to={`/podcasts/${podcast.id}`}
                >
                  <div className={styles.artwork}>
                    <PodcastImage
                      imageUrl={podcast.imageUrl}
                      title={podcast.title}
                    />

                    {podcast.monitored ? null : (
                      <span className={styles.badge}>
                        {translate('Unmonitored')}
                      </span>
                    )}

                    {podcast.lastRefreshError ? (
                      <span
                        className={styles.errorBadge}
                        title={podcast.lastRefreshError}
                      >
                        {translate('FeedError')}
                      </span>
                    ) : null}
                  </div>

                  <div className={styles.title}>{podcast.title}</div>

                  <div className={styles.meta}>
                    {translate('DownloadedOfTotalEpisodes', {
                      downloaded: downloadedEpisodeCount,
                      total: episodeCount,
                    })}
                  </div>
                </Link>
              );
            })}
          </div>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default PodcastIndex;
