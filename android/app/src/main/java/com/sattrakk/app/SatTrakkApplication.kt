package com.sattrakk.app

import android.app.Application
import com.sattrakk.app.data.push.FcmTokenSyncObserver
import com.sattrakk.app.data.push.SatTrakkMessagingService
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject

@HiltAndroidApp
class SatTrakkApplication : Application() {

    @Inject lateinit var fcmTokenSyncObserver: FcmTokenSyncObserver

    override fun onCreate() {
        super.onCreate() // Hilt injects the fields above here.
        // Must exist before any notification is posted — including FCM's own background-shown
        // ones, which target this channel via AndroidManifest.xml's default_notification_channel_id.
        SatTrakkMessagingService.createNotificationChannel(this)
        // Runs for the whole process lifetime on @ApplicationScope — see FcmTokenSyncObserver.
        fcmTokenSyncObserver.start()
    }
}
