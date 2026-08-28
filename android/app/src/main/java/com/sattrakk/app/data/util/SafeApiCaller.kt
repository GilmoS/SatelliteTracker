package com.sattrakk.app.data.util

import com.sattrakk.app.data.remote.dto.ErrorResponseDto
import com.sattrakk.app.data.session.SessionManager
import com.sattrakk.app.domain.model.ApiResult
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import retrofit2.Response

// Only used to decode the backend's uniform {"error": "..."} body (see ErrorResponseDto) — no
// UUID/OffsetDateTime fields involved, so it doesn't need the sattrakkSerializersModule that the
// main networking Json (di/NetworkModule.kt) is configured with.
private val errorBodyJson = Json { ignoreUnknownKeys = true }

// Every repository built on SatTrakkApi (steps 2.2/2.3+) wraps its calls with this instead of
// handling Retrofit/OkHttp exceptions itself — see ApiResult for what each outcome means.
// isSuccessful alone (not a non-null body check) decides Success, because endpoints with no
// response content (e.g. DELETE /api/notes/{id}) are declared as Response<Void> — body() is
// always null there by design, and treating that as failure would be wrong.
//
// This is a class (injected as a Singleton), not a free top-level function like in step 2.1 —
// it needs SessionManager, and every repository already follows the @Singleton/@Inject
// constructor DI pattern for its own dependencies (api/DAOs), so injecting SafeApiCaller the same
// way is minimal-disruption and consistent, rather than threading a SessionManager parameter
// through every individual safeApiCall{} call site by hand.
@Singleton
class SafeApiCaller @Inject constructor(
    private val sessionManager: SessionManager
) {

    // A 401 is this app's single point of truth for "the stored key is missing/invalid/inactive"
    // (see ApiResult.AuthRequired's doc comment) — so this is also the single point where the
    // global SessionManager learns about it. No repository needs to know SessionManager exists.
    suspend operator fun <T> invoke(call: suspend () -> Response<T>): ApiResult<T> {
        return try {
            val response = call()
            when {
                response.isSuccessful -> {
                    @Suppress("UNCHECKED_CAST")
                    ApiResult.Success(response.body() as T)
                }
                response.code() == 401 -> {
                    sessionManager.markReauthRequired()
                    ApiResult.AuthRequired
                }
                else -> ApiResult.Error(response.code(), extractErrorMessage(response))
            }
        } catch (e: IOException) {
            ApiResult.NetworkError
        }
    }

    private fun extractErrorMessage(response: Response<*>): String {
        val errorBody = response.errorBody()?.string()
        if (!errorBody.isNullOrBlank()) {
            try {
                val parsed = errorBodyJson.decodeFromString(ErrorResponseDto.serializer(), errorBody)
                if (!parsed.error.isNullOrBlank()) return parsed.error
            } catch (e: SerializationException) {
                // Not the expected {"error": "..."} shape — fall through to the generic message.
            }
        }
        return "Request failed with status ${response.code()}"
    }
}
