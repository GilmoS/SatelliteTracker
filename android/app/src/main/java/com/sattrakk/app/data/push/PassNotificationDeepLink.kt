package com.sattrakk.app.data.push

import android.content.Intent
import java.util.UUID

// The contract between a pass-reminder push and MainActivity. The backend's FCM Data payload
// (FirebaseService.SendPassNotificationAsync — see repo-root CLAUDE.md) carries exactly these two
// keys, and both tap paths deliver them as String extras on MainActivity's Intent under the same
// names:
//  - app backgrounded/killed: FCM itself shows the tray notification from the Notification block
//    and, on tap, launches the launcher Activity with every Data key copied in as an extra;
//  - app foregrounded: SatTrakkMessagingService builds the notification itself and puts the same
//    keys on its PendingIntent.
object PassNotificationDeepLink {
    const val EXTRA_PASS_ID = "passId"
    const val EXTRA_TYPE = "type"
    const val TYPE_PASS_REMINDER = "pass_reminder"

    // Returns the passId to open, or null if this isn't a (well-formed) pass-reminder payload — an
    // ordinary launcher tap, an unknown future `type`, or a malformed id all yield null, so a bad
    // payload can never produce a garbage route.
    fun passIdFrom(getExtra: (String) -> String?): String? {
        if (getExtra(EXTRA_TYPE) != TYPE_PASS_REMINDER) return null
        val raw = getExtra(EXTRA_PASS_ID)?.trim().orEmpty()
        if (raw.isEmpty()) return null
        return runCatching { UUID.fromString(raw) }.getOrNull()?.let { raw }
    }

    fun passIdFrom(intent: Intent?): String? = intent?.let { i -> passIdFrom { key -> i.getStringExtra(key) } }
}
