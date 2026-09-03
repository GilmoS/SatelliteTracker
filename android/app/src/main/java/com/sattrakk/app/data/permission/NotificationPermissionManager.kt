package com.sattrakk.app.data.permission

import android.app.Activity

// Single point where notification permission state is read — ViewModels must never touch
// Context/Activity directly (testability, lifecycle-safety). This wrapper only REPORTS status; it
// does not itself trigger the system permission dialog (that must happen from an Activity/
// Composable in a future UI step, e.g. via rememberLauncherForActivityResult).
interface NotificationPermissionManager {

    fun isGranted(): Boolean

    // True if the user denied the permission once before but hasn't permanently denied it
    // (Android's shouldShowRequestPermissionRationale semantics) — used to decide whether to show
    // an in-app rationale vs. a "go to system settings" prompt for the permanently-denied case.
    //
    // Takes an Activity parameter rather than being no-arg: the underlying platform API
    // (ActivityCompat.shouldShowRequestPermissionRationale) is defined only on Activity, with no
    // Context-only overload available down to this app's minSdk 29 (PackageManager's own
    // Context-based shouldShowRequestPermissionRationale wasn't added until API 34, which would
    // leave API 33 — where POST_NOTIFICATIONS first exists — with no way to ask). Rather than
    // holding an Activity reference in this @Singleton (a leak risk) or unsafely casting the
    // injected Application Context to Activity (would crash — an Application is never an
    // Activity), the caller passes its own Activity in at call time, sourced from the future UI
    // layer (e.g. LocalContext.current as Activity in a Composable) and never stored here. This is
    // a deliberate deviation from a literal no-arg signature, flagged explicitly rather than
    // shipping a signature that can't actually be implemented correctly.
    fun shouldShowRationale(activity: Activity): Boolean
}
