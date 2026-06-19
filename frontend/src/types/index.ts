export interface Satellite {
  id: string;
  name: string;
  noradId: number;
  isActive: boolean;
  isDefault: boolean;
}

export interface Pass {
  id: string;
  satelliteId: string;
  orbitNumber: number;
  aos: string;
  los: string;
  maxElevation: number;
  aosAzimuth: number;
  losAzimuth: number;
  durationSec: number;
}

export interface SatellitePosition {
  noradId: number;
  satName: string;
  latitude: number;
  longitude: number;
  altitude: number;
  azimuth: number;
  elevation: number;
  timestamp: number;
}

export interface TrackPoint {
  latitude: number;
  longitude: number;
  altitude: number;
  timestamp: number;
}

export interface TrackResponse {
  noradId: number;
  satName: string;
  points: TrackPoint[];
}
