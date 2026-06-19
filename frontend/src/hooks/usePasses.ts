import { useQuery } from '@tanstack/react-query'
import apiClient from '../services/apiClient'
import type { Pass } from '../types'

export function usePasses(satelliteId: string) {
  return useQuery({
    queryKey: ['passes', satelliteId],
    queryFn: async () => {
      const { data } = await apiClient.get<Pass[]>(`/api/passes/${satelliteId}`)
      return data
    },
    staleTime: 60 * 60 * 1000,
    enabled: !!satelliteId,
  })
}
