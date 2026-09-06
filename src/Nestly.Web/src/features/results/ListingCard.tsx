import BedOutlinedIcon from '@mui/icons-material/BedOutlined';
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined';
import ShowerOutlinedIcon from '@mui/icons-material/ShowerOutlined';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import { Box, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import { formatBathrooms, formatBedrooms, formatMoney, truncate } from '../../format';
import { HighlightedText } from './HighlightedText';
import type { ListingHit } from '../../api/types';
import type { ReactElement } from 'react';

const SNIPPET_LENGTH = 180;

export function ListingCard({ hit }: { hit: ListingHit }): ReactElement {
  const { listing } = hit;

  return (
    <Card component="li" sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.25, flexGrow: 1 }}>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', gap: 2 }}>
          <Box>
            <Typography variant="subtitle1" component="h2" sx={{ lineHeight: 1.3 }}>
              {listing.title}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {listing.neighborhood} · {listing.borough}
            </Typography>
          </Box>

          <Box sx={{ textAlign: 'right', flexShrink: 0 }}>
            <Typography variant="subtitle1" component="p" sx={{ color: 'primary.main' }}>
              {formatMoney(listing.monthlyRent)}
            </Typography>
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              per month
            </Typography>
          </Box>
        </Stack>

        <Stack direction="row" sx={{ gap: 0.75, flexWrap: 'wrap' }}>
          <Chip icon={<BedOutlinedIcon />} label={formatBedrooms(listing.bedrooms)} variant="outlined" />
          <Chip icon={<ShowerOutlinedIcon />} label={formatBathrooms(listing.bathrooms)} variant="outlined" />
          <Chip icon={<GroupOutlinedIcon />} label={`Sleeps ${String(listing.accommodates)}`} variant="outlined" />
          {listing.reviewScore !== undefined && (
            <Chip icon={<StarRoundedIcon />} label={listing.reviewScore.toFixed(2)} variant="outlined" />
          )}
        </Stack>

        {/* The server's snippets when the query matched the description, the opening line
            otherwise, so a card is never a bare title. */}
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {hit.highlights.length > 0
            ? hit.highlights.map((fragment, index) => (
                <Box component="span" key={index}>
                  {index > 0 && ' … '}
                  <HighlightedText fragment={fragment} />
                </Box>
              ))
            : truncate(listing.description, SNIPPET_LENGTH)}
        </Typography>

        <Typography variant="caption" sx={{ color: 'text.secondary', mt: 'auto' }}>
          {listing.roomType} · {formatMoney(listing.pricePerNight)}/night
        </Typography>
      </CardContent>
    </Card>
  );
}
