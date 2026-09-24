package com.sattrakk.app.ui.dashboard

import android.Manifest
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.PrimaryTabRow
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.PathMeasure
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.scale
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.navigation.ChevronIcon
import com.sattrakk.app.navigation.OrbitIcon
import com.sattrakk.app.ui.common.formatTimeLocal
import com.sattrakk.app.ui.theme.OnSecondaryContainerVariant
import com.sattrakk.app.ui.theme.ScreenContentBottomPadding
import com.sattrakk.app.ui.theme.ScreenContentTopPadding
import com.sattrakk.app.ui.theme.TelemetryTextStyle
import java.time.Duration
import java.time.OffsetDateTime
import kotlin.math.roundToInt

// Real Dashboard content, wired to the already-complete DashboardViewModel/DashboardUiState (see
// android/CLAUDE.md's DashboardViewModel section) per the M3 Home screen of the design (Claude
// Design project "Map detail and AR improvements", "SatelliteTracker M3.dc.html", option 2a).
// Every element's REAL/PARTIAL/DECORATIVE treatment follows the code-truth-map audit — see
// android/CLAUDE.md for the full breakdown of what was omitted (status bar, notification bell,
// elapsed-ring percentage, alert chip) and why.
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    modifier: Modifier = Modifier,
    viewModel: DashboardViewModel = hiltViewModel(),
    onSelectedSatelliteChanged: (satelliteId: String, satelliteName: String) -> Unit = { _, _ -> },
    onViewFullPassList: (satelliteId: String, satelliteName: String) -> Unit = { _, _ -> },
    onPassClick: (passId: String) -> Unit = {},
    onOpenMap: () -> Unit = {},
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    // The one-time POST_NOTIFICATIONS request after the first successful load — same
    // RequestPermission launcher mechanism SettingsScreen's permission card uses. The ViewModel
    // decides *whether* to ask (and only sets this on API 33+ with the permission not yet granted);
    // this only launches the dialog and reports the result back.
    val requestNotificationPermission by viewModel.requestNotificationPermission.collectAsStateWithLifecycle()
    val notificationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) { granted -> viewModel.onNotificationPermissionResult(granted) }
    LaunchedEffect(requestNotificationPermission) {
        if (requestNotificationPermission) {
            viewModel.onNotificationPermissionRequestLaunched()
            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }

    // Reports the Dashboard's current selection up to the nav-bar level (see MainNavHost) so the
    // bottom nav's "Passes" item knows which satellite to open. Keyed on selectedSatelliteId
    // alone, not the whole state, so it only re-fires on an actual selection change — not on
    // every countdown tick, which produces a new Content instance every second.
    val content = state as? DashboardUiState.Content
    LaunchedEffect(content?.selectedSatelliteId) {
        val selectedId = content?.selectedSatelliteId ?: return@LaunchedEffect
        val tab = content.tabs.find { it.satelliteId == selectedId } ?: return@LaunchedEffect
        onSelectedSatelliteChanged(tab.satelliteId, tab.satelliteName)
    }

    Scaffold(
        modifier = modifier,
        topBar = {
            Column {
                TopAppBar(title = { Text("SatelliteTracker") })
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            }
        },
        // See MainNavHost's own contentWindowInsets comment — without this, this screen's
        // Scaffold also reserves the status bar (top) and system gesture inset (bottom) on top of
        // what's already correctly reserved once at the app-root Scaffold, doubling both gaps.
        contentWindowInsets = WindowInsets(0),
        floatingActionButton = {
            // FAB is pure navigation (truth map: "[PARTIAL] FAB ... fine as pure navigation to
            // Map/Sky View, routes exist"). Map was chosen as the target — the FAB's icon reads as
            // "track on a map" (an orbiting dot), and Sky View remains reachable from the bottom
            // nav bar either way.
            FloatingActionButton(onClick = onOpenMap) {
                OrbitIcon(MaterialTheme.colorScheme.onPrimaryContainer)
            }
        },
    ) { innerPadding ->
        // Pull-to-refresh was previously entirely unwired -- DashboardViewModel.refresh() existed
        // and worked, but no gesture handler in this Composable ever called it (see
        // android/CLAUDE.md's DashboardViewModel section and this task's design-review findings).
        // isRefreshing binds to DashboardUiState.Content.isRefreshing, added for this purpose;
        // Loading/Error states simply aren't refreshable (onRefresh no-ops via the ViewModel's own
        // early-return when not Content).
        PullToRefreshBox(
            isRefreshing = (state as? DashboardUiState.Content)?.isRefreshing ?: false,
            onRefresh = viewModel::refresh,
            modifier = Modifier.padding(innerPadding),
        ) {
            when (val s = state) {
                DashboardUiState.Loading -> LoadingContent(PaddingValues())
                is DashboardUiState.Error -> ErrorContent(s.message, PaddingValues())
                is DashboardUiState.Content -> DashboardContent(
                    state = s,
                    contentPadding = PaddingValues(top = ScreenContentTopPadding, bottom = ScreenContentBottomPadding),
                    onTabSelected = viewModel::selectTab,
                    onViewFullPassList = onViewFullPassList,
                    onPassClick = onPassClick,
                )
            }
        }
    }
}

