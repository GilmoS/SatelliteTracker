import { useQuery } from '@tanstack/react-query'
import apiClient from '../services/apiClient'
import type { SatellitePosition } from '../types'

export function usePosition(satelliteId: string) {
  return useQuery({
    queryKey: ['position', satelliteId],
    queryFn: async () => {
      const { data } = await apiClient.get<SatellitePosition>(`/api/satellites/${satelliteId}/position`)
      return data
    },
    refetchInterval: 30000,
    enabled: !!satelliteId,
  })
}
