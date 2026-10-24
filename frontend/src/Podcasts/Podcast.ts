import ModelBase from 'App/ModelBase';

export type PodcastMonitorType = 'all' | 'future' | 'latest' | 'none';

export type PodcastEpisodeSource = 'feed' | 'feedAndTracker' | 'tracker';

export type PodcastFileSource = 'none' | 'feed' | 'tracker';

export interface PodcastStatistics {
  episodeCount: number;
  monitoredEpisodeCount: number;
  downloadedEpisodeCount: number;
  sizeOnDisk: number;
  lastAired?: string;
}

export interface Podcast extends ModelBase {
  title: string;
  sortTitle?: string;
  description?: string;
  author?: string;
  feedUrl: string;
  websiteUrl?: string;
  imageUrl?: string;
  language?: string;
  categories: string[];
  explicit: boolean;
  itunesId?: number;
  path?: string;
  rootFolderPath?: string;
  monitored: boolean;
  monitorNewEpisodes: boolean;
  added: string;
  lastRefreshed?: string;
  lastRefreshError?: string;
  tags: number[];
  episodeSource: PodcastEpisodeSource;
  trackerSearchTerm?: string;
  trackerSearchTerms: string[];
  grabTrackerReleases: boolean;
  grabTrackerReleasesSince?: string;
  statistics?: PodcastStatistics;
}

export type PodcastDownloadStatus =
  | 'queued'
  | 'downloading'
  | 'imported'
  | 'failed';

export interface PodcastDownload extends ModelBase {
  podcastId: number;
  podcastEpisodeId?: number;
  title: string;
  indexer?: string;
  protocol: string;
  size: number;
  status: PodcastDownloadStatus;
  progress: number;
  message?: string;
  added: string;
  completed?: string;
}

export interface PodcastRelease {
  guid: string;
  title: string;
  indexerId: number;
  indexer: string;
  protocol: string;
  size: number;
  seeders?: number;
  leechers?: number;
  publishDate: string;
  infoUrl?: string;
  matchesPodcast: boolean;
  episodeScore: number;
  isGrabbed: boolean;
}

export interface PodcastEpisode extends ModelBase {
  podcastId: number;
  title: string;
  description?: string;
  airDateUtc?: string;
  seasonNumber?: number;
  episodeNumber?: number;
  episodeType?: string;
  duration?: number;
  enclosureUrl?: string;
  enclosureType?: string;
  enclosureLength?: number;
  imageUrl?: string;
  explicit: boolean;
  monitored: boolean;
  hasFile: boolean;
  relativePath?: string;
  fileSource: PodcastFileSource;
  size: number;
  dateDownloaded?: string;
  lastDownloadError?: string;
}

export default Podcast;
