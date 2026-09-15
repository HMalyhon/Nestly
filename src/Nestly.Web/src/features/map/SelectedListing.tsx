import CloseIcon from '@mui/icons-material/Close';
import StarRoundedIcon from '@mui/icons-material/StarRounded';
import { Alert, Box, Chip, IconButton, Paper, Skeleton, Stack, Typography } from '@mui/material';
import { visuallyHidden } from '@mui/utils';
import { formatBathrooms, formatBedrooms, formatMoney, truncate } from '../../format';
import type { Listing } from '../../api/types';
import type { ReactElement } from 'react';

const BLURB_LENGTH = 150;
const AMENITY_COUNT = 5;

interface SelectedListingProps {
  listing: Listing | undefined;
  isPending: boolean;
  error: string | undefined;
  onClose: () => void;
}

/**
 * The detail panel for whichever listing is pinned, over the map rather than anchored to it.
 */
// A Leaflet popup would sit above the marker, where a card this tall is cut off by the top of the
// pane -- and the option that fixes that, autoPan, moves the map, which this app reads as the user
// panning and answers by refiltering the results out from under the click.
export function SelectedListing({
  listing, isPending, error, onClose,
}: SelectedListingProps): ReactElement | null {
  // Without the error case this returned null on a failed fetch, so clicking an off-page pin
  // selected it and then showed nothing at all.
  if (!listing && !isPending && error === undefined) {
    return null;
  }

  return (
    <Paper
      elevation={6}
      component="section"
      aria-label="Selected listing"
      sx={{
        position: 'absolute',
        left: 8,
        right: 8,

        // Clears Leaflet's attribution bar rather than covering it: the OpenStreetMap credit has
        // to stay visible, and it sits in this corner.
        bottom: 24,
        zIndex: 1000,
        p: 1.5,
        borderRadius: 1,
        maxHeight: '55%',
        overflowY: 'auto',
      }}
    >
      <IconButton
        size="small"
        onClick={onClose}
        aria-label="Close listing details"
        sx={{ position: 'absolute', top: 4, right: 4 }}
      >
        <CloseIcon fontSize="small" />
      </IconButton>

      {error !== undefined && !listing ? (
        <Alert severity="error" sx={{ mr: 3 }}>{error}</Alert>
      ) : !listing ? (
        <Stack sx={{ gap: 0.5, pr: 4 }}>
          <Skeleton variant="text" width="60%" height={26} />
          <Skeleton variant="text" width="35%" />
          <Skeleton variant="text" width="80%" />
        </Stack>
      ) : (
        <Stack sx={{ gap: 1, pr: 4 }}>
          <Box>
            <Typography variant="subtitle1" component="h3" sx={{ lineHeight: 1.3 }}>
              {listing.title}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {listing.neighborhood} · {listing.borough} · {listing.roomType}
            </Typography>
          </Box>

          <Stack direction="row" sx={{ alignItems: 'baseline', gap: 1, flexWrap: 'wrap' }}>
            <Typography variant="subtitle1" component="p" sx={{ color: 'primary.main' }}>
              {formatMoney(listing.monthlyRent)}
            </Typography>
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              per month · {formatMoney(listing.pricePerNight)}/night · {listing.minimumNights} night minimum
            </Typography>
          </Stack>

          <Stack direction="row" sx={{ gap: 0.75, flexWrap: 'wrap' }}>
            <Chip label={formatBedrooms(listing.bedrooms)} variant="outlined" />
            <Chip label={formatBathrooms(listing.bathrooms)} variant="outlined" />
            <Chip label={`Sleeps ${String(listing.accommodates)}`} variant="outlined" />
            <Chip label={listing.propertyType} variant="outlined" />
            {listing.reviewScore !== undefined && (
              <Chip
                icon={<StarRoundedIcon />}
                variant="outlined"
                label={(
                  <>
                    {listing.reviewScore.toFixed(2)}
                    <Box component="span" sx={visuallyHidden}> out of 5, average review score</Box>
                  </>
                )}
              />
            )}
          </Stack>

          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {truncate(listing.description, BLURB_LENGTH)}
          </Typography>

          {listing.amenities.length > 0 && (
            <Typography variant="caption" sx={{ color: 'text.secondary' }}>
              {listing.amenities.slice(0, AMENITY_COUNT).join(' · ')}
              {listing.amenities.length > AMENITY_COUNT
                && ` · +${String(listing.amenities.length - AMENITY_COUNT)} more`}
            </Typography>
          )}
        </Stack>
      )}
    </Paper>
  );
}
