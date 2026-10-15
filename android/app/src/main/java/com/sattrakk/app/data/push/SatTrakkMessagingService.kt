package com.sattrakk.app.data.push

import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import com.sattrakk.app.MainActivity
import com.sattrakk.app.R
import com.sattrakk.app.data.local.FcmTokenStore
import com.sattrakk.app.data.permission.NotificationPermissionManager
import com.sattrakk.app.di.ApplicationScope
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch

@AndroidEntryPoint
class SatTrakkMessagingService : FirebaseMessagingService() {

    @Inject lateinit var fcmTokenStore: FcmTokenStore
    @Inject lateinit var notificationPermissionManager: NotificationPermissionManager
    @Inject @ApplicationScope lateinit var applicationScope: CoroutineScope

    // Saved unconditionally — no session check here. FcmTokenSyncObserver decides when it can
    // actually be sent (see FcmTokenStore). @ApplicationScope, not a service-lifetime scope: the
    // service may be destroyed right after this returns, and the DataStore write must still land.
    override fun onNewToken(token: String) {
        applicationScope.launch { fcmTokenStore.savePendingToken(token) }
    }

    // Our pushes are Notification+Data hybrids. Per FCM's documented delivery rules, when the app
    // is in the BACKGROUND (or killed) the system shows the Notification block itself and this
    // method is NOT called; the Data keys arrive as launch-Intent extras on tap. When the app is in
    // the FOREGROUND, nothing is shown automatically and this method IS called — so this builds
    // the equivalent notification by hand, with the same extras, so both cases look and deep-link
    // the same. (Documented behavior — not verified on a device in this task; see android/CLAUDE.md.)
    @SuppressLint("MissingPermission") // Checked via notificationPermissionManager.isGranted() below.
    override fun onMessageReceived(message: RemoteMessage) {
        val passId = PassNotificationDeepLink.passIdFrom { key -> message.data[key] } ?: return
        if (!notificationPermissionManager.isGranted()) return

        val tapIntent = Intent(this, MainActivity::class.java).apply {
            // MainActivity is singleTop: an already-running instance gets onNewIntent instead of a
            // second copy being stacked on top.
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
            putExtra(PassNotificationDeepLink.EXTRA_TYPE, PassNotificationDeepLink.TYPE_PASS_REMINDER)
            putExtra(PassNotificationDeepLink.EXTRA_PASS_ID, passId)
        }
        // Per-pass request code so two reminders' PendingIntents don't overwrite each other's extras.
        val pendingIntent = PendingIntent.getActivity(
            this,
            passId.hashCode(),
            tapIntent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val notification = NotificationCompat.Builder(this, PASS_REMINDER_CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_pass_reminder)
            .setContentTitle(message.notification?.title ?: getString(R.string.pass_reminder_fallback_title))
            .setContentText(message.notification?.body)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .build()

        // One notification per pass: a later threshold (e.g. 5 min after the 10 min one) replaces
        // the earlier reminder for the same pass rather than stacking.
        NotificationManagerCompat.from(this).notify(passId.hashCode(), notification)
    }

    companion object {
        // Also declared as FCM's default channel in AndroidManifest.xml, so background (FCM-shown)
        // and foreground (built above) notifications land in the same user-visible channel.
        const val PASS_REMINDER_CHANNEL_ID = "pass_reminders"

        fun createNotificationChannel(context: Context) {
            val channel = NotificationChannel(
                PASS_REMINDER_CHANNEL_ID,
                context.getString(R.string.pass_reminder_channel_name),
                NotificationManager.IMPORTANCE_HIGH
            )
            context.getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
        }
    }
}
