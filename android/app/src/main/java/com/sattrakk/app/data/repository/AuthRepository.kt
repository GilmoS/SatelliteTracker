package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.ApiKeyStore
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.RegisterRequest
import com.sattrakk.app.data.util.SafeApiCaller
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.mapSuccess
import javax.inject.Inject
import javax.inject.Singleton

// The raw API key POST /api/auth/register returns is visible exactly once, at this exact moment
// (repo-root CLAUDE.md's beta allowlist section) — this repository is the single place in the app
// that ever touches it: it's handed straight to ApiKeyStore.saveKey here, at the repository
// boundary, rather than returned up to a future ViewModel/UI layer. No Room involvement.
@Singleton
class AuthRepository @Inject constructor(
    private val api: SatTrakkApi,
    private val apiKeyStore: ApiKeyStore,
    private val safeApiCall: SafeApiCaller
) {

    // On any non-Success result (403 not allowlisted, 409 already registered, NetworkError, ...)
    // saveKey is never called, so a failed registration can never leak a key into storage.
    suspend fun register(email: String, displayName: String): ApiResult<Unit> {
        val result = safeApiCall {
            api.register(RegisterRequest(email = email, displayName = displayName))
        }
        if (result is ApiResult.Success) {
            apiKeyStore.saveKey(requireNotNull(result.data.apiKey) { "RegisterResponse.apiKey" })
        }
        return result.mapSuccess { }
    }
}
