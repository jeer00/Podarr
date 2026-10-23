import React, { useCallback, useEffect, useState } from 'react';
import Form from 'Components/Form/Form';
import FormInput from 'Components/Form/FormInput';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { inputTypes, kinds } from 'Helpers/Props';
import episodeSourceOptions from 'Podcasts/episodeSourceOptions';
import { Podcast, PodcastEpisodeSource } from 'Podcasts/Podcast';
import { useSavePodcast } from 'Podcasts/usePodcasts';
import { CheckInputChanged, InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import styles from './EditPodcastModal.module.css';

interface EditPodcastModalContentProps {
  podcast: Podcast;
  onModalClose: () => void;
}

function EditPodcastModalContent({
  podcast,
  onModalClose,
}: EditPodcastModalContentProps) {
  const [monitored, setMonitored] = useState(podcast.monitored);
  const [monitorNewEpisodes, setMonitorNewEpisodes] = useState(
    podcast.monitorNewEpisodes
  );
  const [episodeSource, setEpisodeSource] = useState<PodcastEpisodeSource>(
    podcast.episodeSource
  );
  const [trackerSearchTerm, setTrackerSearchTerm] = useState(
    podcast.trackerSearchTerm ?? ''
  );
  const [grabTrackerReleases, setGrabTrackerReleases] = useState(
    podcast.grabTrackerReleases
  );
  const { savePodcast, isSaving, saveError } = useSavePodcast();
  const wasSaving = usePrevious(isSaving);

  useEffect(() => {
    if (wasSaving && !isSaving && !saveError) {
      onModalClose();
    }
  }, [isSaving, wasSaving, saveError, onModalClose]);

  const handleMonitoredChange = useCallback(({ value }: CheckInputChanged) => {
    setMonitored(value);
  }, []);

  const handleMonitorNewEpisodesChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setMonitorNewEpisodes(value);
    },
    []
  );

  const handleEpisodeSourceChange = useCallback(
    ({ value }: InputChanged<PodcastEpisodeSource>) => {
      setEpisodeSource(value);
    },
    []
  );

  const handleTrackerSearchTermChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTrackerSearchTerm(value);
    },
    []
  );

  const handleGrabTrackerReleasesChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setGrabTrackerReleases(value);
    },
    []
  );

  const handleSavePress = useCallback(() => {
    savePodcast({
      ...podcast,
      monitored,
      monitorNewEpisodes,
      episodeSource,
      trackerSearchTerm: trackerSearchTerm.trim() || undefined,
      grabTrackerReleases,
    });
  }, [
    podcast,
    monitored,
    monitorNewEpisodes,
    episodeSource,
    trackerSearchTerm,
    grabTrackerReleases,
    savePodcast,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('EditPodcastModalHeader', { title: podcast.title })}
      </ModalHeader>

      <ModalBody>
        <Form>
          <FormRow>
            <FormLabel>{translate('Monitored')}</FormLabel>
            <FormInput
              type={inputTypes.CHECK}
              name="monitored"
              value={monitored}
              helpText={translate('PodcastMonitoredHelpText')}
              onChange={handleMonitoredChange}
            />
          </FormRow>

          <FormRow>
            <FormLabel>{translate('MonitorNewEpisodes')}</FormLabel>
            <FormInput
              type={inputTypes.CHECK}
              name="monitorNewEpisodes"
              value={monitorNewEpisodes}
              helpText={translate('MonitorNewEpisodesHelpText')}
              onChange={handleMonitorNewEpisodesChange}
            />
          </FormRow>

          <FormRow>
            <FormLabel>{translate('EpisodeSource')}</FormLabel>
            <FormInputHelpText text={translate('EpisodeSourceHelpText')} />
            <FormInput
              type={inputTypes.SELECT}
              name="episodeSource"
              value={episodeSource}
              values={episodeSourceOptions}
              onChange={handleEpisodeSourceChange}
            />
          </FormRow>

          <FormRow>
            <FormLabel>{translate('TrackerSearchTerm')}</FormLabel>
            <FormInputHelpText text={translate('TrackerSearchTermHelpText')} />
            <FormInput
              type={inputTypes.TEXT}
              name="trackerSearchTerm"
              value={trackerSearchTerm}
              placeholder={
                podcast.trackerSearchTerm
                  ? podcast.title
                  : podcast.trackerSearchTerms?.join(', ') ?? podcast.title
              }
              onChange={handleTrackerSearchTermChange}
            />
          </FormRow>

          <FormRow>
            <FormLabel>{translate('GrabTrackerReleases')}</FormLabel>
            <FormInput
              type={inputTypes.CHECK}
              name="grabTrackerReleases"
              value={grabTrackerReleases}
              helpText={translate('GrabTrackerReleasesHelpText')}
              onChange={handleGrabTrackerReleasesChange}
            />
          </FormRow>

          <FormRow>
            <FormLabel>{translate('Path')}</FormLabel>
            <div className={styles.path}>{podcast.path}</div>
          </FormRow>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerErrorButton
          kind={kinds.PRIMARY}
          isSpinning={isSaving}
          error={saveError}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

interface EditPodcastModalProps extends EditPodcastModalContentProps {
  isOpen: boolean;
}

function EditPodcastModal({
  isOpen,
  onModalClose,
  ...otherProps
}: EditPodcastModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <EditPodcastModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default EditPodcastModal;
