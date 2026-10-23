import React, { useCallback, useState } from 'react';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { OverflowDivider } from 'Components/Page/Toolbar/Overflow';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import ToolbarItem from 'Components/Page/Toolbar/ToolbarItem';
import SectionHeading from 'Components/SectionHeading';
import StatusIndicator from 'Components/StatusIndicator';
import { icons } from 'Helpers/Props';
import settingsStyles from 'Settings/Settings.module.css';
import SettingsPage from 'Settings/SettingsPage';
import translate from 'Utilities/String/translate';
import DownloadClients from './DownloadClients/DownloadClients';
import ManageDownloadClientsModal from './DownloadClients/Manage/ManageDownloadClientsModal';
import { useTestAllDownloadClients } from './DownloadClients/useDownloadClients';
import RemotePathMappings from './RemotePathMappings/RemotePathMappings';

function DownloadClientSettings() {
  const {
    testAllDownloadClients,
    isTestingAllDownloadClients,
    testAllResults,
    testAllError,
  } = useTestAllDownloadClients();

  const [
    isManageDownloadClientsModalOpen,
    setIsManageDownloadClientsModalOpen,
  ] = useState(false);

  const handleManageDownloadClientsPress = useCallback(() => {
    setIsManageDownloadClientsModalOpen(true);
  }, []);

  const handleManageDownloadClientsModalClose = useCallback(() => {
    setIsManageDownloadClientsModalOpen(false);
  }, []);

  const handleTestAllDownloadClientsPress = useCallback(() => {
    testAllDownloadClients();
  }, [testAllDownloadClients]);

  let testStatusLabel: string | undefined = undefined;

  if (isTestingAllDownloadClients) {
    testStatusLabel = translate('TestingAllDownloadClients');
  } else if (testAllResults) {
    testStatusLabel = translate('DownloadClientTestAllCompleted');
  } else if (testAllError) {
    testStatusLabel = translate('DownloadClientTestAllFailed');
  }

  return (
    <SettingsPage
      title={translate('DownloadClientSettings')}
      showSave={false}
      toolbarChildren={
        <>
          <OverflowDivider groupId="extras">
            <PageToolbarSeparator />
          </OverflowDivider>

          <ToolbarItem
            id="test-all"
            priority={1}
            groupId="extras"
            label={translate('TestAllClients')}
            iconName={icons.TEST}
            isSpinning={isTestingAllDownloadClients}
            onPress={handleTestAllDownloadClientsPress}
          />

          <ToolbarItem
            id="manage"
            priority={1}
            groupId="extras"
            label={translate('ManageClients')}
            iconName={icons.MANAGE}
            onPress={handleManageDownloadClientsPress}
          />
        </>
      }
    >
      <PageContentBody>
        {testStatusLabel ? (
          <StatusIndicator
            label={testStatusLabel}
            role={testAllError ? 'alert' : 'status'}
            aria-atomic={true}
          >
            <span />
          </StatusIndicator>
        ) : null}

        <div className={settingsStyles.section}>
          <PageHeading
            scope={translate('Settings')}
            title={translate('DownloadClients')}
          />

          <div className={settingsStyles.pageSection}>
            <SectionHeading
              title={translate('DownloadClients')}
              description={translate('DownloadClientsSectionDescription')}
            />

            <DownloadClients testResults={testAllResults} />
          </div>

          <RemotePathMappings />

          <ManageDownloadClientsModal
            isOpen={isManageDownloadClientsModalOpen}
            onModalClose={handleManageDownloadClientsModalClose}
          />
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default DownloadClientSettings;
