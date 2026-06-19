import { useQuery } from '@tanstack/react-query'
import apiClient from '../services/apiClient'
import type { Satellite } from '../types'

export function useSatellites() {
  return useQuery({
    queryKey: ['satellites'],
    queryFn: async () => {
      const { data } = await apiClient.get<Satellite[]>('/api/satellites')
      return data
    },
    staleTime: 5 * 60 * 1000,
  })
}
