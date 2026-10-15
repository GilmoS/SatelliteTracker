package com.sattrakk.app.data.repository

import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.UpdateAlertMinutesRequest
import com.sattrakk.app.data.remote.dto.UpdateFcmTokenRequest
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.mapper.toDomain
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.UserSettings
import com.sattrakk.app.domain.model.mapSuccess
import javax.inject.Inject
import javax.inject.Singleton

// No Room caching, by explicit decision — see android/CLAUDE.md. All three operations are
// straight safeApiCall passthroughs; if local caching becomes necessary later it should follow
// the existing TTL-gated pattern (data/util/CachedNetworkFirst.kt) rather than a new one.
@Singleton
class SettingsRepository @Inject constructor(
    private val api: SatTrakkApi,
    private val safeApiCall: SafeApiCaller
) {

    // Always succeeds for an authenticated tester — one who's never written to either field gets
    // a computed default from the backend itself (empty alertMinutes, null fcmToken), never a
    // 404, so no special "not found yet" handling is needed here.
    suspend fun getSettings(): ApiResult<UserSettings> =
        safeApiCall { api.getMySettings() }.mapSuccess { it.toDomain() }

    suspend fun updateAlertMinutes(minutes: List<Int>): ApiResult<UserSettings> =
        safeApiCall { api.updateMyAlertMinutes(UpdateAlertMinutesRequest(alertMinutes = minutes)) }
            .mapSuccess { it.toDomain() }

    // PUT /api/settings/me/fcm-token returns the updated UserSettingsDto (same response shape as
    // the other two operations), so this returns mapped domain settings rather than Unit.
    suspend fun updateFcmToken(token: String): ApiResult<UserSettings> =
        safeApiCall { api.updateMyFcmToken(UpdateFcmTokenRequest(fcmToken = token)) }
            .mapSuccess { it.toDomain() }
}
