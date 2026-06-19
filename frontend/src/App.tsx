import { useState, useEffect } from 'react'
import { useSatellites } from './hooks/useSatellites'
import { usePasses } from './hooks/usePasses'
import { usePosition } from './hooks/usePosition'
import { useTrack } from './hooks/useTrack'
import { SatelliteSelector } from './components/SatelliteSelector'
import { CountdownTimer } from './components/CountdownTimer'
import { PassTable } from './components/PassTable'
import { SatelliteMap } from './components/SatelliteMap'

const IL_TIME_FMT = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Asia/Jerusalem',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

export default function App() {
  const { data: satellites } = useSatellites()
  const [selectedId, setSelectedId] = useState('')
  const [now, setNow] = useState(new Date())

  useEffect(() => {
    if (selectedId || !satellites?.length) return
    const def = satellites.find((s) => s.isDefault) ?? satellites[0]
    setSelectedId(def.id)
  }, [satellites, selectedId])

  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(t)
  }, [])

  const { data: passes } = usePasses(selectedId)
  const { data: position, dataUpdatedAt } = usePosition(selectedId)
  const { data: track } = useTrack(selectedId)

  const selected = satellites?.find((s) => s.id === selectedId)

  return (
    <div style={styles.root}>
      {/* ── 3-column body ─────────────────────────────────────── */}
      <div style={styles.body}>

        {/* LEFT PANEL */}
        <div style={styles.leftPanel}>
          <div style={styles.panelHeader}>SATELLITES</div>
          <SatelliteSelector
            satellites={satellites}
            selectedId={selectedId}
            onSelect={setSelectedId}
          />

          <div style={styles.sectionDivider} />

          <div style={styles.panelHeader}>ORBITAL DATA</div>
          <div style={styles.dataBlock}>
            {position ? (
              <>
                <DataRow label="LAT" value={`${position.latitude.toFixed(4)}°`} />
                <DataRow label="LON" value={`${position.longitude.toFixed(4)}°`} />
                <DataRow label="ALT" value={`${position.altitude.toFixed(1)} km`} />
                <DataRow label="AZ" value={`${position.azimuth.toFixed(1)}°`} />
                <DataRow label="EL" value={`${position.elevation.toFixed(1)}°`} />
              </>
            ) : (
              <span style={styles.dimText}>— NO DATA —</span>
            )}
          </div>

          <div style={styles.sectionDivider} />

          <div style={styles.panelHeader}>OBSERVER</div>
          <div style={styles.dataBlock}>
            <DataRow label="SITE" value="BGN" />
            <DataRow label="LAT" value="32.00N" />
            <DataRow label="LON" value="34.88E" />
            <DataRow label="ALT" value="135 m" />
            <DataRow label="MIN EL" value="5°" />
          </div>

          <div style={{ flex: 1 }} />

          <div style={styles.clockBlock}>
            <span style={styles.dimLabel}>LOCAL</span>
            <span style={styles.clockText}>{IL_TIME_FMT.format(now)}</span>
          </div>
        </div>

        {/* CENTER PANEL */}
        <div style={styles.centerPanel}>
          <CountdownTimer
            passes={passes}
            satelliteName={selected?.name ?? '—'}
          />
          <div style={styles.mapWrapper}>
            <SatelliteMap
              satelliteName={selected?.name ?? ''}
              position={position}
              track={track}
            />
          </div>
        </div>

        {/* RIGHT PANEL */}
        <div style={styles.rightPanel}>
          <div style={styles.panelHeader}>UPCOMING PASSES — 7 DAYS</div>
          <PassTable passes={passes} satellites={satellites} />
        </div>
      </div>

      {/* STATUS BAR */}
      <div style={styles.statusBar}>
        <StatusItem label="TLE" value={dataUpdatedAt ? 'OK' : '—'} ok={!!dataUpdatedAt} />
        <div style={styles.statusDivider} />
        <StatusItem label="N2YO" value={position ? 'CONNECTED' : 'WAITING'} ok={!!position} />
        <div style={styles.statusDivider} />
        <StatusItem label="DB" value={passes ? 'OK' : '—'} ok={!!passes} />
        <div style={{ flex: 1 }} />
        <span style={styles.statusMeta}>
          LAST UPDATE&nbsp;&nbsp;
          {dataUpdatedAt ? IL_TIME_FMT.format(new Date(dataUpdatedAt)) : '—'}
          &nbsp;IL
        </span>
      </div>
    </div>
  )
}

function DataRow({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
      <span style={styles.dimLabel}>{label}</span>
      <span style={styles.dataValue}>{value}</span>
    </div>
  )
}

function StatusItem({ label, value, ok }: { label: string; value: string; ok: boolean }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
      <span style={styles.dimLabel}>{label}</span>
      <span style={{ ...styles.statusValue, color: ok ? '#22c55e' : '#4b5563' }}>{value}</span>
    </div>
  )
}

const styles = {
  root: {
    display: 'flex',
    flexDirection: 'column' as const,
    height: '100vh',
    background: '#080c08',
    fontFamily: 'monospace',
    color: '#4ade80',
    overflow: 'hidden',
  },
  body: {
    display: 'flex',
    flex: 1,
    overflow: 'hidden',
  },
  leftPanel: {
    width: '220px',
    flexShrink: 0,
    display: 'flex',
    flexDirection: 'column' as const,
    background: '#090d09',
    borderRight: '0.5px solid #1a3a1a',
    overflow: 'hidden',
  },
  centerPanel: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column' as const,
    overflow: 'hidden',
  },
  mapWrapper: {
    flex: 1,
    overflow: 'hidden',
    position: 'relative' as const,
  },
  rightPanel: {
    width: '220px',
    flexShrink: 0,
    display: 'flex',
    flexDirection: 'column' as const,
    background: '#090d09',
    borderLeft: '0.5px solid #1a3a1a',
    overflow: 'hidden',
  },
  panelHeader: {
    padding: '10px 12px 6px',
    fontSize: '9px',
    letterSpacing: '0.15em',
    color: '#166534',
    textTransform: 'uppercase' as const,
    borderBottom: '0.5px solid #1a3a1a',
  },
  sectionDivider: {
    height: '0.5px',
    background: '#1a3a1a',
    margin: '4px 0',
  },
  dataBlock: {
    padding: '8px 12px',
  },
  dimLabel: {
    fontSize: '9px',
    letterSpacing: '0.1em',
    color: '#166534',
    textTransform: 'uppercase' as const,
  },
  dataValue: {
    fontSize: '11px',
    color: '#4ade80',
  },
  dimText: {
    fontSize: '10px',
    color: '#166534',
    letterSpacing: '0.08em',
  },
  clockBlock: {
    display: 'flex',
    flexDirection: 'column' as const,
    alignItems: 'center',
    padding: '10px 12px',
    borderTop: '0.5px solid #1a3a1a',
    gap: '2px',
  },
  clockText: {
    fontSize: '14px',
    color: '#22c55e',
    letterSpacing: '0.08em',
  },
  statusBar: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    padding: '6px 16px',
    borderTop: '0.5px solid #1a3a1a',
    background: '#090d09',
    flexShrink: 0,
  },
  statusDivider: {
    width: '0.5px',
    height: '14px',
    background: '#1a3a1a',
  },
  statusValue: {
    fontSize: '10px',
    fontFamily: 'monospace',
    letterSpacing: '0.06em',
  },
  statusMeta: {
    fontSize: '10px',
    color: '#166534',
    letterSpacing: '0.06em',
  },
} as const
