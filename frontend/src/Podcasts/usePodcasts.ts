import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import {
  Podcast,
  PodcastDownload,
  PodcastEpisode,
  PodcastMonitorType,
  PodcastRelease,
} from './Podcast';

const DEFAULT_PODCASTS: Podcast[] = [];
const DEFAULT_EPISODES: PodcastEpisode[] = [];
const DEFAULT_RELEASES: PodcastRelease[] = [];
const DEFAULT_DOWNLOADS: PodcastDownload[] = [];

export const PODCASTS_QUERY_KEY = ['/podcast'];

const usePodcasts = () => {
  const { data, ...result } = useApiQuery<Podcast[]>({
    path: '/podcast',
    queryOptions: {
      staleTime: 5 * 60 * 1000,
      gcTime: Infinity,
    },
  });

  return {
    ...result,
    data: data ?? DEFAULT_PODCASTS,
  };
};

export default usePodcasts;

export const useSortedPodcasts = () => {
  const { data, ...result } = usePodcasts();

  const sorted = useMemo(() => {
    return [...data].sort((a, b) =>
      (a.sortTitle ?? a.title).localeCompare(b.sortTitle ?? b.title)
    );
  }, [data]);

  return { ...result, data: sorted };
};

export const usePodcast = (podcastId?: number) => {
  const { data } = usePodcasts();

  return useMemo(
    () => data.find((podcast) => podcast.id === podcastId),
    [data, podcastId]
  );
};

export const usePodcastLookup = (term: string) => {
  const { data, ...result } = useApiQuery<Podcast[]>({
    path: '/podcast/lookup',
    queryParams: { term },
    queryOptions: {
      enabled: !!term.trim(),
      retry: false,
      staleTime: 5 * 60 * 1000,
    },
  });

  return {
    ...result,
    data: data ?? DEFAULT_PODCASTS,
  };
};

export const usePodcastEpisodes = (podcastId: number) => {
  const { data, ...result } = useApiQuery<PodcastEpisode[]>({
    path: '/podcastEpisode',
    queryParams: { podcastId },
  });

  return {
    ...result,
    data: data ?? DEFAULT_EPISODES,
  };
};

const updatePodcastInCache = (
  queryClient: ReturnType<typeof useQueryClient>,
  updated: Podcast
) => {
  queryClient.setQueryData<Podcast[]>(PODCASTS_QUERY_KEY, (oldData = []) => {
    if (!oldData.some((p) => p.id === updated.id)) {
      return [...oldData, updated];
    }

    return oldData.map((p) => (p.id === updated.id ? updated : p));
  });
};

export interface AddPodcastPayload {
  podcast: Podcast;
  rootFolderPath: string;
  monitor: PodcastMonitorType;
  monitorNewEpisodes: boolean;
}

export const useAddPodcast = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error, data } = useApiMutation<
    Podcast,
    Partial<Podcast> & { addOptions: { monitor: PodcastMonitorType } }
  >({
    path: '/podcast',
    method: 'POST',
    mutationOptions: {
      onSuccess: (podcast) => {
        updatePodcastInCache(queryClient, podcast);
        queryClient.invalidateQueries({ queryKey: ['/podcast/lookup'] });
      },
    },
  });

  const addPodcast = useCallback(
    ({
      podcast,
      rootFolderPath,
      monitor,
      monitorNewEpisodes,
    }: AddPodcastPayload) => {
      mutate({
        title: podcast.title,
        feedUrl: podcast.feedUrl,
        imageUrl: podcast.imageUrl,
        itunesId: podcast.itunesId,
        rootFolderPath,
        monitored: true,
        monitorNewEpisodes: monitor === 'none' ? false : monitorNewEpisodes,
        tags: [],
        addOptions: { monitor },
      });
    },
    [mutate]
  );

  return {
    addPodcast,
    isAdding: isPending,
    addError: error,
    addedPodcast: data,
  };
};

export const useSavePodcast = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<Podcast, Podcast>({
    path: '/podcast',
    method: 'PUT',
    getRequest: (podcast) => ({ body: podcast }),
    mutationOptions: {
      onSuccess: (podcast) => updatePodcastInCache(queryClient, podcast),
    },
  });

  return {
    savePodcast: mutate,
    isSaving: isPending,
    saveError: error,
  };
};

