package com.sattrakk.app.ui.settings

data class SatelliteVisibility(
    val satelliteId: String,
    val satelliteName: String,
    val isHidden: Boolean
)

data class SettingsUiState(
    val satellites: List<SatelliteVisibility> = emptyList(),
    val alertMinutes: Set<Int> = emptySet(), // empty set = sendPush is effectively off
    // In-memory ONLY — never persisted, never read from/written to any repository. Resets to
    // empty on process death: turning push off, killing the app, and reopening loses the
    // "remembered" selection. Accepted tradeoff, not a bug — see needsAlertMinutesSelection and
    // android/CLAUDE.md.
    val lastNonEmptyAlertMinutes: Set<Int> = emptySet(),
    val pushPermissionGranted: Boolean = false,
    val pushPermissionShouldShowRationale: Boolean = false,
    // Set when setSendPushEnabled(true) is called but there's no lastNonEmptyAlertMinutes to
    // restore this session (e.g. fresh app start, never toggled off-then-on-again yet). Chosen
    // resolution — flagged explicitly, per the task's own instructions, as one of two defensible
    // options — over silently picking default alert minutes on the user's behalf: alertMinutes is
    // left untouched (no backend call is made) and this flag tells the future UI to prompt the
    // user to pick at least one alert minute instead. Cleared by the next successful
    // updateAlertMinutes call.
    val needsAlertMinutesSelection: Boolean = false,
    // Set by addSatellite()/removeSatellite() — both are stubs (no backend support exists for
    // satellite catalog management yet). A dedicated field rather than reusing `error`, so a
    // future UI can render it as an informational snackbar, not an error state. Cleared by
    // consumeStubMessage().
    val stubMessage: String? = null,
    val isLoading: Boolean = true,
    val error: String? = null
) {
    // Deliberately a computed property, not a stored constructor field, even though the task
    // sketch lists it alongside the other constructor params: storing it separately would let a
    // copy() call set alertMinutes and sendPushEnabled out of sync (e.g. a copy that updates one
    // but forgets the other). Computing it from alertMinutes makes that class of bug impossible
    // while preserving the same "derived: alertMinutes.isNotEmpty()" meaning.
    val sendPushEnabled: Boolean
        get() = alertMinutes.isNotEmpty()
}
