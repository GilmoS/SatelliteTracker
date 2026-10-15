package com.sattrakk.app.data.push

import android.util.Log
import com.google.firebase.messaging.FirebaseMessaging
import com.sattrakk.app.data.local.FcmTokenStore
import com.sattrakk.app.di.ApplicationScope
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch

// Proactively asks FCM for this device's current token and drops it into FcmTokenStore's pending
// slot, from where FcmTokenSyncObserver sends it. Needed because SatTrakkMessagingService.onNewToken
// only fires when a token is created or rotated — never on an ordinary app start — so an app whose
// token is already stable has no other way to feed the sync flow (e.g. after re-registering under a
// new ApiKey, whose UserSettings row has no token yet). An interface purely so ViewModels/the sync
// observer can be unit-tested without FirebaseMessaging's static singleton.
interface FcmTokenFetcher {
    // Fire-and-forget: the result lands in FcmTokenStore, never returned to the caller.
    fun fetchToken()
}

@Singleton
class FirebaseFcmTokenFetcher @Inject constructor(
    private val fcmTokenStore: FcmTokenStore,
    @ApplicationScope private val applicationScope: CoroutineScope
) : FcmTokenFetcher {

    override fun fetchToken() {
        FirebaseMessaging.getInstance().token
            .addOnSuccessListener { token ->
                applicationScope.launch { fcmTokenStore.savePendingToken(token) }
            }
            // No retry here: onNewToken still covers rotation, and the next proactive trigger
            // (re-registration / first Dashboard load on a fresh install) tries again.
            .addOnFailureListener { e -> Log.w(TAG, "Fetching FCM token failed", e) }
    }

    private companion object {
        const val TAG = "FcmTokenFetcher"
    }
}
