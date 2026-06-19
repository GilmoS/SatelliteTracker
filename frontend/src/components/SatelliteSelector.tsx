import type { Satellite } from '../types'

interface Props {
  satellites: Satellite[] | undefined
  selectedId: string
  onSelect: (id: string) => void
}

export function SatelliteSelector({ satellites, selectedId, onSelect }: Props) {
  return (
    <div>
      {satellites?.map((sat) => (
        <button
          key={sat.id}
          onClick={() => onSelect(sat.id)}
          style={{
            display: 'block',
            width: '100%',
            textAlign: 'left',
            padding: '8px 12px',
            background: sat.id === selectedId ? '#0f2a0f' : 'transparent',
            border: 'none',
            borderLeft: sat.id === selectedId ? '2px solid #22c55e' : '2px solid transparent',
            color: sat.id === selectedId ? '#22c55e' : '#4ade80',
            fontFamily: 'monospace',
            fontSize: '11px',
            letterSpacing: '0.08em',
            cursor: 'pointer',
            textTransform: 'uppercase',
          }}
        >
          {sat.name}
          {sat.isDefault && (
            <span style={{ color: '#166534', marginLeft: '6px' }}>★</span>
          )}
        </button>
      ))}
    </div>
  )
}
