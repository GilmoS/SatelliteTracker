import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { MapContainer, TileLayer, Polyline, Circle, Marker, Popup } from 'react-leaflet'
import type { SatellitePosition, TrackPoint } from '../types'

import iconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png'
import iconUrl from 'leaflet/dist/images/marker-icon.png'
import shadowUrl from 'leaflet/dist/images/marker-shadow.png'

delete (L.Icon.Default.prototype as any)._getIconUrl
L.Icon.Default.mergeOptions({ iconRetinaUrl, iconUrl, shadowUrl })

const crosshairIcon = L.divIcon({
  html: `
    <div style="width:24px;height:24px;position:relative;">
      <div style="position:absolute;top:50%;left:0;right:0;height:1px;background:#22c55e;transform:translateY(-50%);"></div>
      <div style="position:absolute;left:50%;top:0;bottom:0;width:1px;background:#22c55e;transform:translateX(-50%);"></div>
      <div style="position:absolute;top:50%;left:50%;width:8px;height:8px;border:1.5px solid #22c55e;border-radius:50%;transform:translate(-50%,-50%);box-shadow:0 0 6px #22c55e;"></div>
    </div>
  `,
  className: '',
  iconSize: [24, 24],
  iconAnchor: [12, 12],
  popupAnchor: [0, -14],
})

interface Props {
  satelliteName: string
  position: SatellitePosition | undefined
  track: TrackPoint[] | undefined
}

export function SatelliteMap({ satelliteName, position, track }: Props) {
  const trackPositions: [number, number][] = (track ?? []).map((p) => [p.latitude, p.longitude])

  return (
    <MapContainer
      center={[31.5, 35.0]}
      zoom={4}
      style={{ height: '100%', width: '100%' }}
      zoomControl
    >
      <TileLayer
        url="https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
        attribution="&copy; CartoDB"
      />

      {trackPositions.length > 1 && (
        <Polyline positions={trackPositions} color="#22c55e" opacity={0.7} weight={1.5} />
      )}

      {position && (
        <>
          <Circle
            center={[position.latitude, position.longitude]}
            radius={2000000}
            color="#22c55e"
            fillColor="#22c55e"
            fillOpacity={0.05}
            opacity={0.15}
            weight={1}
          />
          <Marker
            position={[position.latitude, position.longitude]}
            icon={crosshairIcon}
          >
            <Popup>
              <div
                style={{
                  fontFamily: 'monospace',
                  fontSize: '11px',
                  color: '#4ade80',
                  background: '#090d09',
                  padding: '6px 8px',
                  lineHeight: '1.6',
                  minWidth: '130px',
                }}
              >
                <div style={{ color: '#22c55e', marginBottom: '4px', letterSpacing: '0.06em' }}>
                  {satelliteName}
                </div>
                <div>ALT: {position.altitude.toFixed(1)} km</div>
                <div>AZ: {position.azimuth.toFixed(1)}°</div>
                <div>EL: {position.elevation.toFixed(1)}°</div>
              </div>
            </Popup>
          </Marker>
        </>
      )}
    </MapContainer>
  )
}