@Composable
private fun LoadingContent(innerPadding: PaddingValues) {
    Box(
        modifier = Modifier.fillMaxSize().padding(innerPadding),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator()
    }
}

@Composable
private fun ErrorContent(message: String, innerPadding: PaddingValues) {
    Box(
        modifier = Modifier.fillMaxSize().padding(innerPadding),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            text = message,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.error,
            modifier = Modifier.padding(24.dp),
        )
    }
}

@Composable
private fun DashboardContent(
    state: DashboardUiState.Content,
    contentPadding: PaddingValues,
    onTabSelected: (String) -> Unit,
    onViewFullPassList: (satelliteId: String, satelliteName: String) -> Unit,
    onPassClick: (passId: String) -> Unit,
) {
    if (state.tabs.isEmpty()) {
        Box(modifier = Modifier.fillMaxSize().padding(contentPadding), contentAlignment = Alignment.Center) {
            Text("No satellites configured.", style = MaterialTheme.typography.bodyLarge)
        }
        return
    }

    val selectedTab = state.tabs.find { it.satelliteId == state.selectedSatelliteId } ?: state.tabs.first()
    val selectedIndex = state.tabs.indexOf(selectedTab)

    LazyColumn(contentPadding = contentPadding, modifier = Modifier.fillMaxSize()) {
        item {
            PrimaryTabRow(selectedTabIndex = selectedIndex) {
                state.tabs.forEachIndexed { index, tab ->
                    Tab(
                        selected = index == selectedIndex,
                        onClick = { onTabSelected(tab.satelliteId) },
                        text = { Text(tab.satelliteName) },
                    )
                }
            }
        }

        selectedTab.loadError?.let { error ->
            item {
                Text(
                    text = error,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.fillMaxWidth().padding(16.dp, 8.dp),
                )
            }
        }

        item {
            Column(modifier = Modifier.padding(16.dp, 8.dp)) {
                val nextPass = selectedTab.nextPass
                if (nextPass != null && selectedTab.nextPassCountdown != null) {
                    HeroPassCard(
                        satelliteName = selectedTab.satelliteName,
                        pass = nextPass,
                        countdown = selectedTab.nextPassCountdown,
                    )
                    Column(modifier = Modifier.padding(top = 14.dp)) {
                        MetricGrid(nextPass)
                    }
                } else {
                    Text(
                        text = "No upcoming passes.",
                        style = MaterialTheme.typography.bodyLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth().padding(16.dp, 4.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    text = "Upcoming passes",
                    style = MaterialTheme.typography.titleSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                // No exact design element maps to this per the code truth map — placed here,
                // next to the section it lists, as the Dashboard-side entry point into the Full
                // Pass List screen (the other of the two planned entry points; the bottom nav
                // bar's "Passes" item is the other — see android/CLAUDE.md).
                TextButton(onClick = { onViewFullPassList(selectedTab.satelliteId, selectedTab.satelliteName) }) {
                    Text("View all")
                }
            }
        }

        // Renders visiblePasses (already excludes passes whose AOS has slipped into the past
        // while the TTL-gated cache was still "fresh"), never the raw passes list — see
        // SatelliteTabState.visiblePasses and android/CLAUDE.md.
        if (selectedTab.visiblePasses.isEmpty()) {
            item {
                Text(
                    text = "No upcoming passes for ${selectedTab.satelliteName}.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(16.dp, 4.dp),
                )
            }
        } else {
            items(selectedTab.visiblePasses, key = { it.id }) { pass ->
                PassRow(
                    pass = pass,
                    satelliteName = selectedTab.satelliteName,
                    isNextPass = pass.id == selectedTab.nextPass?.id,
                    onClick = { onPassClick(pass.id) },
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 1.dp),
                )
            }
        }
    }
}

@Composable
private fun HeroPassCard(satelliteName: String, pass: Pass, countdown: Duration) {
    Card(
        shape = MaterialTheme.shapes.large,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // The old elapsed-ratio ring TODO is resolved: this is a purely decorative, looping
            // animation (a satellite dot tracing an elevation-arc trajectory), not a computed
            // elapsed percentage — that idea remains explicitly rejected. See PassArcAnimation's
            // own doc comment and android/CLAUDE.md's HeroPassCard section.
            PassArcAnimation(modifier = Modifier.padding(end = 18.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = "Next pass",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    text = formatCountdown(countdown),
                    style = TelemetryTextStyle.copy(fontSize = 32.sp, letterSpacing = (-0.5).sp),
                    color = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.padding(top = 4.dp),
                )
                Surface(
                    shape = MaterialTheme.shapes.small,
                    color = MaterialTheme.colorScheme.primaryContainer,
                    modifier = Modifier.padding(top = 9.dp),
                ) {
                    Text(
                        text = "$satelliteName · orbit ${pass.orbitNumber}",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onPrimaryContainer,
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 5.dp),
                    )
                }
            }
        }
    }
}

