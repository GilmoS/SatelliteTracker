import type { Pass, Satellite } from '../types'

interface Props {
  passes: Pass[] | undefined
  satellites: Satellite[] | undefined
}

const IL_FMT = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Jerusalem',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

const DATE_FMT = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
})

function elevColor(el: number): string {
  if (el >= 30) return '#22c55e'
  if (el >= 15) return '#eab308'
  return '#374151'
}

export function PassTable({ passes, satellites }: Props) {
  const now = Date.now()
  const sorted = (passes ?? [])
    .slice()
    .sort((a, b) => new Date(a.aos).getTime() - new Date(b.aos).getTime())

  const nextIdx = sorted.findIndex((p) => new Date(p.aos).getTime() > now)

  return (
    <div style={{ overflowY: 'auto', flex: 1 }}>
      {sorted.map((pass, idx) => {
        const sat = satellites?.find((s) => s.id === pass.satelliteId)
        const aosDate = new Date(pass.aos)
        const isNext = idx === nextIdx

        return (
          <div
            key={pass.id}
            style={{
              padding: '8px 12px',
              borderBottom: '0.5px solid #1a3a1a',
              borderLeft: isNext ? '2px solid #22c55e' : '2px solid transparent',
              background: isNext ? '#0a1a0a' : 'transparent',
            }}
          >
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '3px',
              }}
            >
              <span style={{ color: '#4ade80', fontSize: '10px', fontFamily: 'monospace', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
                {sat?.name ?? pass.satelliteId.slice(0, 8)}
              </span>
              <span style={{ color: elevColor(pass.maxElevation), fontSize: '11px', fontFamily: 'monospace' }}>
                {pass.maxElevation.toFixed(0)}°
              </span>
            </div>
            <div style={{ color: '#166534', fontSize: '9px', fontFamily: 'monospace', letterSpacing: '0.08em', marginBottom: '3px' }}>
              ORB #{pass.orbitNumber}
            </div>
            <div style={{ color: '#4ade80', fontSize: '10px', fontFamily: 'monospace' }}>
              {DATE_FMT.format(aosDate)}
            </div>
            <div style={{ display: 'flex', gap: '8px', marginTop: '2px' }}>
              <span style={{ color: '#4ade80', fontSize: '10px', fontFamily: 'monospace' }}>
                IL {IL_FMT.format(aosDate)}
              </span>
              <span style={{ color: '#166534', fontSize: '10px', fontFamily: 'monospace' }}>
                UTC {aosDate.toUTCString().split(' ')[4]}
              </span>
            </div>
          </div>
        )
      })}
      {sorted.length === 0 && (
        <div style={{ padding: '16px 12px', color: '#166534', fontSize: '10px', fontFamily: 'monospace', textAlign: 'center' }}>
          NO PASSES
        </div>
      )}
    </div>
  )
}
