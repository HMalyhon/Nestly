import BedOutlinedIcon from '@mui/icons-material/BedOutlined';
import GroupOutlinedIcon from '@mui/icons-material/GroupOutlined';
import ShowerOutlinedIcon from '@mui/icons-material/ShowerOutlined';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import { Box, ButtonBase, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import { visuallyHidden } from '@mui/utils';
import { formatBathrooms, formatBedrooms, formatMoney, truncate } from '../../format';
import { HighlightedText } from './HighlightedText';
import type { ListingHit } from '../../api/types';
import type { ReactElement } from 'react';

const SNIPPET_LENGTH = 180;

interface ListingCardProps {
  hit: ListingHit;
  isHighlighted: boolean;
  isSelected: boolean;
  onHover: (id: string | undefined) => void;
  onSelect: (id: string | undefined) => void;
}

export function ListingCard({
  hit, isHighlighted, isSelected, onHover, onSelect,
}: ListingCardProps): ReactElement {
  const { listing } = hit;
  const titleId = `listing-${listing.id}-title`;
  const placeId = `listing-${listing.id}-place`;
  const priceId = `listing-${listing.id}-price`;
  const actionId = `listing-${listing.id}-action`;

  return (
    <Card
      component="li"
      data-listing={listing.id}
      onMouseEnter={() => { onHover(listing.id); }}
      onMouseLeave={() => { onHover(undefined); }}
      sx={{
        position: 'relative',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        borderColor: isHighlighted ? 'secondary.main' : undefined,
        boxShadow: isHighlighted ? 3 : 0,
        transition: 'box-shadow 120ms ease-out, border-color 120ms ease-out',
      }}
    >
      <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 1.25, flexGrow: 1, width: '100%' }}>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start', gap: 2 }}>
          <Box>
            <Typography variant="subtitle1" component="h2" sx={{ lineHeight: 1.3 }}>
              {/* The control sits inside the heading and is stretched over the card by ::after, so
                  the whole card stays clickable without the content being swallowed by a button. */}
              <ButtonBase
                onClick={() => { onSelect(listing.id); }}
                onFocus={() => { onHover(listing.id); }}
                onBlur={() => { onHover(undefined); }}
                aria-pressed={isSelected}
                aria-labelledby={`${titleId} ${placeId} ${priceId} ${actionId}`}
                sx={{
                  textAlign: 'left',
                  font: 'inherit',
                  color: 'inherit',
                  borderRadius: 1,
                  '&::after': { content: '""', position: 'absolute', inset: 0, borderRadius: 'inherit' },
                }}
              >
                <span id={titleId}>{listing.title}</span>
              </ButtonBase>
            </Typography>
            <Typography id={placeId} variant="body2" sx={{ color: 'text.secondary' }}>
              {listing.neighborhood} · {listing.borough}
            </Typography>
            <Box component="span" id={actionId} sx={visuallyHidden}>show on the map</Box>
          </Box>

          <Box sx={{ textAlign: 'right', flexShrink: 0 }}>
            <Typography id={priceId} variant="subtitle1" component="p" sx={{ color: 'primary.main' }}>
              {formatMoney(listing.monthlyRent)}
              <Box component="span" sx={visuallyHidden}> per month</Box>
            </Typography>
            <Typography variant="caption" aria-hidden sx={{ color: 'text.secondary' }}>
              per month
            </Typography>
          </Box>
        </Stack>

        <Stack direction="row" sx={{ gap: 0.75, flexWrap: 'wrap' }}>
          <Chip icon={<BedOutlinedIcon />} label={formatBedrooms(listing.bedrooms)} variant="outlined" />
          <Chip icon={<ShowerOutlinedIcon />} label={formatBathrooms(listing.bathrooms)} variant="outlined" />
          <Chip icon={<GroupOutlinedIcon />} label={`Sleeps ${String(listing.accommodates)}`} variant="outlined" />
          {listing.reviewScore !== undefined && (
            <Chip
              icon={<StarRoundedIcon />}
              variant="outlined"

              // The star is aria-hidden like every MUI icon, so the chip read as a bare "4.87".
              label={(
                <>
                  {listing.reviewScore.toFixed(2)}
                  <Box component="span" sx={visuallyHidden}> out of 5, average review score</Box>
                </>
              )}
            />
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
