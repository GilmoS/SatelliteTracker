import { useQuery } from '@tanstack/react-query'
import apiClient from '../services/apiClient'
import type { TrackPoint, TrackResponse } from '../types'

export function useTrack(satelliteId: string) {
  return useQuery<TrackPoint[]>({
    queryKey: ['track', satelliteId],
    queryFn: async () => {
      const { data } = await apiClient.get<TrackResponse>(`/api/satellites/${satelliteId}/track`)
      return data.points
    },
    staleTime: 5 * 60 * 1000,
    refetchInterval: 5 * 60 * 1000,
    enabled: !!satelliteId,
  })
}
