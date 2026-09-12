package com.sattrakk.app.ui.common

import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.util.Locale

// Shared local/UTC time formatting for Dashboard, Full Pass List, and Pass Details — extracted
// during the design-review bug-fix round (android/CLAUDE.md's "Timezone conversion" section) so
// the conversion logic lives in exactly one place instead of three near-identical private copies.
//
// Each of the three screens' own `formatTimeLocal` was independently verified to already call
// `atZoneSameInstant(ZoneId.systemDefault())` correctly (Pass.aos/.los round-trip through the
// backend as real UTC-offset OffsetDateTime values — see repo-root CLAUDE.md — so this was never
// a "relabeled UTC" bug in the code actually shipped). This file doesn't fix a conversion defect;
// it consolidates the three duplicates and makes the zone an explicit, injectable parameter
// (default ZoneId.systemDefault()) so DateTimeFormattingTest can assert against a fixed zone
// instead of depending on the test runner's own system timezone.
// Locale.US pinned explicitly (not the device default) -- these patterns ("EEE, MMM d yyyy")
// render English month/weekday abbreviations regardless, and pinning avoids a formatter whose
// output shape (e.g. "Sep" vs "Sept") silently depends on which JDK/ICU version happens to be
// running, which is exactly what made DateTimeFormattingTest non-deterministic before this pin.
private val TIME_FORMATTER: DateTimeFormatter = DateTimeFormatter.ofPattern("HH:mm", Locale.US)
private val DATE_FORMATTER: DateTimeFormatter = DateTimeFormatter.ofPattern("EEE, MMM d yyyy", Locale.US)

fun formatTimeLocal(dateTime: OffsetDateTime, zone: ZoneId = ZoneId.systemDefault()): String =
    dateTime.atZoneSameInstant(zone).format(TIME_FORMATTER)

fun formatTimeUtc(dateTime: OffsetDateTime): String =
    dateTime.withOffsetSameInstant(ZoneOffset.UTC).format(TIME_FORMATTER)

fun formatDateLocal(dateTime: OffsetDateTime, zone: ZoneId = ZoneId.systemDefault()): String =
    dateTime.atZoneSameInstant(zone).format(DATE_FORMATTER)
