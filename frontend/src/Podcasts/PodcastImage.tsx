import classNames from 'classnames';
import React, { useCallback, useEffect, useState } from 'react';
import styles from './PodcastImage.module.css';

interface PodcastImageProps {
  className?: string;
  imageUrl?: string;
  title: string;
  lazy?: boolean;
}

function PodcastImage({
  className,
  imageUrl,
  title,
  lazy = true,
}: PodcastImageProps) {
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    setHasError(false);
  }, [imageUrl]);

  const handleError = useCallback(() => {
    setHasError(true);
  }, []);

  if (!imageUrl || hasError) {
    return (
      <div className={classNames(styles.placeholder, className)}>
        <span className={styles.placeholderTitle}>{title}</span>
      </div>
    );
  }

  return (
    <img
      className={classNames(styles.image, className)}
      src={imageUrl}
      alt={title}
      loading={lazy ? 'lazy' : 'eager'}
      referrerPolicy="no-referrer"
      onError={handleError}
    />
  );
}

export default PodcastImage;
