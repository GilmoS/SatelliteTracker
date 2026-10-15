SatelliteTracker M3 Design — Code Truth Map (2a baseline only)

Legend: [REAL] = exact field/action already exists and is ready to wire · [PARTIAL] = backing exists but needs adaptation, or value is derivable but not stored · [DECORATIVE] = no code path or schema concept produces this; omit, or render as static non-interactive chrome only if omitting breaks layout.

Screen 1/8 — Home

Backing: DashboardViewModel + DashboardUiState (ui/dashboard/)

- [DECORATIVE] Status bar (clock/signal/wifi/battery) — OS chrome, not app state.
- [DECORATIVE] Top app bar title "SatelliteTracker" — static label.
- [DECORATIVE] Notification bell + amber badge dot — no unread/notification field anywhere.
- [REAL] Per-satellite tabs (EROS C3 / RUNNER-1) — DashboardUiState.Content.tabs, .selectedSatelliteId, selectTab(). Generic over however many satellites the backend returns.
- [REAL] Countdown numeral "00:47:23" — SatelliteTabState.nextPassCountdown, ticks every second.
- [DECORATIVE] Circular ring fill ("62% elapsed") — only a raw remaining Duration exists, no elapsed-ratio field. Render static/indeterminate, not a literal percentage.
- [REAL] "EROS C3 · orbit 20002" chip — Pass.satelliteId, .orbitNumber of the same next pass.
- [REAL] AOS/LOS/MAX EL/DUR metric cards — Pass.aos/.los/.maxElevation/.durationSec.
- [REAL] "Upcoming passes" list rows — SatelliteTabState.passes.
- [PARTIAL] Per-row relative time ("in 47 min", "in 1h 34m") — only the tab's one next pass has a ticking countdown; other rows need ad-hoc computation from Pass.aos at render time.
- [DECORATIVE] "Alert 15 min before" assist chip — alertMinutes lives in SettingsUiState/UserSettings; DashboardViewModel never loads it.
- [PARTIAL] FAB (compass/satellite icon) — fine as pure navigation to Map/Sky View (routes exist), but those destinations are empty placeholders today. Update 2026-09-24: the FAB now opens Map Flow 1 (live track of the selected satellite), which has real content. Sky View is still a placeholder.
- [REAL] Bottom nav bar, pill indicator — all four destinations exist as Composable stubs in the nav graph.

Screen 2/8 — Pass List (Upcoming)

Backing: FullPassListViewModel + FullPassListUiState (ui/fullpasslist/) — scoped to one satellite via SavedStateHandle. No Composable exists yet.

