import React, { useCallback, useEffect, useState } from 'react';
import FormInput from 'Components/Form/FormInput';
import FormLabel from 'Components/Form/FormLabel';
import FormRow from 'Components/Form/FormRow';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons, inputTypes, kinds } from 'Helpers/Props';
import { Podcast } from 'Podcasts/Podcast';
import { useDeletePodcast } from 'Podcasts/usePodcasts';
import styles from 'Series/Delete/DeleteSeriesModalContent.module.css';
import { CheckInputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';

interface DeletePodcastModalContentProps {
  podcast: Podcast;
  onModalClose: (wasDeleted?: boolean) => void;
}

function DeletePodcastModalContent({
  podcast,
  onModalClose,
}: DeletePodcastModalContentProps) {
  const { id, title, path, statistics } = podcast;
  const { downloadedEpisodeCount = 0, sizeOnDisk = 0 } = statistics ?? {};
  const [deleteFiles, setDeleteFiles] = useState(false);
  const { deletePodcast, isDeleting, deleteError } = useDeletePodcast(id);
  const wasDeleting = usePrevious(isDeleting);

  useEffect(() => {
    if (wasDeleting && !isDeleting && !deleteError) {
      onModalClose(true);
    }
  }, [isDeleting, wasDeleting, deleteError, onModalClose]);

  const handleDeleteFilesChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setDeleteFiles(value);
    },
    []
  );

  const handleDeletePress = useCallback(() => {
    deletePodcast({ deleteFiles });
  }, [deleteFiles, deletePodcast]);

  const handleClosePress = useCallback(() => {
    onModalClose();
  }, [onModalClose]);

  return (
    <ModalContent onModalClose={handleClosePress}>
      <ModalHeader>
        {translate('DeletePodcastModalHeader', { title })}
      </ModalHeader>

      <ModalBody>
        <div className={styles.pathContainer}>
          <Icon className={styles.pathIcon} name={icons.FOLDER} />
          {path}
        </div>

        <FormRow>
          <FormLabel>{translate('DeletePodcastFolder')}</FormLabel>
          <FormInput
            type={inputTypes.CHECK}
            name="deleteFiles"
            value={deleteFiles}
            kind={kinds.DANGER}
            helpText={translate('DeletePodcastFolderHelpText')}
            onChange={handleDeleteFilesChange}
          />
        </FormRow>

        {deleteFiles && downloadedEpisodeCount ? (
          <div className={styles.deleteFilesMessage}>
            {translate('DeletePodcastFolderEpisodeCount', {
              count: downloadedEpisodeCount,
              size: formatBytes(sizeOnDisk),
            })}
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={handleClosePress}>{translate('Close')}</Button>

        <SpinnerErrorButton
          kind={kinds.DANGER}
          isSpinning={isDeleting}
          error={deleteError}
          onPress={handleDeletePress}
        >
          {translate('Delete')}
        </SpinnerErrorButton>
      </ModalFooter>
    </ModalContent>
  );
}

interface DeletePodcastModalProps extends DeletePodcastModalContentProps {
  isOpen: boolean;
}

function DeletePodcastModal({
  isOpen,
  onModalClose,
  ...otherProps
}: DeletePodcastModalProps) {
  const handleModalClose = useCallback(() => {
    onModalClose();
  }, [onModalClose]);

  return (
    <Modal isOpen={isOpen} onModalClose={handleModalClose}>
      <DeletePodcastModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default DeletePodcastModal;
