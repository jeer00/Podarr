import React from 'react';
import { Navigate, Route } from 'react-router-dom';
import NotFound from 'Components/NotFound';
import AddPodcast from 'Podcasts/Add/AddPodcast';
import PodcastDetailsPage from 'Podcasts/Details/PodcastDetailsPage';
import PodcastIndex from 'Podcasts/Index/PodcastIndex';
import DownloadClientSettings from 'Settings/DownloadClients/DownloadClientSettings';
import FeedSettings from 'Settings/Feeds/FeedSettings';
import GeneralSettings from 'Settings/General/GeneralSettings';
import IndexerSettings from 'Settings/Indexers/IndexerSettings';
import RootFolderSettings from 'Settings/RootFolders/RootFolderSettings';
import Settings from 'Settings/Settings';
import UISettings from 'Settings/UI/UISettings';
import Backups from 'System/Backup/Backups';
import LogsTable from 'System/Events/LogsTable';
import AppLogFiles from 'System/Logs/App/AppLogFiles';
import UpdateLogFiles from 'System/Logs/Update/UpdateLogFiles';
import Status from 'System/Status/Status';
import Tasks from 'System/Tasks/Tasks';

export function appRouteElements() {
  return (
    <>
      <Route path="/" element={<Navigate to="/podcasts" replace={true} />} />

      {/*
        Podcasts
      */}

      <Route path="/podcasts" element={<PodcastIndex />} />

      <Route path="/podcasts/add" element={<AddPodcast />} />

      <Route path="/podcasts/:podcastId" element={<PodcastDetailsPage />} />

      {/*
        Old TV routes, kept as redirects so existing bookmarks still land somewhere useful
      */}

      <Route
        path="/series/*"
        element={<Navigate to="/podcasts" replace={true} />}
      />

      <Route
        path="/add/*"
        element={<Navigate to="/podcasts/add" replace={true} />}
      />

      {/*
        Settings
      */}

      <Route path="/settings" element={<Settings />} />

      <Route path="/settings/rootfolders" element={<RootFolderSettings />} />

      <Route path="/settings/indexers" element={<IndexerSettings />} />

      <Route
        path="/settings/downloadclients"
        element={<DownloadClientSettings />}
      />

      <Route path="/settings/feeds" element={<FeedSettings />} />

      <Route path="/settings/general" element={<GeneralSettings />} />

      <Route path="/settings/ui" element={<UISettings />} />

      {/*
        System
      */}

      <Route path="/system/status" element={<Status />} />

      <Route path="/system/tasks" element={<Tasks />} />

      <Route path="/system/backups" element={<Backups />} />

      <Route path="/system/events" element={<LogsTable />} />

      <Route path="/system/logs/files" element={<AppLogFiles />} />

      <Route path="/system/logs/files/update" element={<UpdateLogFiles />} />

      {/*
        Not Found
      */}

      <Route path="*" element={<NotFound />} />
    </>
  );
}