- [REAL] Back arrow — nav-only.
- [DECORATIVE] "Passes" title — static label.
- [DECORATIVE] Search icon — no free-text search exists on this screen (documented explicitly).
- [DECORATIVE] Overflow (⋮) menu — no menu actions defined anywhere.
- [DECORATIVE] Satellite tabs row (All / EROS C3 / RUNNER-1 / VENµS) — screen is scoped to exactly one satelliteId; no multi-satellite "All" aggregation exists.
- [REAL] Upcoming/History segmented control — PassListFilter.UPCOMING/.HISTORY, setFilter(). (A third value .ALL exists in code but isn't a toggle state here.)
- [REAL] Filter button — opens sheet; state is FullPassListUiState.timeWindow/.minMaxElevation.
- [PARTIAL] Filter badge count ("2") — no activeFilterCount field; derivable by diffing current filter vs. defaults at render time.
- [PARTIAL] "14 passes" total count — no distinct total field; passes.size of what's currently loaded.
- [REAL] Active-filter chips ("El ≥ 25°", "Next 48h") — minMaxElevation, TimeWindow.Last48h.
- [PARTIAL] Date-group headers ("TODAY · 29 AUG") + per-day counts — passes is a flat list; grouping over Pass.aos is derivable but not implemented.
- [REAL] Row: time range, orbit #, elevation chip — Pass.aos/.los/.orbitNumber/.maxElevation.
- [DECORATIVE] Row: "N→S"/"S→N" direction label — no pass-direction field on Pass; explicitly documented as NOT implemented.
- [REAL] Row tap → Pass Details — navigation to built PassDetailsViewModel.

Screen 3/8 — Pass List (History)

Same screen/ViewModel as Upcoming, filter = HISTORY. Only unique elements listed.

- [DECORATIVE] Summary strip "PASSES·30D 312", "TRACKED 96%", "MISSED 11" — no tracked/missed status concept exists anywhere in the schema.
- [PARTIAL] Group headers ("EARLIER TODAY", "YESTERDAY · 28 AUG") — same derivable-grouping note as Upcoming.
- [REAL] Normal history row (check icon, time, "Xh ago", duration) — getPassHistory() → Pass.aos/.los/.durationSec.
- [DECORATIVE] "X frames" tracked-count text — no imaging/frame-count field anywhere in the system.
- [DECORATIVE] "Missed" row variant (amber icon, "station in maintenance") — no missed/outage status exists on Pass or backend schema.

Screen 4/8 — Filter Modal (bottom sheet)

Same FullPassListViewModel — fields write into PassHistoryFilter.

- [DECORATIVE] Drag handle, "Filter passes" title, close (×) — chrome/dismiss only.
- [PARTIAL] "Reset" — achievable via setTimeWindow()+setMinMaxElevation() to defaults; no dedicated reset method yet.
- [DECORATIVE] Satellite chips (multi-select) — no multi-satellite selection in PassHistoryFilter.
- [REAL] Time window chips (24h/48h/7 days/Custom) — TimeWindow.Last24h/.Last48h/.Last7Days/.Custom.
- [REAL] "Minimum max elevation" slider (0–90°) — PassHistoryFilter.minMaxElevation.
- [DECORATIVE] "Minimum duration" chips (Any/2m+/5m+/8m+) — explicitly NOT implemented, no backend param.
- [DECORATIVE] "Pass direction" chips (Any/N→S/S→N) — explicitly NOT implemented, no backend param.
- [DECORATIVE] "Sunlit satellite only" toggle — no illumination/sun-angle modeling anywhere.
- [DECORATIVE] "Hide passes below horizon mask" toggle — no per-site horizon-obstruction concept anywhere.
- [REAL] "Cancel" — pure UI dismissal, no repository call.
- [PARTIAL] "Show N passes" commit button with live preview count — current setters apply+reload immediately; no staged/draft filter state exists for a pre-commit preview.

Screen 5/8 — Map (Ground Track)

Backing: MapViewModel + MapUiState (ui/map/) over MapRepository; rendered by MapScreen.kt (MapLibre Compose 0.12.1). Two flows, chosen from optional nav args: Flow 1 = LiveTrack (no passId — Dashboard FAB, bottom-nav Map), Flow 2 = StaticPassTrack (passId — Pass Details "Show on map", drawer tap). Each item below re-verified on an emulator during the post-merge integration QA (2026-09-24), not bulk-relabeled.

- [REAL] Basemap — inline MapLibre raster style over CartoDB Dark Matter tiles (the same tiles the web frontend uses). ⚠ As of 2026-09-24 CARTO returns every keyless tile with an "API KEY REQUIRED" watermark, whatever the request headers (the web frontend is affected too). Needs a tile-provider/key decision — open follow-up.
- [REAL] Dashed ground-track polyline — split from the old combined entry. Flow 1: LiveTrack.trackPoints (getLiveTrack, re-polled every 5 min), dashed. Flow 2: StaticPassTrack.trackPoints (getPassTrack), solid, camera framed to its bbox. Verified on device. The dashed style doesn't draw under the emulator's SwiftShader GPU but does on the host GPU — an emulator artifact, not an app bug.
- [REAL] Live position dot — split from the old "dot + pulse" entry. LiveTrack.currentPosition, polled every 15 s. Verified: 4 position requests in ~50 s on the Map, 0 after leaving via bottom nav or back. Drawn as a static halo + dot.
- [DECORATIVE] Pulse animation on the position dot — still not built.
- [REAL] "EROS C3" floating label — LiveTrack.satelliteName (satellite-catalog lookup), a Compose overlay anchored through the map projection (the inline style has no glyphs for map-rendered text).
- [REAL] Back arrow + top bar — nav-only. Title is the satellite name + "Live ground track" (Flow 1) or "Pass track" + orbit/AOS subtitle (Flow 2).
- [DECORATIVE] Layer/zoom/compass FABs — still omitted; no map-control state (pinch-zoom/pan work natively).
- [PARTIAL] Bottom sheet lat/lon/altitude/velocity readout — not built. lat/lon/altitude now exist in LiveTrack.currentPosition; velocity doesn't exist on SatellitePosition.
- [DECORATIVE] "Over Israel" status badge — still no geofence/region check client-side.
- [REAL] Bottom nav bar — the Map item opens Flow 1 for the Dashboard's selected satellite (disabled until one is known) and never restores an earlier Map entry.
- Built, not in the mockup: [REAL] ~2000 km footprint polygon (Flow 1 only; GeoUtils + MapGeometry antimeridian/polar handling). [REAL] AOS (filled) / LOS (hollow) end markers (Flow 2). [PARTIAL] "Passes with notifications on" drawer (both flows) over PassDao.getNotifyEnabled(). Verified on device, it currently lists nearly every cached pass (first-seen passes default to notify = true locally while the backend is opt-in), includes past passes, and ignores hidden satellites — open follow-up. Drawer taps replace the Map entry: three hops, then one back press, landed on Dashboard.
- Hidden satellites: Map doesn't observe HiddenSatellitesStore. Hiding the satellite in Settings and reopening Map from the bottom nav still showed the hidden satellite's live track, until Dashboard recomposed and re-reported a visible selection.

Screen 6/8 — Sky View (AR)

Milestone F. No ARCore/camera code exists anywhere in the app. SkyViewScreen.kt is an empty placeholder.

Re-checked 2026-09-24, after the Map (Step 6) merges: SkyViewScreen.kt is still the 17-line text placeholder, there's still no ARCore/camera/sensor dependency or code, and the Map work added nothing Sky View could reuse for sky projection (MapViewModel's live data is ground-track lat/lon; it doesn't touch azimuth/elevation). Each item below was checked individually and stays [DECORATIVE].

- [DECORATIVE] Starfield + horizon glow background.
- [DECORATIVE] AR pass-arc path + AOS/LOS azimuth pins — Pass.aosAzimuth/.losAzimuth exist but nothing projects a live sky arc from them.
- [DECORATIVE] Reticle with live "EL 48° · AZ 198°" readout — no live elevation/azimuth tracking or sensor fusion exists.
- [DECORATIVE] "AR tracking" status pill.
- [DECORATIVE] Compass ring — no device-orientation/sensor code exists.
- [DECORATIVE] Bottom HUD card + "Track pass"/center FABs.

Screen 7/8 — Pass Details

Backing: PassDetailsViewModel + PassDetailsUiState (ui/passdetails/). Nav-graph "screen" but functions as a modal. PassDetailsScreen.kt is currently a plain text placeholder.

- [REAL] Back arrow — nav-only.
- [DECORATIVE] Header ground-track sparkline — PassRepository.getPassTrack() exists and is fully built, but PassDetailsViewModel never calls it.
- [REAL] "EROS C3" satellite name — Pass.satelliteId (resolving id→name needs a satellite lookup, see next).
- [PARTIAL] "Pass #20002 · NORAD 56998" — #20002 = Pass.orbitNumber (real). NORAD id lives on Satellite.noradId, which this ViewModel never fetches.
- [REAL] "72° MAX ELEV" badge — Pass.maxElevation.
- [REAL] Date + AOS/LOS (local & UTC) — Pass.aos/.los, both formats derive from the same OffsetDateTime.
- [REAL] Metric grid: Duration/AOS az/LOS az/Orbit/Max elev — Pass.durationSec/.aosAzimuth/.losAzimuth/.orbitNumber/.maxElevation.
- [DECORATIVE] Metric grid: "TLE epoch" — Pass only carries an opaque tleId; no TLE repository/endpoint is wrapped client-side.
- [DECORATIVE] "Add to Outlook" button — scheduleCalendar() exists on the API interface, no CalendarRepository or ViewModel action wraps it.
- [PARTIAL] "Set alert" row ("15 minutes before AOS", chevron) — real backing is a boolean (toggleNotify()/Pass.notify), not a per-pass minute picker. alertMinutes is a separate, tester-global setting (Settings screen).
- [PARTIAL] "Pass note" single inline text field — real backing exists (saveNote()/NotesRepository), but the confirmed design is dialog-based multi-note editing (EditingNoteState), not one inline field. No notes list/delete affordance shown.
- [DECORATIVE] "Visible to your team" label + "0/240" char counter — no team-visibility flag or char limit exists on Note.

Screen 8/8 — Settings

Backing: SettingsViewModel + SettingsUiState (ui/settings/)

- [DECORATIVE] "Settings" large title — static label.
- [REAL] Satellite rows (avatar, name, NORAD id, toggle) — SettingsUiState.satellites (SatelliteVisibility), toggleSatelliteVisibility(). NORAD id is genuinely available here (unlike Pass Details) since this screen already loads full Satellite objects.
- [PARTIAL] "Add satellite" row — wired to a real method (addSatellite()) that is an explicit stub: sets stubMessage only, calls no repository.
- [REAL] Alert timing chips (5m/10m/15m/30m/60m) — updateAlertMinutes(). Matches backend's exact valid set {5,10,15,30,60} one-to-one.
- [DECORATIVE] "Minimum elevation" pass-filter slider — maps to the backend's global Settings.MinElevation; SettingsRepository only wraps per-tester /api/settings/me*, never /api/settings.
- [DECORATIVE] "Outlook integration" card (Connected account, Manage, schedule range, Team email CC) — reflects the pre-ICS Graph-based design explicitly replaced by the ICS MVP; none of this concept exists in the current flow.
- [DECORATIVE] "About" version/attribution row — static text, harmless to keep.
- [REAL] Bottom nav bar — nav graph.

Cross-cutting findings

1. No Composable is wired to real logic anywhere yet. DashboardScreen, MapScreen, SkyViewScreen, SettingsScreen, PassDetailsScreen are all one-line text placeholders. No placeholder even exists yet for Full Pass List / Filter screens. Every REAL verdict above describes backing logic ready to consume, not existing UI.
2. Three backend-connected endpoints are defined but never wrapped: getSatellitePosition(), getSatelliteTrack() (Map), scheduleCalendar() (Pass Details "Add to Outlook"). Conversely PassRepository.getPassTrack() is fully wrapped but has zero consumers. Update 2026-09-24: getSatellitePosition()/getSatelliteTrack() are now wrapped by MapRepository, and getPassTrack() is consumed by Map Flow 2. Only scheduleCalendar() is still unwrapped.
3. Pass List screens are single-satellite; the mockup shows a multi-satellite "All" view. FullPassListViewModel takes exactly one satelliteId via nav arg — the satellite-tabs row and Filter sheet's satellite multi-select chips both assume a mode that doesn't exist.
4. Duration, pass-direction, sunlit-only, and horizon-mask filters have no backend param at all — not "unwired," structurally absent from the API.
5. "Missed" status and imaging "frame counts" don't exist in the schema — no outage flag on passes, no image-capture-count concept anywhere.
6. "Set alert" conflates a per-pass toggle with a per-tester setting. Build it as a switch (Pass.notify), not a picker entry point; alertMinutes only lives in Settings.
7. Pass Details' note field should become the dialog it's already built for — EditingNoteState/multi-note CRUD is fully implemented; the single inline field with a 240-char counter and "visible to your team" label doesn't map to anything on Note.
8. Settings' "Outlook integration" card describes a reversed product direction — the app moved from a Graph "connected account" model to a per-pass ICS-export flow; none of the connected-account/schedule-range/team-email UI should be built as shown.