import { Box } from '@mui/material';
import type { ReactElement } from 'react';

const TAGS = /<em>(.*?)<\/em>/g;

const ENTITY = /&(#x[\da-f]+|#\d+|[a-z]+);/gi;

const NAMED: Record<string, string> = {
  amp: '&',
  lt: '<',
  gt: '>',
  quot: '"',
  apos: "'",
  nbsp: '\u00a0',
};

function codePoint(body: string): string | undefined {
  const value = body[1] === 'x' || body[1] === 'X'
    ? Number.parseInt(body.slice(2), 16)
    : Number.parseInt(body.slice(1), 10);

  return Number.isInteger(value) && value >= 0 && value <= 0x10ffff ? String.fromCodePoint(value) : undefined;
}

// Numeric escapes as well as named ones: Lucene's encoder writes "/" as &#x2F;, so a list of the
// five obvious entities leaves that showing through in half the snippets.
function decode(text: string): string {
  return text.replace(ENTITY, (entity, body: string) =>
    (body.startsWith('#') ? codePoint(body) : NAMED[body.toLowerCase()]) ?? entity);
}

// One Elasticsearch highlight fragment. The API escapes the text and leaves only its own <em>,
// so the string is safe to render -- but splitting on the tags is cheap and keeps
// dangerouslySetInnerHTML out of the app entirely.
export function HighlightedText({ fragment }: { fragment: string }): ReactElement {
  const parts: ReactElement[] = [];
  let cursor = 0;

  for (const match of fragment.matchAll(TAGS)) {
    const start = match.index;

    if (start > cursor) {
      parts.push(<span key={parts.length}>{decode(fragment.slice(cursor, start))}</span>);
    }

    parts.push(
      <Box key={parts.length} component="mark" sx={{ bgcolor: 'transparent', color: 'primary.main', fontWeight: 600 }}>
        {decode(match[1] ?? '')}
      </Box>,
    );

    cursor = start + match[0].length;
  }

  if (cursor < fragment.length) {
    parts.push(<span key={parts.length}>{decode(fragment.slice(cursor))}</span>);
  }

  return <>{parts}</>;
}
