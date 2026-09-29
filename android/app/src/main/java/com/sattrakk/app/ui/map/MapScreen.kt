package com.sattrakk.app.ui.map

import android.net.Uri
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DrawerValue
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalDrawerSheet
import androidx.compose.material3.ModalNavigationDrawer
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.rememberDrawerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.layout
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.BuildConfig
import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.util.GeoUtils
import com.sattrakk.app.navigation.BackArrowIcon
import com.sattrakk.app.navigation.PassesIcon
import com.sattrakk.app.ui.common.formatDateLocal
import com.sattrakk.app.ui.common.formatTimeLocal
import com.sattrakk.app.ui.theme.TelemetryTextStyle
import kotlinx.coroutines.launch
import kotlin.math.atan2
import kotlin.math.cos
import java.util.concurrent.TimeUnit
import org.maplibre.compose.camera.CameraPosition
import org.maplibre.compose.camera.CameraState
import org.maplibre.compose.camera.rememberCameraState
import org.maplibre.compose.expressions.dsl.const
import org.maplibre.compose.expressions.value.LineCap
import org.maplibre.compose.expressions.value.LineJoin
import org.maplibre.compose.layers.CircleLayer
import org.maplibre.compose.layers.FillLayer
import org.maplibre.compose.layers.LineLayer
import org.maplibre.compose.map.MapOptions
import org.maplibre.compose.map.MaplibreMap
import org.maplibre.compose.map.OrnamentOptions
import org.maplibre.compose.sources.GeoJsonData
import org.maplibre.compose.sources.rememberGeoJsonSource
import org.maplibre.compose.style.BaseStyle
import org.maplibre.spatialk.geojson.BoundingBox
import org.maplibre.spatialk.geojson.MultiLineString
import org.maplibre.spatialk.geojson.Point
import org.maplibre.spatialk.geojson.Polygon
import org.maplibre.spatialk.geojson.Position

// Map screen, both flows (see MapViewModel/MapUiState — consumed exactly as built, no new logic):
//  - LiveTrack (Flow 1): live ground track + current-position marker (moves as MapViewModel polls)
//    + visibility footprint + satellite-name label + direction-of-flight arrow.
//  - StaticPassTrack (Flow 2): one pass's fixed ground track with AOS/LOS end markers. No live
//    marker, no footprint (confirmed decision).
//  - SatelliteHidden (Flow 1): the tracked satellite was hidden in Settings. A message and an
//    "Open Settings" button, no map.
// The notify-enabled pass drawer is available in all three. See android/CLAUDE.md's Map screen
// Composable/UI section.
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MapScreen(
    onBackClick: () -> Unit = {},
    onPassSelected: (passId: String) -> Unit = {},
    onOpenSettings: () -> Unit = {},
    viewModel: MapViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val drawerState = rememberDrawerState(DrawerValue.Closed)
    val scope = rememberCoroutineScope()

    ModalNavigationDrawer(
        drawerState = drawerState,
        // Swipe-to-open would fight the map's own pan gesture, so the drawer only opens from the
        // top bar button; once open, swiping/scrim-tapping it closed works normally.
        gesturesEnabled = drawerState.isOpen,
        drawerContent = {
            NotifyPassesDrawer(
                state = state,
                onPassClick = { passId ->
                    scope.launch { drawerState.close() }
                    onPassSelected(passId)
                },
            )
        },
    ) {
        Scaffold(
            topBar = {
                TopAppBar(
                    title = { MapTitle(state) },
                    navigationIcon = {
                        IconButton(onClick = onBackClick) {
                            BackArrowIcon(MaterialTheme.colorScheme.onSurface)
                        }
                    },
                    actions = {
                        IconButton(onClick = { scope.launch { drawerState.open() } }) {
                            PassesIcon(MaterialTheme.colorScheme.onSurface)
                        }
                    },
                )
            },
        ) { innerPadding ->
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(innerPadding),
            ) {
                when (val s = state) {
                    MapUiState.Loading -> CircularProgressIndicator(Modifier.align(Alignment.Center))
                    is MapUiState.Error -> Text(
                        text = s.message,
                        color = MaterialTheme.colorScheme.error,
                        textAlign = TextAlign.Center,
                        modifier = Modifier
                            .align(Alignment.Center)
                            .padding(24.dp),
                    )
                    is MapUiState.LiveTrack -> LiveTrackMap(s)
                    is MapUiState.StaticPassTrack -> StaticPassTrackMap(s)
                    is MapUiState.SatelliteHidden -> SatelliteHiddenMessage(
                        satelliteName = s.satelliteName,
                        onOpenSettings = onOpenSettings,
                        modifier = Modifier.align(Alignment.Center),
                    )
                }
            }
        }
    }
}