// Purely decorative, continuous looping animation — replaces the removed elapsed-ratio ring
// placeholder (see HeroPassCard's call site and android/CLAUDE.md). Reproduces, with Compose's
// Canvas + PathMeasure, the small "pass trace" SVG animation from the design's 2a Home screen
// (Claude Design project "Map detail and AR improvements", "SatelliteTracker M3.dc.html"): a dot
// endlessly tracing a stylized elevation-arc trajectory over a faint horizon/elevation-dome guide.
//
// This is NOT bound to any real value — not nextPassCountdown, not an elapsed fraction, not any
// other Pass/UiState field. It runs on its own infinite clock regardless of the actual pass's
// timing; only its visual motion happens to suggest "something moving along an arc," which the
// design itself treats as pure atmosphere, not a progress indicator (the design's own literal
// percentage-ring idea was already rejected — see the TODO(design) history at HeroPassCard).
//
// Isolated into its own composable specifically so the per-frame animation state is read inside
// Canvas's draw-phase lambda (a DrawScope.() -> Unit run during drawing, not recomposition) rather
// than in HeroPassCard's own body — this way the animation ticking triggers a re-draw of just this
// small Canvas every frame and never a recomposition of HeroPassCard, the countdown text, or
// MetricGrid. rememberInfiniteTransition's animation coroutine is scoped to this composable's own
// composition, so it's cancelled automatically once HeroPassCard (and this) leaves composition —
// e.g. switching tabs to a satellite with no next pass, or navigating off Dashboard.
@Composable
private fun PassArcAnimation(modifier: Modifier = Modifier) {
    val transition = rememberInfiniteTransition(label = "passArcTrace")
    val progress by transition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(animation = tween(durationMillis = 4800, easing = LinearEasing)),
        label = "passArcProgress",
    )

    val guideColor = MaterialTheme.colorScheme.outlineVariant
    val trackColor = MaterialTheme.colorScheme.primaryContainer
    val traceColor = MaterialTheme.colorScheme.primary
    val dotColor = MaterialTheme.colorScheme.onPrimaryContainer

    // The trajectory curve itself never changes — built once, not on every frame, even though the
    // Canvas below redraws every frame.
    val trajectory = remember {
        Path().apply {
            moveTo(12f, 84f)
            quadraticTo(52f, 6f, 92f, 84f)
        }
    }
    val pathMeasure = remember { PathMeasure() }
    val traceSegment = remember { Path() }
    val dashEffect = remember { PathEffect.dashPathEffect(floatArrayOf(2f, 3f)) }

    Canvas(modifier = modifier.size(88.dp)) {
        // Original design coordinates are laid out on a 104x104 grid — scale uniformly to
        // whatever size this Canvas actually renders at instead of assuming a fixed dp value.
        val s = size.minDimension / 104f
        scale(scale = s, pivot = Offset.Zero) {
            // Faint elevation-dome guide arcs + horizon line — static chrome framing the trace.
            drawArc(
                color = guideColor,
                startAngle = 180f,
                sweepAngle = 180f,
                useCenter = false,
                topLeft = Offset(8f, 40f),
                size = Size(88f, 88f),
                style = Stroke(width = 1f),
            )
            drawArc(
                color = guideColor,
                startAngle = 180f,
                sweepAngle = 180f,
                useCenter = false,
                topLeft = Offset(23f, 55f),
                size = Size(58f, 58f),
                style = Stroke(width = 1f, pathEffect = dashEffect),
            )
            drawArc(
                color = guideColor,
                startAngle = 180f,
                sweepAngle = 180f,
                useCenter = false,
                topLeft = Offset(38f, 70f),
                size = Size(28f, 28f),
                style = Stroke(width = 1f, pathEffect = dashEffect),
            )
            drawLine(color = guideColor, start = Offset(4f, 84f), end = Offset(100f, 84f), strokeWidth = 1f)

            // Full (dim) trajectory — always visible in full, the same curve the bright trace
            // below animates along.
            drawPath(trajectory, color = trackColor, style = Stroke(width = 2.5f, cap = StrokeCap.Round))

            // Bright trace, drawn from the start up to the current animated progress — the
            // Compose equivalent of the SVG's animated stroke-dashoffset "self-drawing" line.
            pathMeasure.setPath(trajectory, false)
            val length = pathMeasure.length
            traceSegment.reset()
            pathMeasure.getSegment(0f, length * progress, traceSegment, true)
            drawPath(traceSegment, color = traceColor, style = Stroke(width = 2.5f, cap = StrokeCap.Round))

            // Moving dot (soft glow + bright core), positioned along the curve at the same
            // progress — the Compose equivalent of the SVG's animateMotion dots.
            val dotPosition = pathMeasure.getPosition(length * progress)
            drawCircle(color = traceColor, radius = 7f, center = dotPosition, alpha = 0.18f)
            drawCircle(color = dotColor, radius = 3.4f, center = dotPosition)
        }
    }
}

