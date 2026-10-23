import React, { useCallback, useState } from 'react';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import { icons, kinds } from 'Helpers/Props';
import { Podcast } from 'Podcasts/Podcast';
import PodcastImage from 'Podcasts/PodcastImage';
import translate from 'Utilities/String/translate';
import AddPodcastModal from './AddPodcastModal';
import styles from './AddPodcastSearchResult.module.css';

interface AddPodcastSearchResultProps {
  podcast: Podcast;
}

function AddPodcastSearchResult({ podcast }: AddPodcastSearchResultProps) {
  const { id, title, author, imageUrl, categories, explicit, feedUrl } =
    podcast;
  const isExisting = !!id;
  const [isModalOpen, setIsModalOpen] = useState(false);

  const handlePress = useCallback(() => {
    setIsModalOpen(true);
  }, []);

  const handleModalClose = useCallback(() => {
    setIsModalOpen(false);
  }, []);

  return (
    <div className={styles.result}>
      <Link
        className={styles.link}
        {...(isExisting ? { to: `/podcasts/${id}` } : { onPress: handlePress })}
      >
        <div className={styles.artwork}>
          <PodcastImage imageUrl={imageUrl} title={title} />
        </div>

        <div className={styles.content}>
          <div className={styles.titleRow}>
            <div className={styles.title}>{title}</div>

            {isExisting ? (
              <Icon
                className={styles.alreadyExistsIcon}
                name={icons.CHECK_CIRCLE}
                size={18}
                title={translate('AlreadyInYourLibrary')}
              />
            ) : null}
          </div>

          {author ? <div className={styles.author}>{author}</div> : null}

          <div className={styles.labels}>
            {categories.slice(0, 3).map((category) => (
              <Label key={category}>{category}</Label>
            ))}

            {explicit ? (
              <Label kind={kinds.WARNING}>{translate('Explicit')}</Label>
            ) : null}
          </div>

          <div className={styles.feedUrl}>{feedUrl}</div>
        </div>
      </Link>

      <AddPodcastModal
        isOpen={isModalOpen}
        podcast={podcast}
        onModalClose={handleModalClose}
      />
    </div>
  );
}

export default AddPodcastSearchResult;
