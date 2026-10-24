import { useQueryClient } from '@tanstack/react-query';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface PodcastFeedSettings {
  enabled: boolean;
  baseUrl?: string;
  token: string;
  feedBaseUrl: string;
  s3Enabled: boolean;
  s3Endpoint?: string;
  s3Region?: string;
  s3Bucket?: string;
  s3AccessKey?: string;
  s3SecretKey?: string;
  hasS3SecretKey: boolean;
  s3Prefix?: string;
  s3PublicUrl?: string;
  s3PublicOpmlUrl?: string;
  s3PublicFeedUrlTemplate?: string;
}

const QUERY_KEY = ['/podcastfeed/settings'];

export const usePodcastFeedSettings = () => {
  return useApiQuery<PodcastFeedSettings>({
    path: '/podcastfeed/settings',
  });
};

export const useSavePodcastFeedSettings = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending, error } = useApiMutation<
    PodcastFeedSettings,
    PodcastFeedSettings
  >({
    path: '/podcastfeed/settings',
    method: 'PUT',
    mutationOptions: {
      onSuccess: (settings) => {
        queryClient.setQueryData(QUERY_KEY, settings);
      },
    },
  });

  return { saveSettings: mutate, isSaving: isPending, saveError: error };
};

export const useResetPodcastFeedToken = () => {
  const queryClient = useQueryClient();

  const { mutate, isPending } = useApiMutation<PodcastFeedSettings, void>({
    path: '/podcastfeed/settings/resettoken',
    method: 'POST',
    mutationOptions: {
      onSuccess: (settings) => {
        queryClient.setQueryData(QUERY_KEY, settings);
      },
    },
  });

  return { resetToken: mutate, isResetting: isPending };
};

// Where podcast apps reach the feeds, the configured external URL or this machine's local network address.
export const getFeedBaseUrl = (settings: PodcastFeedSettings) => {
  return settings.feedBaseUrl;
};

// Address of the copy uploaded to the S3 bucket, when uploading is set up with a public URL.
export const getPodcastBucketFeedUrl = (
  settings: PodcastFeedSettings,
  podcastId: number
) => {
  if (!settings.s3Enabled || !settings.s3PublicFeedUrlTemplate) {
    return undefined;
  }

  return settings.s3PublicFeedUrlTemplate.replace('{id}', String(podcastId));
};

export const useTestS3Settings = () => {
  const { mutate, isPending, error, isSuccess, reset } = useApiMutation<
    void,
    PodcastFeedSettings
  >({
    path: '/podcastfeed/settings/tests3',
    method: 'POST',
  });

  return {
    testS3: mutate,
    isTestingS3: isPending,
    testS3Error: error,
    testS3Succeeded: isSuccess,
    resetTestS3: reset,
  };
};

export const usePublishPodcastFeeds = () => {
  const { mutate, isPending } = useApiMutation<void, void>({
    path: '/podcastfeed/settings/publish',
    method: 'POST',
  });

  return { publishFeeds: mutate, isPublishing: isPending };
};

export const getPodcastFeedUrl = (
  settings: PodcastFeedSettings,
  podcastId: number
) => {
  return `${getFeedBaseUrl(settings)}/podcast/${podcastId}.xml`;
};

// The feed a podcast app should subscribe to, the uploaded copy if there is one.
export const usePodcastFeedUrl = (podcastId: number) => {
  const { data } = usePodcastFeedSettings();

  if (!data?.enabled) {
    return undefined;
  }

  return (
    getPodcastBucketFeedUrl(data, podcastId) ??
    getPodcastFeedUrl(data, podcastId)
  );
};
