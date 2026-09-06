import { Box, useTheme } from '@mui/material';
import { useEffect, useRef } from 'react';
import { CircleMarker, MapContainer, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import { formatCount, formatMoney } from '../../format';
import { DEFAULT_VIEW } from '../search/searchState';
import type { GeoBounds, MapResponse } from '../../api/types';
import type { LatLngBoundsExpression } from 'leaflet';
import type { ReactElement } from 'react';

import 'leaflet/dist/leaflet.css';

const PIN_RADIUS = 6;
const CLUSTER_MIN_RADIUS = 11;
const CLUSTER_MAX_RADIUS = 34;

export type ViewChange = (bounds: GeoBounds, zoom: number) => void;

function toBounds(map: ReturnType<typeof useMap>): GeoBounds {
  const bounds = map.getBounds();

  return {
    topLat: bounds.getNorth(),
    leftLon: bounds.getWest(),
    bottomLat: bounds.getSouth(),
    rightLon: bounds.getEast(),
  };
}

/** Reports the viewport up, on settle and once on mount. */
// The map is never driven back the other way -- nothing calls setView from state -- so there is
// no pan/render feedback loop to break.
function ViewportReporter({ onChange }: { onChange: ViewChange }): null {
  // react-leaflet re-subscribes whenever the handlers object changes, which is every render, so
  // this closure is always the current one.
  const map = useMapEvents({
    moveend: () => { onChange(toBounds(map), map.getZoom()); },
  });

  // Mount only, through a ref that is deliberately never updated: the first render's callback
  // already reads the state the map was built from, and depending on `onChange` here would
  // re-emit on every render.
  const emitOnce = useRef(onChange);

  useEffect(() => {
    // Without this the list would show the whole city while the map shows one borough, until the
    // user happened to pan.
    emitOnce.current(toBounds(map), map.getZoom());
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
  /** The listing the cursor or a click is currently on, in either pane. */
  highlightedId: string | undefined;

  onSelect: (id: string) => void;
}

export function MapPane({ data, initialBounds, onViewChange, highlightedId, onSelect }: MapPaneProps): ReactElement {
  const theme = useTheme();

  // Read once, on mount: MapContainer treats these as initial state, and the map owns its view
  // from then on.
  const bounds: LatLngBoundsExpression | undefined = initialBounds && [
    [initialBounds.bottomLat, initialBounds.leftLon],
    [initialBounds.topLat, initialBounds.rightLon],
  ];

  const largest = Math.max(...(data?.clusters ?? []).map((cluster) => cluster.count), 1);

  return (
    <Box
      sx={{
        height: '100%',
        minHeight: 320,
        borderRadius: 1,
        overflow: 'hidden',
        border: 1,
        borderColor: 'divider',
        '& .leaflet-container': { height: '100%', width: '100%', bgcolor: 'background.default' },

      }}
    >
      <MapContainer
        {...(bounds ? { bounds } : { center: DEFAULT_VIEW.center, zoom: DEFAULT_VIEW.zoom })}
        scrollWheelZoom
        style={{ height: '100%' }}
      >
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          maxZoom={19}
        />
        <ViewportReporter onChange={onViewChange} />

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
            eventHandlers={{ click: () => { onSelect(pin.id); } }}
            pathOptions={{
              color: theme.palette.common.white,
              fillColor: pin.id === highlightedId ? theme.palette.secondary.main : theme.palette.primary.main,
              fillOpacity: 1,
              weight: 2,
            }}
          >
            <Tooltip direction="top" offset={[0, -6]}>{formatMoney(pin.monthlyRent)}</Tooltip>
          </CircleMarker>
        ))}
      </MapContainer>
    </Box>
  );
}
