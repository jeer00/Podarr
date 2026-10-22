import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import { PodcastMonitorType } from './Podcast';

export interface AddPodcastOptions {
  rootFolderPath: string;
  monitor: PodcastMonitorType;
  monitorNewEpisodes: boolean;
}

const { useOptions, setOption } = createOptionsStore<AddPodcastOptions>(
  'add_podcast_options',
  () => {
    return {
      rootFolderPath: '',
      monitor: 'latest',
      monitorNewEpisodes: true,
    };
  }
);

export const useAddPodcastOptions = useOptions;
export const setAddPodcastOption = setOption;
