// Feed descriptions are frequently HTML. Render them as plain text rather
// than injecting untrusted markup into the page.
function stripHtml(html?: string) {
  if (!html) {
    return '';
  }

  const document = new DOMParser().parseFromString(
    html.replace(/<\/(p|div|li|h\d)>|<br\s*\/?>/gi, '$&\n'),
    'text/html'
  );

  return (document.body.textContent ?? '')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

export default stripHtml;
