import moment from 'moment-timezone';
import React, { useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import CheckInput from 'Components/Form/CheckInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, kinds } from 'Helpers/Props';
import { Podcast, PodcastEpisode, PodcastRelease } from 'Podcasts/Podcast';
import {
  useGrabPodcastRelease,
  usePodcastReleases,
} from 'Podcasts/usePodcasts';
import { CheckInputChanged } from 'typings/inputs';
import formatAge from 'Utilities/Number/formatAge';
import formatBytes from 'Utilities/Number/formatBytes';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import styles from './PodcastSearchModal.module.css';

interface ReleaseRowProps {
  release: PodcastRelease;
  showEpisodeMatch: boolean;
  isGrabbing: boolean;
  isGrabbed: boolean;
  onGrabPress: (release: PodcastRelease) => void;
}

function ReleaseRow({
  release,
  showEpisodeMatch,
  isGrabbing,
  isGrabbed,
  onGrabPress,
}: ReleaseRowProps) {
  const { title, indexer, size, seeders, leechers, publishDate, infoUrl } =
    release;

  const ageMinutes = moment().diff(moment(publishDate), 'minutes');

  const handleGrabPress = useCallback(() => {
    onGrabPress(release);
  }, [release, onGrabPress]);

  return (
    <tr className={styles.row}>
      <td className={styles.titleCell}>
        {infoUrl ? (
          <Link className={styles.title} to={infoUrl}>
            {title}
          </Link>
        ) : (
          <span className={styles.title}>{title}</span>
        )}

        {showEpisodeMatch && release.episodeScore >= 60 ? (
          <span className={styles.matchBadge}>
            {translate('MatchesEpisode')}
          </span>
        ) : null}
      </td>
      <td className={styles.cell}>{indexer}</td>
      <td className={styles.cell}>
        {formatAge(ageMinutes / 1440, ageMinutes / 60, ageMinutes)}
      </td>
      <td className={styles.cell}>{formatBytes(size)}</td>
      <td className={styles.cell}>
        {seeders === undefined || seeders === null
          ? '-'
          : `${seeders} / ${leechers ?? 0}`}
      </td>
      <td className={styles.actionCell}>
        {isGrabbed ? (
          <Icon
            name={icons.CHECK_CIRCLE}
            kind={kinds.SUCCESS}
            title={translate('Grabbed')}
          />
        ) : (
          <SpinnerIconButton
            name={icons.DOWNLOAD}
            title={translate('Grab')}
            isSpinning={isGrabbing}
            onPress={handleGrabPress}
          />
        )}
      </td>
    </tr>
  );
}

interface PodcastSearchModalContentProps {
  podcast: Podcast;
  episode?: PodcastEpisode;
  onModalClose: () => void;
}

function PodcastSearchModalContent({
  podcast,
  episode,
  onModalClose,
}: PodcastSearchModalContentProps) {
  const [showAll, setShowAll] = useState(false);
  const [grabbedGuids, setGrabbedGuids] = useState<Set<string>>(new Set());
  const { data, isFetching, error, refetch } = usePodcastReleases(
    podcast.id,
    episode?.id,
    true
  );
  const { grabRelease, grabbingGuid, grabError } = useGrabPodcastRelease();

  const releases = useMemo(
    () => (showAll ? data : data.filter((r) => r.matchesPodcast)),
    [data, showAll]
  );

  const hiddenCount = data.length - releases.length;

  const handleShowAllChange = useCallback(({ value }: CheckInputChanged) => {
    setShowAll(value);
  }, []);

  const handleGrabPress = useCallback(
    (release: PodcastRelease) => {
      grabRelease(
        {
          podcastId: podcast.id,
          episodeId: episode?.id,
          indexerId: release.indexerId,
          guid: release.guid,
        },
        {
          onSuccess: () => {
            setGrabbedGuids((guids) => new Set(guids).add(release.guid));
          },
        }
      );
    },
    [podcast.id, episode?.id, grabRelease]
  );

  const handleSearchAgainPress = useCallback(() => {
    refetch();
  }, [refetch]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {episode
          ? translate('SearchTrackersForEpisode', { title: episode.title })
          : translate('SearchTrackersForPodcast', { title: podcast.title })}
      </ModalHeader>

      <ModalBody>
        <div className={styles.searchInfo}>
          {translate('TrackerSearchQuery', {
            query: (podcast.trackerSearchTerms ?? [podcast.title])
              .map((term) => `"${term}"`)
              .join(', '),
          })}
        </div>

        {grabError ? (
          <Alert kind={kinds.DANGER}>{getErrorMessage(grabError)}</Alert>
        ) : null}

        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>{getErrorMessage(error)}</Alert>
        ) : null}

        {!isFetching && !error && !data.length ? (
          <Alert kind={kinds.INFO}>{translate('NoTrackerResults')}</Alert>
        ) : null}

        {!isFetching && !error && data.length ? (
          <>
            <div className={styles.filter}>
              <CheckInput
                name="showAll"
                value={showAll}
                helpText={translate('ShowNonMatchingReleases', {
                  count: hiddenCount,
                })}
                onChange={handleShowAllChange}
              />
            </div>

            <div className={styles.tableContainer}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th className={styles.header}>{translate('Title')}</th>
                    <th className={styles.header}>{translate('Indexer')}</th>
                    <th className={styles.header}>{translate('Age')}</th>
                    <th className={styles.header}>{translate('Size')}</th>
                    <th className={styles.header}>{translate('Peers')}</th>
                    <th className={styles.header} />
                  </tr>
                </thead>
                <tbody>
                  {releases.map((release) => (
                    <ReleaseRow
                      key={`${release.indexerId}-${release.guid}`}
                      release={release}
                      showEpisodeMatch={!!episode}
                      isGrabbing={grabbingGuid === release.guid}
                      isGrabbed={
                        release.isGrabbed || grabbedGuids.has(release.guid)
                      }
                      onGrabPress={handleGrabPress}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button isDisabled={isFetching} onPress={handleSearchAgainPress}>
          {translate('SearchAgain')}
        </Button>
        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

interface PodcastSearchModalProps extends PodcastSearchModalContentProps {
  isOpen: boolean;
}

function PodcastSearchModal({
  isOpen,
  onModalClose,
  ...otherProps
}: PodcastSearchModalProps) {
  return (
    <Modal isOpen={isOpen} size="extraLarge" onModalClose={onModalClose}>
      {isOpen ? (
        <PodcastSearchModalContent
          {...otherProps}
          onModalClose={onModalClose}
        />
      ) : null}
    </Modal>
  );
}

export default PodcastSearchModal;