@Composable
private fun MetricGrid(pass: Pass) {
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
        MetricCard("AOS", formatTimeLocal(pass.aos), Modifier.weight(1f))
        MetricCard("LOS", formatTimeLocal(pass.los), Modifier.weight(1f))
        MetricCard(
            "MAX EL",
            "${pass.maxElevation.roundToInt()}°",
            Modifier.weight(1f),
            valueColor = MaterialTheme.colorScheme.primary,
        )
        MetricCard("DUR", formatDurationColon(pass.durationSec), Modifier.weight(1f))
    }
}

@Composable
private fun MetricCard(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    valueColor: Color = Color.Unspecified,
) {
    Card(
        shape = MaterialTheme.shapes.medium,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerHigh),
        modifier = modifier,
    ) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(vertical = 10.dp, horizontal = 6.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(
                value,
                style = TelemetryTextStyle.copy(fontSize = 14.sp),
                color = if (valueColor == Color.Unspecified) MaterialTheme.colorScheme.onSurface else valueColor,
                modifier = Modifier.padding(top = 5.dp),
            )
        }
    }
}

@Composable
private fun PassRow(
    pass: Pass,
    satelliteName: String,
    isNextPass: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val rowBackground = if (isNextPass) MaterialTheme.colorScheme.secondaryContainer else Color.Transparent
    val timeColor = if (isNextPass) MaterialTheme.colorScheme.onSecondaryContainer else MaterialTheme.colorScheme.onSurface
    val secondaryTextColor = if (isNextPass) OnSecondaryContainerVariant else MaterialTheme.colorScheme.onSurfaceVariant
    val chevronColor = if (isNextPass) OnSecondaryContainerVariant else MaterialTheme.colorScheme.outline
    val avatarBackground = if (isNextPass) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.secondaryContainer
    val avatarTextColor = if (isNextPass) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSecondaryContainer

    Row(
        modifier = modifier
            .fillMaxWidth()
            .heightIn(min = 72.dp)
            .clip(MaterialTheme.shapes.large)
            .background(rowBackground)
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        // Satellite-name initials — not a modeled concept anywhere in the schema (no avatar/
        // abbreviation field on Satellite), so this is a generic derivation (first two
        // letters/digits of the name) rather than a hardcoded "EROS C3" -> "E3" mapping, which
        // would violate DashboardViewModel's "never hardcoded to specific satellites" rule for an
        // arbitrary future satellite list. Highlighting (primary vs. secondaryContainer) tracks
        // whether this row is the same pass shown in the hero card above, not satellite identity.
        Box(
            modifier = Modifier.size(40.dp).background(avatarBackground, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = avatarInitials(satelliteName),
                style = MaterialTheme.typography.labelLarge,
                color = avatarTextColor,
            )
        }
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = "${formatTimeLocal(pass.aos)} → ${formatTimeLocal(pass.los)}",
                style = TelemetryTextStyle.copy(fontSize = 15.sp),
                color = timeColor,
            )
            Text(
                text = "${formatRelativeTime(pass.aos)} · ${formatDurationShort(pass.durationSec)} · #${pass.orbitNumber}",
                style = MaterialTheme.typography.bodySmall,
                color = secondaryTextColor,
                modifier = Modifier.padding(top = 3.dp),
            )
        }
        ElevationChip(pass.maxElevation, isNextPass)
        ChevronIcon(chevronColor)
    }
}

