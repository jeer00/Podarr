function formatDuration(seconds?: number) {
  if (!seconds) {
    return '';
  }

  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }

  return `${Math.max(minutes, 1)}m`;
}

export default formatDuration;
