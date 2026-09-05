@file:OptIn(ExperimentalMaterial3Api::class)

package com.sattrakk.app.ui.fullpasslist

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.Button
import androidx.compose.material3.DatePicker
import androidx.compose.material3.DatePickerDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Slider
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberDatePickerState
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.sattrakk.app.domain.model.TimeWindow
import com.sattrakk.app.navigation.CloseIcon
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter

// The Filter Modal bottom sheet (Milestone E) — per the code truth map's Screen 4/8 verdicts.
// Every control here calls its ViewModel setter immediately on change (point 2 of this task's
// confirmed decisions: no staged/draft filter state, "Show N passes" is a plain dismiss showing
// the CURRENT passes.size, not a hypothetical preview). Duration/direction/sunlit/horizon-mask
// controls from the design are omitted entirely, not rendered disabled — no backend param exists
// for any of them (truth map, [DECORATIVE]). The satellite multi-select chips are omitted too —
// this screen is permanently single-satellite (see android/CLAUDE.md).
@Composable
fun FilterModalSheet(
    timeWindow: TimeWindow,
    minMaxElevation: Double?,
    currentPassesCount: Int,
    onSetTimeWindow: (TimeWindow) -> Unit,
    onSetMinMaxElevation: (Double?) -> Unit,
    onReset: () -> Unit,
    onDismiss: () -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)

    ModalBottomSheet(onDismissRequest = onDismiss, sheetState = sheetState) {
        Column(modifier = Modifier.padding(horizontal = 20.dp).padding(bottom = 28.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Filter passes", style = MaterialTheme.typography.titleLarge)
                // Fulfils the truth map's "[REAL] Cancel — pure UI dismissal" item: since every
                // control below already applies immediately, a separate "Cancel" button would do
                // exactly what this × (and the scrim/back gesture) already does, so no duplicate
                // button was added for it.
                IconButton(onClick = onDismiss) {
                    CloseIcon(MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }

            Spacer(modifier = Modifier.height(20.dp))
            Text(
                "Time window",
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(modifier = Modifier.height(10.dp))
            TimeWindowChips(timeWindow = timeWindow, onSetTimeWindow = onSetTimeWindow)

            Spacer(modifier = Modifier.height(28.dp))
            ElevationSlider(minMaxElevation = minMaxElevation, onSetMinMaxElevation = onSetMinMaxElevation)

            Spacer(modifier = Modifier.height(28.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                TextButton(onClick = onReset) { Text("Reset") }
                // Plain dismiss showing the CURRENT (already loaded) count — not a live preview of
                // what a not-yet-applied filter would return. See this task's confirmed decisions.
                Button(onClick = onDismiss) { Text("Show $currentPassesCount passes") }
            }
        }
    }
}

private enum class TimeWindowOption(val label: String) {
    LAST_24H("Last 24h"),
    LAST_48H("Last 48h"),
    LAST_7_DAYS("Last 7 days"),
    CUSTOM_RANGE("Custom range"),
    ALL_TIME("All time"),
}

// TimeWindow.Custom(null, null) is the one way this filter model can express "no time
// constraint" (see TimeWindow's own doc comment) — surfaced here as its own explicitly-labeled
// "All time" chip rather than a hidden/unlabeled empty-Custom state, per this task's instructions.
// Any other Custom (either bound non-null) reads as "Custom range".
private fun TimeWindow.toOption(): TimeWindowOption = when (this) {
    TimeWindow.Last24h -> TimeWindowOption.LAST_24H
    TimeWindow.Last48h -> TimeWindowOption.LAST_48H
    TimeWindow.Last7Days -> TimeWindowOption.LAST_7_DAYS
    is TimeWindow.Custom -> if (from == null && to == null) TimeWindowOption.ALL_TIME else TimeWindowOption.CUSTOM_RANGE
}

@Composable
private fun TimeWindowChips(timeWindow: TimeWindow, onSetTimeWindow: (TimeWindow) -> Unit) {
    val currentOption = timeWindow.toOption()
    // Purely local UI state: whether the custom from/to date fields are expanded. Tapping "Custom
    // range" only reveals these fields — it doesn't call onSetTimeWindow by itself, since there's
    // no date chosen yet to apply. This is not staged filter state (nothing here overrides what's
    // actually applied); it's just which section of the sheet is visible.
    var customRangeExpanded by remember { mutableStateOf(currentOption == TimeWindowOption.CUSTOM_RANGE) }
    val customFrom = (timeWindow as? TimeWindow.Custom)?.from
    val customTo = (timeWindow as? TimeWindow.Custom)?.to

    Row(
        modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        TimeWindowOption.entries.forEach { option ->
            val selected = if (option == TimeWindowOption.CUSTOM_RANGE) {
                customRangeExpanded || currentOption == TimeWindowOption.CUSTOM_RANGE
            } else {
                currentOption == option
            }
            FilterChip(
                selected = selected,
                onClick = {
                    when (option) {
                        TimeWindowOption.LAST_24H -> onSetTimeWindow(TimeWindow.Last24h)
                        TimeWindowOption.LAST_48H -> onSetTimeWindow(TimeWindow.Last48h)
                        TimeWindowOption.LAST_7_DAYS -> onSetTimeWindow(TimeWindow.Last7Days)
                        TimeWindowOption.ALL_TIME -> {
                            customRangeExpanded = false
                            onSetTimeWindow(TimeWindow.Custom(null, null))
                        }
                        TimeWindowOption.CUSTOM_RANGE -> customRangeExpanded = true
                    }
                },
                label = { Text(option.label) },
            )
        }
    }

    if (customRangeExpanded) {
        Spacer(modifier = Modifier.height(12.dp))
        DateBoundField(
            label = "From",
            value = customFrom,
            onPicked = { picked -> onSetTimeWindow(TimeWindow.Custom(picked, customTo)) },
            onCleared = { onSetTimeWindow(TimeWindow.Custom(null, customTo)) },
        )
        Spacer(modifier = Modifier.height(8.dp))
        DateBoundField(
            label = "To",
            value = customTo,
            onPicked = { picked ->
                // "To" is treated as through-the-end-of-that-day (the day's exclusive next-day
                // boundary), not that day's midnight — a judgment call, flagged rather than
                // silently decided: "up to and including this day" reads as the more useful
                // meaning for a history filter than "up to this day's start."
                onSetTimeWindow(TimeWindow.Custom(customFrom, picked.plusDays(1)))
            },
            onCleared = { onSetTimeWindow(TimeWindow.Custom(customFrom, null)) },
        )
    }
}

@Composable
private fun DateBoundField(
    label: String,
    value: OffsetDateTime?,
    onPicked: (OffsetDateTime) -> Unit,
    onCleared: () -> Unit,
) {
    var showPicker by remember { mutableStateOf(false) }
    val formatter = remember { DateTimeFormatter.ofPattern("d MMM yyyy") }
    val displayDate = value?.atZoneSameInstant(ZoneId.systemDefault())?.toLocalDate()

    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        TextButton(onClick = { showPicker = true }) {
            Text("$label: ${displayDate?.format(formatter) ?: "Any"}")
        }
        if (displayDate != null) {
            TextButton(onClick = onCleared) { Text("Clear") }
        }
    }

    if (showPicker) {
        val initialMillis = displayDate?.atStartOfDay(ZoneOffset.UTC)?.toInstant()?.toEpochMilli()
        val pickerState = rememberDatePickerState(initialSelectedDateMillis = initialMillis)
        DatePickerDialog(
            onDismissRequest = { showPicker = false },
            confirmButton = {
                TextButton(onClick = {
                    pickerState.selectedDateMillis?.let { millis ->
                        val pickedDate = Instant.ofEpochMilli(millis).atZone(ZoneOffset.UTC).toLocalDate()
                        // The picker's own calendar is read as the date the user means in their
                        // local timezone; converting through UTC only round-trips the picker
                        // widget's internal representation, not the user's intent.
                        onPicked(pickedDate.atStartOfDay(ZoneId.systemDefault()).toOffsetDateTime())
                    }
                    showPicker = false
                }) { Text("OK") }
            },
            dismissButton = { TextButton(onClick = { showPicker = false }) { Text("Cancel") } },
        ) {
            DatePicker(state = pickerState)
        }
    }
}

@Composable
private fun ElevationSlider(minMaxElevation: Double?, onSetMinMaxElevation: (Double?) -> Unit) {
    // Local float mirrors the slider thumb for smooth dragging; the ViewModel setter only fires on
    // release (onValueChangeFinished) — reloading on every intermediate drag value would be a
    // network call per pixel, which "applies immediately" was never meant to require (see this
    // task's confirmed decisions: immediate means no separate confirm step, not no debouncing).
    var sliderValue by remember(minMaxElevation) { mutableFloatStateOf(minMaxElevation?.toFloat() ?: 0f) }

    Text(
        if (sliderValue <= 0f) "Minimum max elevation: No minimum" else "Minimum max elevation: ${sliderValue.toInt()}°+",
        style = MaterialTheme.typography.titleSmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    Slider(
        value = sliderValue,
        onValueChange = { sliderValue = it },
        onValueChangeFinished = { onSetMinMaxElevation(if (sliderValue <= 0f) null else sliderValue.toDouble()) },
        valueRange = 0f..90f,
    )
}
