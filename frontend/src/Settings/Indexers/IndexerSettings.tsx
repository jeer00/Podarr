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
import Indexers from './Indexers/Indexers';
import ManageIndexersModal from './Indexers/Manage/ManageIndexersModal';
import { useTestAllIndexers } from './useIndexers';

function IndexerSettings() {
  const {
    isTestingAllIndexers,
    testAllIndexers,
    testAllResults,
    testAllError,
  } = useTestAllIndexers();

  const [isManageIndexersModalOpen, setIsManageIndexersModalOpen] =
    useState(false);

  const handleManageIndexersPress = useCallback(() => {
    setIsManageIndexersModalOpen(true);
  }, []);

  const handleManageIndexersModalClose = useCallback(() => {
    setIsManageIndexersModalOpen(false);
  }, []);

  const handleTestAllIndexersPress = useCallback(() => {
    testAllIndexers();
  }, [testAllIndexers]);

  let testStatusLabel: string | undefined = undefined;

  if (isTestingAllIndexers) {
    testStatusLabel = translate('TestingAllIndexers');
  } else if (testAllResults) {
    testStatusLabel = translate('IndexerTestAllCompleted');
  } else if (testAllError) {
    testStatusLabel = translate('IndexerTestAllFailed');
  }

  return (
    <SettingsPage
      title={translate('IndexerSettings')}
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
            label={translate('TestAllIndexers')}
            iconName={icons.TEST}
            isSpinning={isTestingAllIndexers}
            onPress={handleTestAllIndexersPress}
          />

          <ToolbarItem
            id="manage"
            priority={1}
            groupId="extras"
            label={translate('ManageIndexers')}
            iconName={icons.MANAGE}
            onPress={handleManageIndexersPress}
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
            title={translate('Indexers')}
          />

          <div className={settingsStyles.pageSection}>
            <SectionHeading
              title={translate('Indexers')}
              description={translate('IndexersSectionDescription')}
            />

            <Indexers testResults={testAllResults} />
          </div>

          <ManageIndexersModal
            isOpen={isManageIndexersModalOpen}
            onModalClose={handleManageIndexersModalClose}
          />
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default IndexerSettings;