export const useDeletePodcast = (podcastId: number) => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    unknown,
    void,
    { deleteFiles: boolean }
  >({
    path: `/podcast/${podcastId}`,
    method: 'DELETE',
    getRequest: ({ deleteFiles }) => ({ queryParams: { deleteFiles } }),
    mutationOptions: {
      onSuccess: () => {
        queryClient.setQueryData<Podcast[]>(PODCASTS_QUERY_KEY, (oldData) =>
          oldData?.filter((p) => p.id !== podcastId)
        );
      },
    },
  });

  return {
    deletePodcast: mutate,
    isDeleting: isPending,
    deleteError: error,
  };
};

const updateEpisodesInCache = (
  queryClient: ReturnType<typeof useQueryClient>,
  episodes: PodcastEpisode[]
) => {
  const byId = new Map(episodes.map((e) => [e.id, e]));

  queryClient.setQueriesData<PodcastEpisode[]>(
    { queryKey: ['/podcastEpisode'] },
    (oldData) => oldData?.map((e) => byId.get(e.id) ?? e)
  );
};

export const useSetEpisodesMonitored = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<
    PodcastEpisode[],
    { episodeIds: number[]; monitored: boolean }
  >({
    path: '/podcastEpisode/monitor',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (episodes) => updateEpisodesInCache(queryClient, episodes),
    },
  });

  return {
    setEpisodesMonitored: mutate,
    isSettingMonitored: isPending,
  };
};

export const useDeleteEpisodeFile = (episodeId: number) => {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<PodcastEpisode, void>({
    path: `/podcastEpisode/${episodeId}/file`,
    method: 'DELETE',
    mutationOptions: {
      onSuccess: (episode) => updateEpisodesInCache(queryClient, [episode]),
    },
  });

  return {
    deleteEpisodeFile: mutate,
    isDeletingFile: isPending,
  };
};

export const usePodcastReleases = (
  podcastId: number,
  episodeId: number | undefined,
  enabled: boolean
) => {
  const { data, ...result } = useApiQuery<PodcastRelease[]>({
    path: '/podcast/release',
    queryParams: episodeId ? { podcastId, episodeId } : { podcastId },
    queryOptions: {
      enabled,
      retry: false,
      staleTime: 0,
      gcTime: 0,
    },
  });

  return {
    ...result,
    data: data ?? DEFAULT_RELEASES,
  };
};

export interface GrabReleasePayload {
  podcastId: number;
  episodeId?: number;
  indexerId: number;
  guid: string;
}

export const useGrabPodcastRelease = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error, variables } = useApiMutation<
    PodcastDownload,
    GrabReleasePayload
  >({
    path: '/podcast/release',
    method: 'POST',
    mutationOptions: {
      onSuccess: (download) => {
        queryClient.invalidateQueries({
          queryKey: ['/podcastDownload', { podcastId: download.podcastId }],
        });
      },
    },
  });

  return {
    grabRelease: mutate,
    isGrabbing: isPending,
    grabError: error,
    grabbingGuid: isPending ? variables?.guid : undefined,
  };
};

export const usePodcastDownloads = (podcastId: number) => {
  const { data, ...result } = useApiQuery<PodcastDownload[]>({
    path: '/podcastDownload',
    queryParams: { podcastId },
  });

  return {
    ...result,
    data: data ?? DEFAULT_DOWNLOADS,
  };
};

export const useReimportPodcastDownload = (id: number) => {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<PodcastDownload, void>({
    path: `/podcastDownload/${id}/reimport`,
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ['/podcastDownload'] });
      },
    },
  });

  return {
    reimportDownload: mutate,
    isReimporting: isPending,
  };
};

export const useRemovePodcastDownload = (id: number) => {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<
    unknown,
    void,
    { removeFromClient: boolean }
  >({
    path: `/podcastDownload/${id}`,
    method: 'DELETE',
    getRequest: ({ removeFromClient }) => ({
      queryParams: { removeFromClient },
    }),
    mutationOptions: {
      onSuccess: () => {
        queryClient.invalidateQueries({ queryKey: ['/podcastDownload'] });
      },
    },
  });

  return {
    removeDownload: mutate,
    isRemoving: isPending,
  };
};
