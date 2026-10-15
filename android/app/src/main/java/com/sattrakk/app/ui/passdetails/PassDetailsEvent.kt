package com.sattrakk.app.ui.passdetails

// One-shot navigation signal, kept out of PassDetailsUiState per the state-vs-event distinction
// established in step 3.1 (SessionManager): session invalidity is state because it's persistent
// and must survive recomposition/process death, but "navigate to the map" is a genuinely one-time
// action, which is the official Android guidance's own carve-out for staying event-based. This is
// the first Channel-based event stream in the app — no prior convention existed to match, so this
// establishes it for future screens.
//
// The Map screen doesn't exist yet (step 6), so this event has no listener today — that's
// expected, not a gap. It's built ready for that future wiring.
sealed interface PassDetailsEvent {
    data class NavigateToMap(val passId: String) : PassDetailsEvent
}
