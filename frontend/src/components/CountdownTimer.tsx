import { useState, useEffect } from 'react'
import type { Pass } from '../types'

interface Props {
  passes: Pass[] | undefined
  satelliteName: string
}

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

export function CountdownTimer({ passes, satelliteName }: Props) {
  const [remaining, setRemaining] = useState(0)

  const nextPass = passes
    ?.slice()
    .sort((a, b) => new Date(a.aos).getTime() - new Date(b.aos).getTime())
    .find((p) => new Date(p.aos).getTime() > Date.now())

  useEffect(() => {
    if (!nextPass) return

    const update = () => {
      const diff = Math.max(0, Math.floor((new Date(nextPass.aos).getTime() - Date.now()) / 1000))
      setRemaining(diff)
    }

    update()
    const interval = setInterval(update, 1000)
    return () => clearInterval(interval)
  }, [nextPass])

  const h = Math.floor(remaining / 3600)
  const m = Math.floor((remaining % 3600) / 60)
  const s = remaining % 60

  if (!nextPass) {
    return (
      <div style={barStyle}>
        <span style={{ color: '#166534', fontSize: '11px', letterSpacing: '0.1em' }}>
          NO UPCOMING PASSES
        </span>
      </div>
    )
  }

  return (
    <div style={barStyle}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
        <span
          style={{
            fontFamily: 'monospace',
            fontSize: '28px',
            fontWeight: 'bold',
            color: '#22c55e',
            textShadow: '0 0 12px #22c55e, 0 0 24px rgba(34,197,94,0.4)',
            letterSpacing: '0.05em',
          }}
        >
          {pad(h)}:{pad(m)}:{pad(s)}
        </span>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
          <span style={labelStyle}>{satelliteName}</span>
          <span style={dimStyle}>ORB #{nextPass.orbitNumber}</span>
        </div>
        <div style={divider} />
        <div style={{ display: 'flex', gap: '20px' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
            <span style={labelStyle}>MAX EL</span>
            <span style={valueStyle}>{nextPass.maxElevation.toFixed(1)}°</span>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
            <span style={labelStyle}>DURATION</span>
            <span style={valueStyle}>{Math.round(nextPass.durationSec / 60)} MIN</span>
          </div>
        </div>
      </div>
    </div>
  )
}

const barStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  padding: '10px 16px',
  borderBottom: '0.5px solid #1a3a1a',
  background: '#090d09',
  flexShrink: 0,
}

const labelStyle: React.CSSProperties = {
  fontFamily: 'monospace',
  fontSize: '9px',
  color: '#166534',
  letterSpacing: '0.12em',
  textTransform: 'uppercase',
}

const valueStyle: React.CSSProperties = {
  fontFamily: 'monospace',
  fontSize: '13px',
  color: '#4ade80',
}

const dimStyle: React.CSSProperties = {
  fontFamily: 'monospace',
  fontSize: '10px',
  color: '#166534',
  letterSpacing: '0.08em',
}

const divider: React.CSSProperties = {
  width: '0.5px',
  height: '32px',
  background: '#1a3a1a',
}
