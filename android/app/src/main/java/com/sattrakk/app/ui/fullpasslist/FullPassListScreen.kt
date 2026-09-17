@file:OptIn(ExperimentalMaterial3Api::class)

package com.sattrakk.app.ui.fullpasslist

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.IconButton
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.domain.model.Pass
import com.sattrakk.app.domain.model.TimeWindow
import com.sattrakk.app.navigation.BackArrowIcon
import com.sattrakk.app.navigation.ChevronIcon
import com.sattrakk.app.navigation.CloseIcon
import com.sattrakk.app.navigation.FilterIcon
import com.sattrakk.app.ui.common.formatTimeLocal
import com.sattrakk.app.ui.theme.ScreenContentTopPadding
import com.sattrakk.app.ui.theme.TelemetryTextStyle
import java.time.Duration
import java.time.LocalDate
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale
import kotlin.math.roundToInt

// Full Pass List screen (Milestone E) — replaces its previous one-line placeholder. Wired to the
// already-complete FullPassListViewModel/FullPassListUiState per the code truth map's Screens
// 2/8 (Upcoming), 3/8 (History), and 4/8 (Filter Modal) verdicts. See android/CLAUDE.md for the
// full element-by-element breakdown and the confirmed decisions this task's spec called out
// (single-satellite scope, no staged/draft filter state, ALL as the real default, resetFilters()).
//
// Takes no satelliteId/satelliteName parameters — hiltViewModel() supplies FullPassListViewModel
// its own SavedStateHandle from the same nav backstack entry, so the ViewModel already carries
// both (FullPassListUiState.satelliteId/.satelliteName) without threading them through twice.
@Composable
fun FullPassListScreen(
    modifier: Modifier = Modifier,
    viewModel: FullPassListViewModel = hiltViewModel(),
    onBackClick: () -> Unit = {},
    onPassClick: (String) -> Unit = {},
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var showFilterSheet by rememberSaveable { mutableStateOf(false) }
    // Round-2 fix: rememberLazyListState() (== rememberSaveable(saver = LazyListState.Saver) with
    // NO keys) kept the exact same LazyListState instance for as long as this screen stayed in
    // composition, so switching segments (Upcoming/History/All/Filtered) or picking a new filter
    // value -- both of which reload() the list from scratch, per FullPassListViewModel -- left
    // whatever scroll offset was already there untouched, sometimes pointing partway down (or past
    // the end of) the freshly-reloaded list. Keying on filter/timeWindow/minMaxElevation -- the
    // exact three fields whose change triggers a from-scratch reload -- forces a brand-new
    // LazyListState (starting at index 0) whenever a real reload happens, while a single load-more
    // page append (which changes state.passes but not these three fields) still reuses the same
    // instance and its scroll position, as it should.
    //
    // A fresh navigation entry already starts at the top for free (a new NavBackStackEntry gets a
    // new SaveableStateHolder scope, so there's nothing to restore). Returning to an already-open
    // instance via the bottom nav bar's "Passes" item is the one path that intentionally keeps
    // rememberSaveable's default behavior of restoring the prior scroll offset for the current
    // filter/segment combination -- matching the normal bottom-nav-tab convention of remembering
    // where each tab was left, per this task's own "use your judgment" call.
    val listState = rememberSaveable(state.filter, state.timeWindow, state.minMaxElevation, saver = LazyListState.Saver) {
        LazyListState()
    }

    val groupedItems = remember(state.passes) { buildGroupedItems(state.passes) }
    val activeFilterChips = buildActiveFilterChips(
        timeWindow = state.timeWindow,
        minMaxElevation = state.minMaxElevation,
        onSetTimeWindow = viewModel::setTimeWindow,
        onSetMinMaxElevation = viewModel::setMinMaxElevation,
    )

    // Infinite-scroll pagination: once the last visible row gets within a few items of the end of
    // the currently-loaded list, ask for more history — wired to the ViewModel's existing
    // loadMore()/hasMoreHistory rather than a "Load more" tap target, per this task's instructions.
    // loadMore() itself already no-ops for UPCOMING and while a load is in flight, so this can fire
    // freely without duplicating that guard here.
    LaunchedEffect(listState, groupedItems.size) {
        snapshotFlow { listState.layoutInfo.visibleItemsInfo.lastOrNull()?.index }
            .collect { lastVisibleIndex ->
                if (lastVisibleIndex != null && lastVisibleIndex >= groupedItems.size - 4) {
                    viewModel.loadMore()
                }
            }
    }

    Scaffold(
        modifier = modifier,
        // See MainNavHost's own contentWindowInsets comment — without this, this screen's
        // Scaffold also reserves the status bar (top) and system gesture inset (bottom) on top of
        // what's already correctly reserved once at the app-root Scaffold, doubling both gaps.
        contentWindowInsets = WindowInsets(0),
        topBar = {
            Column {
                TopAppBar(
                    // Per the truth map, the title itself is decorative/static ("Passes") — the
                    // satellite name is shown as a subtitle underneath instead, since this screen
                    // is permanently scoped to one satellite and that's real, already-loaded state
                    // (FullPassListUiState.satelliteName), not something the raw design needed to
                    // show given how it presents multi-satellite tabs instead (see
                    // android/CLAUDE.md).
                    title = {
                        Column {
                            Text("Passes")
                            Text(
                                state.satelliteName,
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    },
                    navigationIcon = {
                        IconButton(onClick = onBackClick) {
                            BackArrowIcon(MaterialTheme.colorScheme.onSurface)
                        }
                    },
                    actions = {
                        // Search icon and overflow menu are omitted outright (truth map: no
                        // backing action for either). The filter button's badge count is derived
                        // here by diffing current timeWindow/minMaxElevation against
                        // FullPassListViewModel's own default constants — no activeFilterCount
                        // field was added to UiState.
                        BadgedBox(badge = {
                            if (activeFilterChips.isNotEmpty()) {
                                Badge { Text("${activeFilterChips.size}") }
                            }
                        }) {
                            IconButton(onClick = { showFilterSheet = true }) {
                                FilterIcon(MaterialTheme.colorScheme.onSurface)
                            }
                        }
                    },
                )
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            }
        },
    ) { innerPadding ->
        Column(modifier = Modifier.fillMaxSize().padding(innerPadding)) {
            // Upcoming/History/All, ALL selected by default (FullPassListUiState's own initial
            // value) — confirmed product requirement, not derived from the design file. FILTERED
            // (design-review bug-fix round) is a fourth, dynamically-appearing segment: it's only
            // included in the row while a filter is actually active (isFilterActive below, the
            // same check FullPassListViewModel.applyFilterActivation uses), not a permanently
            // visible-but-empty fifth option — see android/CLAUDE.md.
            val isFilterActive = state.timeWindow != FullPassListViewModel.DEFAULT_TIME_WINDOW ||
                state.minMaxElevation != FullPassListViewModel.DEFAULT_MIN_MAX_ELEVATION
            val visibleFilters = PassListFilter.entries.filter { it != PassListFilter.FILTERED || isFilterActive }
            SingleChoiceSegmentedButtonRow(
                modifier = Modifier.fillMaxWidth().padding(
                    PaddingValues(start = 16.dp, top = ScreenContentTopPadding, end = 16.dp, bottom = 8.dp),
                ),
            ) {
                visibleFilters.forEachIndexed { index, filter ->
                    SegmentedButton(
                        selected = state.filter == filter,
                        onClick = { viewModel.setFilter(filter) },
                        shape = SegmentedButtonDefaults.itemShape(index, visibleFilters.size),
                        label = { Text(filter.displayName()) },
                    )
                }
            }

            if (activeFilterChips.isNotEmpty()) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .horizontalScroll(rememberScrollState())
                        .padding(horizontal = 16.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    activeFilterChips.forEach { chip ->
                        InputChip(
                            selected = true,
                            onClick = chip.onRemove,
                            label = { Text(chip.label) },
                            trailingIcon = { CloseIcon(MaterialTheme.colorScheme.onSurfaceVariant, Modifier.size(14.dp)) },
                        )
                    }
                }
                Spacer(modifier = Modifier.height(4.dp))
            }

            state.error?.let { error ->
                Text(
                    text = error,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.fillMaxWidth().padding(16.dp, 8.dp),
                )
            }

            when {
                state.isLoadingMore && state.passes.isEmpty() -> {
                    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                state.passes.isEmpty() -> {
                    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        Text(
                            "No passes match these filters.",
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
                else -> {
                    LazyColumn(state = listState, modifier = Modifier.fillMaxSize()) {
                        items(
                            items = groupedItems,
                            key = { item ->
                                when (item) {
                                    is PassListRowItem.DateHeader -> "header-${item.date}"
                                    is PassListRowItem.Row -> item.pass.id
                                }
                            },
                        ) { item ->
                            when (item) {
                                is PassListRowItem.DateHeader -> DateHeaderRow(item.label)
                                is PassListRowItem.Row -> PassRow(
                                    pass = item.pass,
                                    // The nearestPassId boundary marker only means anything in ALL
                                    // (see FullPassListUiState.nearestPassId and
                                    // android/CLAUDE.md) -- it's always null outside ALL anyway,
                                    // but the filter check makes that explicit rather than
                                    // incidental.
                                    isNearest = state.filter == PassListFilter.ALL && item.pass.id == state.nearestPassId,
                                    onClick = { onPassClick(item.pass.id) },
                                )
                            }
                        }
                        if (state.isLoadingMore && state.passes.isNotEmpty()) {
                            item {
                                Box(modifier = Modifier.fillMaxWidth().padding(16.dp), contentAlignment = Alignment.Center) {
                                    CircularProgressIndicator(modifier = Modifier.size(24.dp))
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    if (showFilterSheet) {
        FilterModalSheet(
            timeWindow = state.timeWindow,
            minMaxElevation = state.minMaxElevation,
            currentPassesCount = state.passes.size,
            // Per the FILTERED segment's design: picking a filter value both activates FILTERED
            // (in the ViewModel, see FullPassListViewModel.applyFilterActivation) AND closes the
            // sheet immediately to show the result, rather than leaving it open for further
            // browsing -- see android/CLAUDE.md.
            onSetTimeWindow = { timeWindow -> viewModel.setTimeWindow(timeWindow); showFilterSheet = false },
            onSetMinMaxElevation = { elevation -> viewModel.setMinMaxElevation(elevation); showFilterSheet = false },
            onReset = viewModel::resetFilters,
            onDismiss = { showFilterSheet = false },
        )
    }
}

private fun PassListFilter.displayName(): String = when (this) {
    PassListFilter.UPCOMING -> "Upcoming"
    PassListFilter.HISTORY -> "History"
    PassListFilter.ALL -> "All"
    PassListFilter.FILTERED -> "Filtered"
}

@Composable
private fun DateHeaderRow(label: String) {
    Text(
        text = label,
        style = MaterialTheme.typography.labelMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 10.dp),
    )
}

// isNearest highlights the row matching FullPassListUiState.nearestPassId in the ALL view (design-
// review finding 2b — the marker already existed and was already correctly computed, it just had
// no visual treatment). Background tint + accent time/subtext color mirrors DashboardScreen's own
// "next pass" row treatment (see that file's PassRow) rather than inventing a new visual language.
@Composable
private fun PassRow(pass: Pass, isNearest: Boolean, onClick: () -> Unit) {
    val rowBackground = if (isNearest) MaterialTheme.colorScheme.secondaryContainer else Color.Transparent
    val timeColor = if (isNearest) MaterialTheme.colorScheme.onSecondaryContainer else MaterialTheme.colorScheme.onSurface
    val secondaryTextColor = if (isNearest) MaterialTheme.colorScheme.onSecondaryContainer else MaterialTheme.colorScheme.onSurfaceVariant
    val chevronColor = if (isNearest) MaterialTheme.colorScheme.onSecondaryContainer else MaterialTheme.colorScheme.outline

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 64.dp)
            .background(rowBackground)
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp),
    ) {
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
        ElevationChip(pass.maxElevation)
        // Pass-direction (N->S / S->N) is omitted outright, per the truth map — no such field
        // exists on Pass, and it's explicitly documented as not implemented anywhere.
        ChevronIcon(chevronColor)
    }
}

@Composable
private fun ElevationChip(maxElevation: Double) {
    Surface(
        shape = MaterialTheme.shapes.small,
        color = Color.Transparent,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Text(
            text = "${maxElevation.roundToInt()}°",
            style = TelemetryTextStyle.copy(fontSize = 12.sp),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(horizontal = 8.dp, vertical = 3.dp),
        )
    }
}

// ---- grouping ----

private sealed interface PassListRowItem {
    data class DateHeader(val date: LocalDate, val label: String) : PassListRowItem
    data class Row(val pass: Pass) : PassListRowItem
}

// Groups the already-ordered `passes` list by calendar day at render time, preserving order --
// per this task's instructions, this never re-sorts or re-fetches, it only inserts headers. The
// merged ALL list's upcoming/history boundary (FullPassListUiState.nearestPassId) is deliberately
// NOT surfaced as a separate divider here -- the date headers already make the transition from
// future to past dates visually obvious, and adding a second boundary marker on top would be
// redundant. See android/CLAUDE.md.
private fun buildGroupedItems(passes: List<Pass>): List<PassListRowItem> {
    val today = LocalDate.now()
    val items = mutableListOf<PassListRowItem>()
    var lastDate: LocalDate? = null
    for (pass in passes) {
        val date = pass.aos.atZoneSameInstant(ZoneId.systemDefault()).toLocalDate()
        if (date != lastDate) {
            items += PassListRowItem.DateHeader(date, formatGroupLabel(date, today))
            lastDate = date
        }
        items += PassListRowItem.Row(pass)
    }
    return items
}

private val groupHeaderDateFormatter = DateTimeFormatter.ofPattern("d MMM", Locale.getDefault())

private fun formatGroupLabel(date: LocalDate, today: LocalDate): String {
    val formatted = groupHeaderDateFormatter.format(date).uppercase(Locale.getDefault())
    return when (date) {
        today -> "TODAY · $formatted"
        today.minusDays(1) -> "YESTERDAY · $formatted"
        today.plusDays(1) -> "TOMORROW · $formatted"
        else -> formatted
    }
}

// ---- active filter chips (Composable-layer derivation, no new UiState field) ----

private data class ActiveFilterChip(val label: String, val onRemove: () -> Unit)

// Tapping a chip clears just that one filter (via the relevant setter, with the ViewModel's own
// default constant) -- never resetFilters(), which would also clear the other filter. See this
// task's confirmed decisions.
private fun buildActiveFilterChips(
    timeWindow: TimeWindow,
    minMaxElevation: Double?,
    onSetTimeWindow: (TimeWindow) -> Unit,
    onSetMinMaxElevation: (Double?) -> Unit,
): List<ActiveFilterChip> = buildList {
    if (timeWindow != FullPassListViewModel.DEFAULT_TIME_WINDOW) {
        add(
            ActiveFilterChip(timeWindowChipLabel(timeWindow)) {
                onSetTimeWindow(FullPassListViewModel.DEFAULT_TIME_WINDOW)
            },
        )
    }
    if (minMaxElevation != FullPassListViewModel.DEFAULT_MIN_MAX_ELEVATION) {
        add(
            ActiveFilterChip("El ≥ ${minMaxElevation?.roundToInt()}°") {
                onSetMinMaxElevation(FullPassListViewModel.DEFAULT_MIN_MAX_ELEVATION)
            },
        )
    }
}

private val chipDateFormatter = DateTimeFormatter.ofPattern("d MMM")

// "Last 24h"/"Last 48h" rather than the design's literal "Next 48h" wording -- TimeWindow resolves
// to a look-BACK window (now.minusHours(...), see PassHistoryFilterMappers.resolve), so "Next"
// would describe the wrong direction. A deliberate wording correction, not a literal copy of the
// mockup.
private fun timeWindowChipLabel(timeWindow: TimeWindow): String = when (timeWindow) {
    TimeWindow.Last24h -> "Last 24h"
    TimeWindow.Last48h -> "Last 48h"
    TimeWindow.Last7Days -> "Last 7 days"
    is TimeWindow.Custom -> {
        val from = timeWindow.from?.atZoneSameInstant(ZoneId.systemDefault())?.toLocalDate()?.format(chipDateFormatter)
        val to = timeWindow.to?.atZoneSameInstant(ZoneId.systemDefault())?.toLocalDate()?.format(chipDateFormatter)
        when {
            from == null && to == null -> "All time"
            from != null && to != null -> "$from – $to"
            from != null -> "From $from"
            else -> "Until $to"
        }
    }
}

// ---- formatting (formatTimeLocal is the shared ui/common utility -- see
// android/CLAUDE.md's "Timezone conversion" section; the rest stays a small local duplicate of
// Dashboard's equivalents, not shared, per this screen's "do not touch Dashboard" scope) ----

private fun formatDurationShort(totalSeconds: Int): String {
    val minutes = totalSeconds / 60
    val seconds = totalSeconds % 60
    return "${minutes}m ${seconds.toString().padStart(2, '0')}s"
}

// Unlike Dashboard's countdown (ticking every second), this screen has no per-second ticker, so
// "now" here is only as fresh as the last recomposition (a filter change, pagination, etc.) --
// fine for a list of times rather than a live countdown. Handles both directions, since a merged
// ALL list mixes future (upcoming) and past (history) passes in one list.
private fun formatRelativeTime(aos: OffsetDateTime): String {
    val minutes = Duration.between(OffsetDateTime.now(), aos).toMinutes()
    return when {
        minutes > 0 -> if (minutes < 60) "in $minutes min" else "in ${minutes / 60}h ${minutes % 60}m"
        minutes < 0 -> {
            val agoMinutes = -minutes
            when {
                agoMinutes < 60 -> "$agoMinutes min ago"
                agoMinutes < 24 * 60 -> "${agoMinutes / 60}h ago"
                else -> "${agoMinutes / (24 * 60)}d ago"
            }
        }
        else -> "now"
    }
}
