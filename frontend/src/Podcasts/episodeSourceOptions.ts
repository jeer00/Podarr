import translate from 'Utilities/String/translate';
import { PodcastEpisodeSource } from './Podcast';

const episodeSourceOptions: {
  key: PodcastEpisodeSource;
  value: () => string;
}[] = [
  {
    key: 'feedAndTracker',
    value: () => translate('EpisodeSourceFeedAndTracker'),
  },
  { key: 'feed', value: () => translate('EpisodeSourceFeed') },
  { key: 'tracker', value: () => translate('EpisodeSourceTracker') },
];

export default episodeSourceOptions;