@Composable
private fun MapTitle(state: MapUiState) {
    val (title, subtitle) = when (state) {
        is MapUiState.LiveTrack -> state.satelliteName to "Live ground track"
        is MapUiState.StaticPassTrack ->
            "Pass track" to "Orbit ${state.pass.orbitNumber} · ${formatDateLocal(state.pass.aos)} ${formatTimeLocal(state.pass.aos)}"
        is MapUiState.SatelliteHidden -> state.satelliteName to "Hidden"
        MapUiState.Loading, is MapUiState.Error -> "Ground track" to null
    }
    Column {
        Text(title)
        if (subtitle != null) {
            Text(
                subtitle,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

// ---- Flow 1 ----

// Flow 1's tracked satellite is hidden in Settings. MapViewModel has stopped polling. Nothing
// navigates away on its own: the user can unhide it (tracking resumes here), open a drawer pass,
// or go back.
@Composable
private fun SatelliteHiddenMessage(satelliteName: String, onOpenSettings: () -> Unit, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier.padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(
            text = "$satelliteName is hidden. Unhide it in Settings to keep tracking.",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        TextButton(onClick = onOpenSettings) { Text("Open Settings") }
    }
}

@Composable
private fun LiveTrackMap(state: MapUiState.LiveTrack) {
    val position = state.currentPosition
    // Centered once, on the position at the moment the map first appears. Later polls move the
    // marker but deliberately don't drag the camera along — the user may have panned away.
    val cameraState = rememberCameraState(
        firstPosition = CameraPosition(
            target = Position(longitude = position.longitude, latitude = position.latitude),
            zoom = LIVE_ZOOM,
        ),
    )
    // Sorted by time defensively; the backend already returns the orbit track in order.
    val track = remember(state.trackPoints) { state.trackPoints.sortedBy { it.timestampEpochMillis } }
    // The orbit track is split at the satellite's current time into a short tail already flown
    // (dimmed) and the full orbit ahead (dashed), both joined at the marker. Recomputed when
    // either poll lands (position every 15 s, track every 5 min).
    val (pastGeometry, futureGeometry) = remember(track, position) {
        val (past, future) = MapGeometry.splitAtTime(
            track, position.timestampEpochMillis, LatLng(position.latitude, position.longitude),
            PAST_TAIL_MILLIS,
        )
        trackGeometry(past) to trackGeometry(future)
    }
    val footprintGeometry = remember(state.footprintPolygon, position.latitude, position.longitude) {
        val ring = MapGeometry.footprintRing(state.footprintPolygon, LatLng(position.latitude, position.longitude))
        if (ring.size >= 4) Polygon(listOf(ring.map { it.toPosition() })) else null
    }
    val marker = Point(longitude = position.longitude, latitude = position.latitude)
    val heading = remember(track, position.timestampEpochMillis) {
        MapGeometry.headingDegrees(position.timestampEpochMillis, track)
    }
    val colors = MapColors.current()

    Box(Modifier.fillMaxSize()) {
        MaplibreMap(
            modifier = Modifier.fillMaxSize(),
            baseStyle = CARTO_DARK_STYLE,
            cameraState = cameraState,
            options = MAP_OPTIONS,
        ) {
            if (footprintGeometry != null) {
                val footprintSource = rememberGeoJsonSource(GeoJsonData.Features(footprintGeometry))
                FillLayer(
                    id = "footprint-fill",
                    source = footprintSource,
                    color = const(colors.accent),
                    opacity = const(0.12f),
                )
                LineLayer(
                    id = "footprint-outline",
                    source = footprintSource,
                    color = const(colors.accent),
                    opacity = const(0.5f),
                    width = const(1.dp),
                )
            }
            val pastSource = rememberGeoJsonSource(GeoJsonData.Features(pastGeometry))
            LineLayer(
                id = "orbit-past",
                source = pastSource,
                color = const(colors.accent),
                opacity = const(0.35f),
                width = const(1.5.dp),
                cap = const(LineCap.Round),
                join = const(LineJoin.Round),
            )
            val futureSource = rememberGeoJsonSource(GeoJsonData.Features(futureGeometry))
            LineLayer(
                id = "orbit-future",
                source = futureSource,
                color = const(colors.accent),
                width = const(2.dp),
                // Dashed, per the design's "dashed ground-track polyline" (units: line widths).
                dasharray = const(listOf(2, 1.5)),
                cap = const(LineCap.Round),
                join = const(LineJoin.Round),
            )
            val markerSource = rememberGeoJsonSource(GeoJsonData.Features(marker))
            CircleLayer(
                id = "position-halo",
                source = markerSource,
                color = const(colors.accent),
                opacity = const(0.25f),
                radius = const(14.dp),
            )
            CircleLayer(
                id = "position-dot",
                source = markerSource,
                color = const(colors.accent),
                radius = const(6.dp),
                strokeColor = const(colors.onAccent),
                strokeWidth = const(2.dp),
            )
        }
        MarkerOverlay(
            name = state.satelliteName,
            position = LatLng(position.latitude, position.longitude),
            headingDegrees = heading,
            colors = colors,
            cameraState = cameraState,
        )
    }
}

// The satellite-name label and the direction-of-flight arrow around the position marker. Drawn as
// Compose overlays positioned through the map's own projection rather than MapLibre SymbolLayers:
// map-rendered text or icons need a glyphs/sprite source in the style, and the inline raster style
// deliberately references nothing but the CartoDB tile source. Isolated in its own composable so
// the camera reads below (which change every frame while the user pans) only recompose this.
@Composable
private fun MarkerOverlay(
    name: String,
    position: LatLng,
    headingDegrees: Double?,
    colors: MapColors,
    cameraState: CameraState,
) {
    // Read position to re-run on every camera move; projection is null until the map is attached.
    @Suppress("UNUSED_VARIABLE")
    val cameraPosition = cameraState.position
    val projection = cameraState.projection ?: return
    val anchor = projection.screenLocationFromPosition(position.toPosition())

    // The arrow's on-screen angle (clockwise from screen-up) comes from projecting a point a short
    // way ahead along the heading, not from the true-north bearing directly, so it stays right if
    // the map is ever rotated or tilted. Its longitude is unwrapped next to the marker's so a
    // marker by the antimeridian doesn't project the ahead point onto the other world copy.
    val screenAngleDegrees = headingDegrees?.let { heading ->
        val ahead = GeoUtils.destinationPoint(position, heading, HEADING_PROBE_KM)
        val aheadLon = ahead.longitude + 360.0 * Math.round((position.longitude - ahead.longitude) / 360.0)
        val aheadScreen = projection.screenLocationFromPosition(
            Position(longitude = aheadLon, latitude = ahead.latitude),
        )
        val dx = (aheadScreen.x - anchor.x).value
        val dy = (aheadScreen.y - anchor.y).value
        if (dx == 0f && dy == 0f) null else Math.toDegrees(atan2(dx, -dy).toDouble()).toFloat()
    }

    if (screenAngleDegrees != null) {
        Canvas(
            modifier = Modifier
                .layout { measurable, constraints ->
                    val size = HEADING_ARROW_BOX.roundToPx()
                    val placeable = measurable.measure(Constraints.fixed(size, size))
                    layout(size, size) {
                        placeable.place(anchor.x.roundToPx() - size / 2, anchor.y.roundToPx() - size / 2)
                    }
                }
                .rotate(screenAngleDegrees),
        ) {
            // Drawn pointing up (screen north) and rotated as a whole: an arrowhead just outside
            // the position halo, on the side the satellite is heading.
            val c = center
            val tip = HEADING_ARROW_TIP.toPx()
            val base = HEADING_ARROW_BASE.toPx()
            val halfWidth = HEADING_ARROW_HALF_WIDTH.toPx()
            val arrow = Path().apply {
                moveTo(c.x, c.y - tip)
                lineTo(c.x + halfWidth, c.y - base)
                lineTo(c.x, c.y - base - (tip - base) * 0.3f)
                lineTo(c.x - halfWidth, c.y - base)
                close()
            }
            drawPath(arrow, colors.accent)
            drawPath(arrow, colors.onAccent, style = Stroke(width = 1.5.dp.toPx(), join = StrokeJoin.Round))
        }
    }

    // The label sits above the marker, unless the arrow points upward (the usual case for these
    // near-polar orbits), where it would cover the arrow. Then it goes below, on the trailing side.
    val labelBelow = screenAngleDegrees != null && cos(Math.toRadians(screenAngleDegrees.toDouble())) > 0
    Text(
        text = name,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.onPrimaryContainer,
        modifier = Modifier
            // Centered horizontally on the marker, just clear of its halo.
            .layout { measurable, constraints ->
                val placeable = measurable.measure(constraints.copy(minWidth = 0, minHeight = 0))
                layout(placeable.width, placeable.height) {
                    val y = if (labelBelow) {
                        anchor.y.roundToPx() + LABEL_GAP.roundToPx()
                    } else {
                        anchor.y.roundToPx() - placeable.height - LABEL_GAP.roundToPx()
                    }
                    placeable.place(x = anchor.x.roundToPx() - placeable.width / 2, y = y)
                }
            }
            .background(MaterialTheme.colorScheme.primaryContainer, RoundedCornerShape(8.dp))
            .padding(horizontal = 8.dp, vertical = 2.dp),
    )
}

// ---- Flow 2 ----

@Composable
private fun StaticPassTrackMap(state: MapUiState.StaticPassTrack) {
    val points = remember(state.trackPoints) { state.trackPoints.map { LatLng(it.latitude, it.longitude) } }
    val segments = remember(points) { MapGeometry.splitAtAntimeridian(points) }
    val start = points.firstOrNull()
    val cameraState = rememberCameraState(
        firstPosition = CameraPosition(
            target = start?.toPosition() ?: Position(0.0, 0.0),
            zoom = STATIC_FALLBACK_ZOOM,
        ),
    )
    // Frame the whole pass. Skipped for a track split at the antimeridian (a bbox across ±180°
    // would span the whole world) — it just stays centered on AOS at the fallback zoom instead.
    // Not a realistic case for passes over Israel, but must not produce a nonsense camera.
    LaunchedEffect(segments) {
        if (segments.size == 1) {
            val box = boundingBoxOf(segments.single())
            cameraState.animateTo(boundingBox = box, padding = PaddingValues(48.dp))
        }
    }
    val colors = MapColors.current()

    MaplibreMap(
        modifier = Modifier.fillMaxSize(),
        baseStyle = CARTO_DARK_STYLE,
        cameraState = cameraState,
        options = MAP_OPTIONS,
    ) {
        val trackSource = rememberGeoJsonSource(GeoJsonData.Features(trackGeometry(points)))
        LineLayer(
            id = "pass-track",
            source = trackSource,
            color = const(colors.accent),
            width = const(3.dp),
            cap = const(LineCap.Round),
            join = const(LineJoin.Round),
        )
        // AOS/LOS end markers (optional per the task, kept: without them the track has no
        // direction). AOS is filled, LOS hollow.
        if (points.size >= 2) {
            val aosSource = rememberGeoJsonSource(GeoJsonData.Features(Point(points.first().toPosition())))
            CircleLayer(
                id = "pass-aos",
                source = aosSource,
                color = const(colors.accent),
                radius = const(6.dp),
                strokeColor = const(colors.onAccent),
                strokeWidth = const(2.dp),
            )
            val losSource = rememberGeoJsonSource(GeoJsonData.Features(Point(points.last().toPosition())))
            CircleLayer(
                id = "pass-los",
                source = losSource,
                color = const(colors.background),
                radius = const(6.dp),
                strokeColor = const(colors.accent),
                strokeWidth = const(2.dp),
            )
        }
    }
}

// ---- Drawer (both flows) ----

@Composable
private fun NotifyPassesDrawer(state: MapUiState, onPassClick: (String) -> Unit) {
    val (passes, satelliteNames) = when (state) {
        is MapUiState.LiveTrack -> state.notifyEnabledPasses to state.satelliteNames
        is MapUiState.StaticPassTrack -> state.notifyEnabledPasses to state.satelliteNames
        is MapUiState.SatelliteHidden -> state.notifyEnabledPasses to state.satelliteNames
        MapUiState.Loading, is MapUiState.Error -> emptyList<Pass>() to emptyMap()
    }
    val currentPassId = (state as? MapUiState.StaticPassTrack)?.pass?.id

    ModalDrawerSheet {
        Text(
            "Passes with notifications on",
            style = MaterialTheme.typography.titleSmall,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(horizontal = 24.dp, vertical = 16.dp),
        )
        HorizontalDivider()
        if (passes.isEmpty()) {
            Text(
                if (state is MapUiState.Loading) "Loading…" else "No passes have notifications turned on.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(24.dp),
            )
        } else {
            LazyColumn {
                items(passes, key = { it.id }) { pass ->
                    DrawerPassRow(
                        pass = pass,
                        // Resolved by MapViewModel's catalog lookup. The fallback only shows if that
                        // lookup failed (Flow 2 tolerates it) or the id isn't in the catalog.
                        satelliteName = satelliteNames[pass.satelliteId] ?: "Unknown satellite",
                        isCurrent = pass.id == currentPassId,
                        onClick = { onPassClick(pass.id) },
                    )
                }
            }
        }
    }
}

@Composable
private fun DrawerPassRow(pass: Pass, satelliteName: String, isCurrent: Boolean, onClick: () -> Unit) {
    val background = if (isCurrent) MaterialTheme.colorScheme.secondaryContainer else Color.Transparent
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(background)
            .clickable(onClick = onClick)
            .padding(horizontal = 24.dp, vertical = 12.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(satelliteName, style = MaterialTheme.typography.titleSmall)
            Text(
                "${formatDateLocal(pass.aos)} · AOS ${formatTimeLocal(pass.aos)}",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Text(
            "Orbit ${pass.orbitNumber}",
            style = TelemetryTextStyle.copy(fontSize = MaterialTheme.typography.labelMedium.fontSize),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

// ---- Shared helpers ----

// Theme colors read in normal composition and handed into the MaplibreMap content lambda as plain
// values — layer properties are map-style constants, not Compose-themed UI.
private data class MapColors(val accent: Color, val onAccent: Color, val background: Color) {
    companion object {
        @Composable
        fun current() = MapColors(
            accent = MaterialTheme.colorScheme.primary,
            onAccent = MaterialTheme.colorScheme.onPrimary,
            background = MaterialTheme.colorScheme.background,
        )
    }
}

private fun trackGeometry(points: List<LatLng>): MultiLineString =
    MultiLineString(MapGeometry.splitAtAntimeridian(points).map { segment -> segment.map { it.toPosition() } })

private fun boundingBoxOf(points: List<LatLng>): BoundingBox = BoundingBox(
    west = points.minOf { it.longitude },
    south = points.minOf { it.latitude },
    east = points.maxOf { it.longitude },
    north = points.maxOf { it.latitude },
)

private fun LatLng.toPosition() = Position(longitude = longitude, latitude = latitude)

// Opens wide enough to show most of the full orbit (the world is ~512 dp wide at zoom 1, about a
// phone screen), while the ~4000 km footprint still reads as a circle around the marker.
private const val LIVE_ZOOM = 1.0
private const val STATIC_FALLBACK_ZOOM = 3.0
private val LABEL_GAP = 16.dp

// Direction-of-flight arrowhead, in dp from the marker's center: it starts just outside the 14 dp
// halo. HEADING_PROBE_KM is how far ahead along the heading the screen angle is sampled; short
// enough that the great circle is effectively straight on screen.
private val HEADING_ARROW_BOX = 80.dp
private val HEADING_ARROW_BASE = 18.dp
private val HEADING_ARROW_TIP = 36.dp
private val HEADING_ARROW_HALF_WIDTH = 10.dp
private const val HEADING_PROBE_KM = 50.0

// How much of the flown orbit the live map draws behind the marker: about 6,800 km at LEO speed,
// enough to show where the satellite came from without a second full line across the map.
private val PAST_TAIL_MILLIS = TimeUnit.MINUTES.toMillis(15)

private val MAP_OPTIONS = MapOptions(
    // Attribution stays on (required by the CARTO/OSM tile terms); the scale bar would crowd the
    // top bar region and a compass is noise on a north-up overview map.
    ornamentOptions = OrnamentOptions(isScaleBarEnabled = false, isCompassEnabled = false),
)

// Inline style (confirmed decision: no hosted MapLibre/Mapbox style). One raster source — the
// same CartoDB Dark Matter tiles the web frontend already uses
// (frontend/src/components/SatelliteMap.tsx) — and one raster layer. Leaflet's `{s}`/`{r}`
// placeholders don't exist in MapLibre, so the subdomains are listed explicitly and the retina
// variant (@2x, 512 px) is requested at tileSize 256 for crisp tiles on high-density screens.
// Since 2026-09-23 CARTO requires an API key on every tile request (query parameter `key`), so
// the URLs carry BuildConfig.MAP_API_KEY, read from local.properties at build time.
private val CARTO_DARK_STYLE = BaseStyle.Json(
    """
    {
      "version": 8,
      "name": "SatTrakk CartoDB Dark Matter",
      "sources": {
        "carto-dark": {
          "type": "raster",
          "tiles": [
            "${cartoTileUrl("a")}",
            "${cartoTileUrl("b")}",
            "${cartoTileUrl("c")}",
            "${cartoTileUrl("d")}"
          ],
          "tileSize": 256,
          "maxzoom": 20,
          "attribution": "© OpenStreetMap contributors © CARTO"
        }
      },
      "layers": [
        { "id": "carto-dark", "type": "raster", "source": "carto-dark" }
      ]
    }
    """.trimIndent(),
)

// CARTO's template is .../rastertiles/dark_all/{z}/{x}/{y}{r}.png?key=KEY; `{r}` is filled with
// the retina suffix "@2x". Uri.encode keeps the key safe inside both the URL and the JSON string.
private fun cartoTileUrl(subdomain: String): String =
    "https://$subdomain.basemaps.cartocdn.com/rastertiles/dark_all/{z}/{x}/{y}@2x.png" +
        "?key=${Uri.encode(BuildConfig.MAP_API_KEY)}"
