package com.sattrakk.app.ui.map

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
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.rememberDrawerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.layout
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.domain.model.LatLng
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.navigation.BackArrowIcon
import com.sattrakk.app.navigation.PassesIcon
import com.sattrakk.app.ui.common.formatDateLocal
import com.sattrakk.app.ui.common.formatTimeLocal
import com.sattrakk.app.ui.theme.TelemetryTextStyle
import kotlinx.coroutines.launch
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
//    + visibility footprint + satellite-name label.
//  - StaticPassTrack (Flow 2): one pass's fixed ground track with AOS/LOS end markers. No live
//    marker, no footprint (confirmed decision).
// The notify-enabled pass drawer is available in both flows. See android/CLAUDE.md's Map screen
// Composable/UI section.
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MapScreen(
    onBackClick: () -> Unit = {},
    onPassSelected: (passId: String) -> Unit = {},
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
    val trackGeometry = remember(state.trackPoints) {
        trackGeometry(state.trackPoints.map { LatLng(it.latitude, it.longitude) })
    }
    val footprintGeometry = remember(state.footprintPolygon, position.latitude, position.longitude) {
        val ring = MapGeometry.footprintRing(state.footprintPolygon, LatLng(position.latitude, position.longitude))
        if (ring.size >= 4) Polygon(listOf(ring.map { it.toPosition() })) else null
    }
    val marker = Point(longitude = position.longitude, latitude = position.latitude)
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
            val trackSource = rememberGeoJsonSource(GeoJsonData.Features(trackGeometry))
            LineLayer(
                id = "live-track",
                source = trackSource,
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
        SatelliteLabel(
            name = state.satelliteName,
            position = Position(longitude = position.longitude, latitude = position.latitude),
            cameraState = cameraState,
        )
    }
}

// The satellite-name label, anchored just above the position marker. Drawn as a Compose overlay
// positioned through the map's own projection rather than a MapLibre SymbolLayer: map-rendered text
// needs a glyphs (font PBF) server in the style, and the inline raster style deliberately
// references nothing but the CartoDB tile source. Isolated in its own composable so the camera
// reads below (which change every frame while the user pans) only recompose this label.
@Composable
private fun SatelliteLabel(name: String, position: Position, cameraState: CameraState) {
    // Read position to re-run on every camera move; projection is null until the map is attached.
    @Suppress("UNUSED_VARIABLE")
    val cameraPosition = cameraState.position
    val projection = cameraState.projection ?: return
    val anchor = projection.screenLocationFromPosition(position)

    Text(
        text = name,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.onPrimaryContainer,
        modifier = Modifier
            // Center horizontally over the marker and sit just above its halo.
            .layout { measurable, constraints ->
                val placeable = measurable.measure(constraints.copy(minWidth = 0, minHeight = 0))
                layout(placeable.width, placeable.height) {
                    placeable.place(
                        x = anchor.x.roundToPx() - placeable.width / 2,
                        y = anchor.y.roundToPx() - placeable.height - LABEL_GAP.roundToPx(),
                    )
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

private const val LIVE_ZOOM = 2.5 // Fits the ~4000 km-wide footprint with room around it.
private const val STATIC_FALLBACK_ZOOM = 3.0
private val LABEL_GAP = 16.dp

private val MAP_OPTIONS = MapOptions(
    // Attribution stays on (required by the CARTO/OSM tile terms); the scale bar would crowd the
    // top bar region and a compass is noise on a north-up overview map.
    ornamentOptions = OrnamentOptions(isScaleBarEnabled = false, isCompassEnabled = false),
)

// Inline style (confirmed decision: no hosted MapLibre/Mapbox style, no API key). One raster
// source — the same CartoDB Dark Matter tiles the web frontend already uses
// (frontend/src/components/SatelliteMap.tsx) — and one raster layer. Leaflet's `{s}`/`{r}`
// placeholders don't exist in MapLibre, so the subdomains are listed explicitly and the retina
// variant (@2x, 512 px) is requested at tileSize 256 for crisp tiles on high-density screens.
private val CARTO_DARK_STYLE = BaseStyle.Json(
    """
    {
      "version": 8,
      "name": "SatTrakk CartoDB Dark Matter",
      "sources": {
        "carto-dark": {
          "type": "raster",
          "tiles": [
            "https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png",
            "https://b.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png",
            "https://c.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png",
            "https://d.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}@2x.png"
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
