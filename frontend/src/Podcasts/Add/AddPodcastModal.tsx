import React, { useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import styles from 'AddSeries/AddNewSeries/AddNewSeriesModalContent.module.css';
import Alert from 'Components/Alert';
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
import { getValidationFailures } from 'Helpers/Hooks/useApiMutation';
import { inputTypes, kinds } from 'Helpers/Props';
import {
  AddPodcastOptions,
  setAddPodcastOption,
  useAddPodcastOptions,
} from 'Podcasts/addPodcastOptionsStore';
import { Podcast, PodcastMonitorType } from 'Podcasts/Podcast';
import PodcastImage from 'Podcasts/PodcastImage';
import { useAddPodcast } from 'Podcasts/usePodcasts';
import { useIsWindows } from 'System/Status/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';

const monitorOptions: { key: PodcastMonitorType; value: () => string }[] = [
  { key: 'all', value: () => translate('PodcastMonitorAll') },
  { key: 'latest', value: () => translate('PodcastMonitorLatest') },
  { key: 'future', value: () => translate('PodcastMonitorFuture') },
  { key: 'none', value: () => translate('PodcastMonitorNone') },
];

interface AddPodcastModalContentProps {
  podcast: Podcast;
  onModalClose: () => void;
}

function AddPodcastModalContent({
  podcast,
  onModalClose,
}: AddPodcastModalContentProps) {
  const { title, author, description, imageUrl } = podcast;
  const { rootFolderPath, monitor, monitorNewEpisodes } =
    useAddPodcastOptions();
  const { addPodcast, isAdding, addError, addedPodcast } = useAddPodcast();
  const isWindows = useIsWindows();
  const navigate = useNavigate();

  const validationFailures = getValidationFailures(addError);
  const errorMessage =
    addError && !validationFailures.errors.length
      ? getErrorMessage(addError)
      : undefined;

  const handleInputChange = useCallback(
    ({ name, value }: InputChanged<string | boolean>) => {
      setAddPodcastOption(
        name as keyof AddPodcastOptions,
        value as AddPodcastOptions[keyof AddPodcastOptions]
      );
    },
    []
  );

  const handleAddPress = useCallback(() => {
    addPodcast({
      podcast,
      rootFolderPath,
      monitor,
      monitorNewEpisodes,
    });
  }, [podcast, rootFolderPath, monitor, monitorNewEpisodes, addPodcast]);

  useEffect(() => {
    if (addedPodcast) {
      onModalClose();
      navigate(`/podcasts/${addedPodcast.id}`);
    }
  }, [addedPodcast, navigate, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{title}</ModalHeader>

      <ModalBody>
        <div className={styles.container}>
          <div className={styles.poster}>
            <PodcastImage imageUrl={imageUrl} title={title} lazy={false} />
          </div>

          <div className={styles.info}>
            {author ? <p className={styles.intro}>{author}</p> : null}
            {description ? <p className={styles.intro}>{description}</p> : null}

            {errorMessage ? (
              <Alert kind={kinds.DANGER}>{errorMessage}</Alert>
            ) : null}

            <Form
              validationErrors={validationFailures.errors}
              validationWarnings={validationFailures.warnings}
            >
              <FormRow>
                <FormLabel>{translate('RootFolder')}</FormLabel>
                <FormInputHelpText
                  text={translate('AddPodcastRootFolderHelpText')}
                />
                <FormInput
                  type={inputTypes.ROOT_FOLDER_SELECT}
                  name="rootFolderPath"
                  value={rootFolderPath}
                  valueOptions={{ seriesFolder: title, isWindows }}
                  selectedValueOptions={{ seriesFolder: title, isWindows }}
                  onChange={handleInputChange}
                />
              </FormRow>

              <FormRow>
                <FormLabel>{translate('DownloadEpisodes')}</FormLabel>
                <FormInput
                  type={inputTypes.SELECT}
                  name="monitor"
                  value={monitor}
                  values={monitorOptions}
                  onChange={handleInputChange}
                />
              </FormRow>

              <FormRow>
                <FormLabel>{translate('MonitorNewEpisodes')}</FormLabel>
                <FormInput
                  type={inputTypes.CHECK}
                  name="monitorNewEpisodes"
                  value={monitor === 'none' ? false : monitorNewEpisodes}
                  isDisabled={monitor === 'none'}
                  helpText={translate('MonitorNewEpisodesHelpText')}
                  onChange={handleInputChange}
                />
              </FormRow>
            </Form>
          </div>
        </div>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <SpinnerErrorButton
          kind={kinds.PRIMARY}
          isSpinning={isAdding}
          isDisabled={!rootFolderPath}
          error={addError}
          onPress={handleAddPress}
        >
          {translate('AddPodcast')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

interface AddPodcastModalProps extends AddPodcastModalContentProps {
  isOpen: boolean;
}

function AddPodcastModal({
  isOpen,
  onModalClose,
  ...otherProps
}: AddPodcastModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <AddPodcastModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default AddPodcastModal;
