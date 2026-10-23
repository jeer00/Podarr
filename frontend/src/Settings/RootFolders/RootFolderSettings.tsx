import React, { useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import FileBrowserModal from 'Components/FileBrowser/FileBrowserModal';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import SectionHeading from 'Components/SectionHeading';
import { icons, kinds } from 'Helpers/Props';
import useRootFolders, {
  RootFolder,
  useAddRootFolder,
  useDeleteRootFolder,
} from 'RootFolder/useRootFolders';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import { InputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import styles from './RootFolderSettings.module.css';

function RootFolderItem({ id, path, accessible, freeSpace }: RootFolder) {
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);
  const { deleteRootFolder } = useDeleteRootFolder(id);

  const handleDeletePress = useCallback(() => {
    setIsConfirmOpen(true);
  }, []);

  const handleConfirmCancel = useCallback(() => {
    setIsConfirmOpen(false);
  }, []);

  const handleConfirmDelete = useCallback(() => {
    deleteRootFolder();
    setIsConfirmOpen(false);
  }, [deleteRootFolder]);

  return (
    <div className={styles.row}>
      <Icon className={styles.icon} name={icons.ROOT_FOLDER} size={18} />

      <div className={styles.info}>
        <div className={styles.path}>{path}</div>
        <div className={styles.meta}>
          {accessible
            ? translate('RootFolderSelectFreeSpace', {
                freeSpace: formatBytes(freeSpace ?? 0),
              })
            : translate('RootFolderNotAccessible')}
        </div>
      </div>

      <IconButton
        name={icons.DELETE}
        title={translate('RemoveRootFolder')}
        onPress={handleDeletePress}
      />

      <ConfirmModal
        isOpen={isConfirmOpen}
        kind={kinds.DANGER}
        title={translate('RemoveRootFolder')}
        message={translate('RemoveRootFolderPodcastMessageText', { path })}
        confirmLabel={translate('Remove')}
        onConfirm={handleConfirmDelete}
        onCancel={handleConfirmCancel}
      />
    </div>
  );
}

function RootFolderSettings() {
  const { data, isFetched, error } = useRootFolders();
  const { addRootFolder, addError } = useAddRootFolder();
  const [isFileBrowserOpen, setIsFileBrowserOpen] = useState(false);

  const handleAddPress = useCallback(() => {
    setIsFileBrowserOpen(true);
  }, []);

  const handleFileBrowserClose = useCallback(() => {
    setIsFileBrowserOpen(false);
  }, []);

  const handleFolderSelect = useCallback(
    ({ value }: InputChanged<string>) => {
      addRootFolder({ path: value });
    },
    [addRootFolder]
  );

  return (
    <SettingsPage title={translate('RootFolders')} showSave={false}>
      <PageContentBody>
        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('RootFolders')}
          />

          <div className={settingsStyles.pageSection}>
            <SectionHeading
              title={translate('RootFolders')}
              description={translate('PodcastRootFoldersDescription')}
            />

            {!isFetched && !error ? <LoadingIndicator /> : null}

            {error ? (
              <Alert kind={kinds.DANGER}>
                {translate('RootFoldersLoadError')}
              </Alert>
            ) : null}

            {addError ? (
              <Alert kind={kinds.DANGER}>
                {translate('AddRootFolderError')}: {getErrorMessage(addError)}
              </Alert>
            ) : null}

            <div className={styles.list}>
              {data.map((rootFolder) => (
                <RootFolderItem key={rootFolder.id} {...rootFolder} />
              ))}
            </div>

            <Button
              kind={data.length ? kinds.DEFAULT : kinds.PRIMARY}
              onPress={handleAddPress}
            >
              <Icon className={styles.addIcon} name={icons.ADD} />
              {translate('AddRootFolder')}
            </Button>

            <FileBrowserModal
              isOpen={isFileBrowserOpen}
              name="rootFolderPath"
              value=""
              onChange={handleFolderSelect}
              onModalClose={handleFileBrowserClose}
            />
          </div>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default RootFolderSettings;
