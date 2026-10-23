import React, { useCallback, useRef, useState } from 'react';
import styles from 'AddSeries/AddNewSeries/AddNewSeries.module.css';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageHeading from 'Components/Page/PageHeading';
import useDebounce from 'Helpers/Hooks/useDebounce';
import useQueryParams from 'Helpers/Hooks/useQueryParams';
import { icons } from 'Helpers/Props';
import { usePodcastLookup } from 'Podcasts/usePodcasts';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddPodcastSearchResult from './AddPodcastSearchResult';

function AddPodcast() {
  const { term: initialTerm = '' } = useQueryParams<{ term: string }>();
  const [term, setTerm] = useState(initialTerm);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const query = useDebounce(term.trim(), term ? 400 : 0);

  const { isFetching, error, data } = usePodcastLookup(query);

  const handleSearchInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTerm(value);
    },
    []
  );

  const handleClearPress = useCallback(() => {
    setTerm('');
    searchInputRef.current?.focus();
  }, []);

  const isSearching = isFetching || term.trim() !== query;

  return (
    <PageContent title={translate('AddPodcast')}>
      <PageContentBody>
        <PageHeading
          scope={translate('Podcasts')}
          title={translate('AddPodcast')}
        />

        <div className={styles.searchSticky}>
          <div className={styles.searchWrap}>
            <Icon className={styles.searchIcon} name={icons.SEARCH} size={18} />

            <TextInput
              ref={searchInputRef}
              className={styles.searchInput}
              name="podcastLookup"
              value={term}
              placeholder={translate('PodcastSearchPlaceholder')}
              autoFocus={true}
              onChange={handleSearchInputChange}
            />

            {term ? (
              <Button
                className={styles.clearLookupButton}
                onPress={handleClearPress}
              >
                <Icon name={icons.REMOVE} size={14} />
              </Button>
            ) : null}
          </div>
        </div>

        {term && isSearching ? <LoadingIndicator /> : null}

        {term && !isSearching && error ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('PodcastSearchFailed')}
            </div>
            <p className={styles.error}>{getErrorMessage(error)}</p>
          </div>
        ) : null}

        {term && !isSearching && !error && data.length ? (
          <div className={styles.searchResults}>
            <div className={styles.resultsLabel}>
              {data.length === 1
                ? translate('CountResult', { count: data.length })
                : translate('CountResults', { count: data.length })}
            </div>

            {data.map((podcast) => (
              <AddPodcastSearchResult key={podcast.feedUrl} podcast={podcast} />
            ))}
          </div>
        ) : null}

        {term && !isSearching && !error && !data.length ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('CouldNotFindResults', { term })}
            </div>
            <div className={styles.emptyBody}>
              {translate('PodcastSearchHelpText')}
            </div>
          </div>
        ) : null}

        {term ? null : (
          <div className={styles.emptyState}>
            <div className={styles.emptyTitle}>
              {translate('AddPodcastHelpText')}
            </div>
            <div className={styles.emptyBody}>
              {translate('PodcastSearchHelpText')}
            </div>
          </div>
        )}
      </PageContentBody>
    </PageContent>
  );
}

export default AddPodcast;
