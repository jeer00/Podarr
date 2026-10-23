import React from 'react';
import { useParams } from 'react-router';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import NotFound from 'Components/NotFound';
import usePodcasts from 'Podcasts/usePodcasts';
import translate from 'Utilities/String/translate';
import PodcastDetails from './PodcastDetails';

function PodcastDetailsPage() {
  const { podcastId } = useParams<{ podcastId: string }>();
  const { data, isFetched } = usePodcasts();
  const podcast = data.find((p) => p.id === Number(podcastId));

  if (!isFetched) {
    return <LoadingIndicator />;
  }

  if (!podcast) {
    return <NotFound message={translate('PodcastCannotBeFound')} />;
  }

  return <PodcastDetails podcast={podcast} />;
}

export default PodcastDetailsPage;
