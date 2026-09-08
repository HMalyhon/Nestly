import { Alert, Box, useTheme } from '@mui/material';
import { useEffect, useRef } from 'react';
import { CircleMarker, MapContainer, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import { DomEvent } from 'leaflet';
import { formatCount, formatMoney, truncate } from '../../format';
import { DEFAULT_VIEW, MIN_ZOOM } from '../search/searchState';
import { SelectedListing } from './SelectedListing';
import type { GeoBounds, Listing, MapPin, MapResponse } from '../../api/types';
import type { LatLngBoundsExpression } from 'leaflet';
import type { ReactElement } from 'react';

import 'leaflet/dist/leaflet.css';

const PIN_RADIUS = 6;
const SELECTED_RADIUS = 10;
const CLUSTER_MIN_RADIUS = 11;
const CLUSTER_MAX_RADIUS = 34;

/** Longer than this and the label covers the neighbourhood it is pointing at. */
const LABEL_LENGTH = 38;

/** Whether the user moved the map, or it is reporting the view it was built with. */
export type ViewCause = 'mount' | 'pan';

export type ViewChange = (bounds: GeoBounds, zoom: number, cause: ViewCause) => void;

function clamp(value: number, limit: number): number {
  return Math.min(Math.max(value, -limit), limit);
}

function toBounds(map: ReturnType<typeof useMap>): GeoBounds {
  const bounds = map.getBounds();

  // Zoomed out far enough that the pane is wider than the world, Leaflet reports the overhang
  // unwrapped -- a west of -190, which the API rejects and which the URL would then keep serving.
  return {
    topLat: clamp(bounds.getNorth(), 90),
    leftLon: clamp(bounds.getWest(), 180),
    bottomLat: clamp(bounds.getSouth(), 90),
    rightLon: clamp(bounds.getEast(), 180),
  };
}

interface SelectedPoint {
  lat: number;
  lon: number;
  price: string;
  title: string | undefined;
}

/** Where to draw the label, from whichever source knows this listing. */
// The results carry a title, but only for the twenty on the page; the pins carry everything in
// view, but only its rent. A pin clicked while the list is on page one of forty is the second case.
function locate(
  selected: Listing | undefined,
  selectedId: string | undefined,
  pins: MapPin[],
): SelectedPoint | undefined {
  if (selected) {
    return {
      lat: selected.location.lat,
      lon: selected.location.lon,
      price: formatMoney(selected.monthlyRent),
      title: truncate(selected.title, LABEL_LENGTH),
    };
  }

  const pin = pins.find((candidate) => candidate.id === selectedId);

  return pin && { lat: pin.lat, lon: pin.lon, price: formatMoney(pin.monthlyRent), title: undefined };
}

/** Clears the selection when the click did not land on a marker. */
// Markers stop their own clicks from propagating, so anything reaching the map is background.
function BackgroundClick({ onClear }: { onClear: () => void }): null {
  useMapEvents({ click: onClear });

  return null;
}

/** Reports the viewport up, on settle and once on mount. */
// The map is never driven back the other way -- nothing calls setView from state -- so there is
// no pan/render feedback loop to break.
function ViewportReporter({ onChange }: { onChange: ViewChange }): null {
  // react-leaflet re-subscribes whenever the handlers object changes, which is every render, so
  // this closure is always the current one.
  const map = useMapEvents({
    moveend: () => { onChange(toBounds(map), map.getZoom(), 'pan'); },
  });

  // Mount only, through a ref that is deliberately never updated: the first render's callback
  // already reads the state the map was built from, and depending on `onChange` here would
  // re-emit on every render.
  const emitOnce = useRef(onChange);

  useEffect(() => {
    // Without this the list would show the whole city while the map shows one borough, until the
    // user happened to pan.
    emitOnce.current(toBounds(map), map.getZoom(), 'mount');
  }, [map]);

  return null;
}

function clusterRadius(count: number, largest: number): number {
  const scale = Math.sqrt(count / largest);

  return CLUSTER_MIN_RADIUS + scale * (CLUSTER_MAX_RADIUS - CLUSTER_MIN_RADIUS);
}

interface MapPaneProps {
  data: MapResponse | undefined;
  initialBounds: GeoBounds | undefined;
  onViewChange: ViewChange;
  /** Why the markers are missing, when they are. */
  error: string | undefined;

  /** The listing the cursor is on, in either pane. */
  highlightedId: string | undefined;

  /** The chosen listing, drawn with a label. Comes from the search results, not from `data`:
      above the pin limit the map clusters and has no individual markers to point at. */
  selected: Listing | undefined;

  selectedId: string | undefined;

  /** True while the detail for an off-page pin is still in flight. */
  isLoadingSelected: boolean;

  onSelect: (id: string | undefined) => void;
}

export function MapPane({
  data, initialBounds, onViewChange, error, highlightedId, selected, selectedId,
  isLoadingSelected, onSelect,
}: MapPaneProps): ReactElement {
  const theme = useTheme();

  // Read once, on mount: MapContainer treats these as initial state, and the map owns its view
  // from then on.
  const bounds: LatLngBoundsExpression | undefined = initialBounds && [
    [initialBounds.bottomLat, initialBounds.leftLon],
    [initialBounds.topLat, initialBounds.rightLon],
  ];

  const largest = Math.max(...(data?.clusters ?? []).map((cluster) => cluster.count), 1);
  const point = locate(selected, selectedId, data?.pins ?? []);

  return (
    <Box
      sx={{
        height: '100%',
        minHeight: 320,
        position: 'relative',
        borderRadius: 1,
        overflow: 'hidden',
        border: 1,
        borderColor: 'divider',
        '& .leaflet-container': { height: '100%', width: '100%', bgcolor: 'background.default' },

      }}
    >
      {/* Over the map rather than in place of it: the tiles are still good, it is only the markers
          that are missing, and saying so beats an empty city. */}
      {error !== undefined && (
        <Alert
          severity="warning"
          sx={{ position: 'absolute', top: 8, left: 8, right: 8, zIndex: 1000 }}
        >
          {error}
        </Alert>
      )}

      <SelectedListing
        listing={selected}
        isPending={isLoadingSelected}
        onClose={() => { onSelect(undefined); }}
      />

      <MapContainer
        {...(bounds ? { bounds } : { center: DEFAULT_VIEW.center, zoom: DEFAULT_VIEW.zoom })}
        scrollWheelZoom
        minZoom={MIN_ZOOM}
        style={{ height: '100%' }}
      >
        {/* Both from settings, and always as a pair: a URL pointed at another provider while the
            credit still reads OpenStreetMap cites the wrong source. */}
        <TileLayer
          url={import.meta.env.VITE_MAP_TILE_URL}
          attribution={import.meta.env.VITE_MAP_TILE_ATTRIBUTION}
          maxZoom={19}
        />
        <ViewportReporter onChange={onViewChange} />
        <BackgroundClick onClear={() => { onSelect(undefined); }} />

        {data?.clusters.map((cluster) => (
          <CircleMarker
            key={`${String(cluster.lat)},${String(cluster.lon)}`}
            center={[cluster.lat, cluster.lon]}
            radius={clusterRadius(cluster.count, largest)}
            pathOptions={{
              color: theme.palette.primary.main,
              fillColor: theme.palette.primary.main,
              fillOpacity: 0.45,
              weight: 1,
            }}
          >
            {/* One tooltip, on hover. Permanent labels on 215 cells cover the city they are
                describing; size already carries the density. */}
            <Tooltip direction="top" offset={[0, -8]}>
              {formatCount(cluster.count)} listings · median {formatMoney(cluster.medianRent)}
            </Tooltip>
          </CircleMarker>
        ))}

        {data?.pins.map((pin) => (
          <CircleMarker
            key={pin.id}
            center={[pin.lat, pin.lon]}
            radius={pin.id === highlightedId ? PIN_RADIUS * 1.9 : PIN_RADIUS}
            // Stopped, or the click carries on to the map and clears what it just selected.
            eventHandlers={{ click: (event) => { DomEvent.stopPropagation(event); onSelect(pin.id); } }}
            pathOptions={{
              color: theme.palette.common.white,
              fillColor: pin.id === highlightedId ? theme.palette.secondary.main : theme.palette.primary.main,
              fillOpacity: 1,
              weight: 2,
            }}
          >
            {/* Suppressed on the selected pin: its permanent label is already at this spot, and
                the two stack into the same price printed twice. */}
            {pin.id !== selectedId && (
              <Tooltip direction="top" offset={[0, -6]}>{formatMoney(pin.monthlyRent)}</Tooltip>
            )}
          </CircleMarker>
        ))}

        {/* Last, so it draws over the pin already at that spot. The only permanent label on the
            map: one is what makes a listing findable among hundreds of identical dots, which
            enlarging its pin never did. */}
        {point && (
          <CircleMarker
            center={[point.lat, point.lon]}
            radius={SELECTED_RADIUS}
            eventHandlers={{ click: (event) => { DomEvent.stopPropagation(event); onSelect(selectedId); } }}
            pathOptions={{
              color: theme.palette.common.white,
              fillColor: theme.palette.secondary.main,
              fillOpacity: 1,
              weight: 3,
            }}
          >
            {/* autoPan is off by default on tooltips, and has to stay off: a pan here would be
                reported as the user moving the map, refiltering the results under the click. */}
            <Tooltip permanent direction="top" offset={[0, -12]}>
              <Box component="span" sx={{ fontWeight: 600 }}>{point.price}</Box>
              {point.title !== undefined && ` · ${point.title}`}
            </Tooltip>
          </CircleMarker>
        )}
      </MapContainer>
    </Box>
  );
}
