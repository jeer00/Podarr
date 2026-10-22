import React from 'react';
import Icon, { IconName } from 'Components/Icon';
import Link from 'Components/Link/Link';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import SettingsPage from './SettingsPage';
import styles from './Settings.module.css';

interface SectionEntry {
  to: string;
  iconName: IconName;
  title: string;
  summary: string;
}

function buildSections(): SectionEntry[] {
  return [
    {
      to: '/settings/rootfolders',
      iconName: icons.ROOT_FOLDER,
      title: translate('RootFolders'),
      summary: translate('RootFoldersSettingsSummary'),
    },
    {
      to: '/settings/indexers',
      iconName: icons.RSS,
      title: translate('Indexers'),
      summary: translate('PodcastIndexersSettingsSummary'),
    },
    {
      to: '/settings/downloadclients',
      iconName: icons.DOWNLOADING,
      title: translate('DownloadClients'),
      summary: translate('PodcastDownloadClientsSettingsSummary'),
    },
    {
      to: '/settings/feeds',
      iconName: icons.RSS,
      title: translate('PodcastFeeds'),
      summary: translate('PodcastFeedsSettingsSummary'),
    },
    {
      to: '/settings/general',
      iconName: icons.ADVANCED_SETTINGS,
      title: translate('General'),
      summary: translate('GeneralSettingsSummary'),
    },
    {
      to: '/settings/ui',
      iconName: icons.VIEW,
      title: translate('Ui'),
      summary: translate('UiSettingsSummary'),
    },
  ];
}

function Settings() {
  const sections = buildSections();

  return (
    <SettingsPage title={translate('Settings')} showSave={false}>
      <PageContentBody className={styles.body}>
        <div className={styles.section}>
          <PageHeading title={translate('Settings')} />

          <div className={styles.grid}>
            {sections.map((section) => (
              <Link key={section.to} className={styles.card} to={section.to}>
                <div className={styles.cardHead}>
                  <span className={styles.cardIcon}>
                    <Icon name={section.iconName} size={16} />
                  </span>
                  <h2 className={styles.cardTitle}>{section.title}</h2>
                </div>
                <p className={styles.cardSummary}>{section.summary}</p>
              </Link>
            ))}
          </div>
        </div>
      </PageContentBody>
    </SettingsPage>
  );
}

export default Settings;