@Composable
private fun ElevationChip(maxElevation: Double, isNextPass: Boolean) {
    val text = "${maxElevation.roundToInt()}°"
    if (isNextPass) {
        Surface(shape = MaterialTheme.shapes.small, color = MaterialTheme.colorScheme.primary) {
            Text(
                text,
                style = TelemetryTextStyle.copy(fontSize = 12.sp),
                color = MaterialTheme.colorScheme.onPrimary,
                modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp),
            )
        }
    } else {
        Surface(
            shape = MaterialTheme.shapes.small,
            color = Color.Transparent,
            border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
        ) {
            Text(
                text,
                style = TelemetryTextStyle.copy(fontSize = 12.sp),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp),
            )
        }
    }
}

private fun avatarInitials(satelliteName: String): String =
    satelliteName.trim().filter { it.isLetterOrDigit() }.take(2).uppercase().ifEmpty { "?" }

private fun formatCountdown(duration: Duration): String {
    val totalSeconds = duration.seconds.coerceAtLeast(0)
    val hours = totalSeconds / 3600
    val minutes = (totalSeconds % 3600) / 60
    val seconds = totalSeconds % 60
    return "%02d:%02d:%02d".format(hours, minutes, seconds)
}

private fun formatDurationColon(totalSeconds: Int): String {
    val minutes = totalSeconds / 60
    val seconds = totalSeconds % 60
    return "$minutes:${seconds.toString().padStart(2, '0')}"
}

private fun formatDurationShort(totalSeconds: Int): String {
    val minutes = totalSeconds / 60
    val seconds = totalSeconds % 60
    return "${minutes}m ${seconds.toString().padStart(2, '0')}s"
}

// Computed fresh at composition time rather than via a ViewModel field — DashboardViewModel's
// countdown ticker already causes a Content recomposition every second (see
// DashboardViewModel.startCountdownTicker), so this naturally stays live without its own clock
// injection. See the code truth map: "[PARTIAL] Per-row relative time ... ad-hoc computation from
// Pass.aos at render time."
private fun formatRelativeTime(aos: OffsetDateTime): String {
    val minutes = Duration.between(OffsetDateTime.now(), aos).toMinutes()
    if (minutes <= 0) return "now"
    if (minutes < 60) return "in $minutes min"
    val hours = minutes / 60
    val remainingMinutes = minutes % 60
    return "in ${hours}h ${remainingMinutes}m"
}
