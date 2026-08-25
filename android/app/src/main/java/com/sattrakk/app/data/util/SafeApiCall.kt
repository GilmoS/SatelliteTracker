package com.sattrakk.app.data.util

import com.sattrakk.app.data.remote.dto.ErrorResponseDto
import com.sattrakk.app.domain.model.ApiResult
import java.io.IOException
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import retrofit2.Response

// Only used to decode the backend's uniform {"error": "..."} body (see ErrorResponseDto) — no
// UUID/OffsetDateTime fields involved, so it doesn't need the sattrakkSerializersModule that the
// main networking Json (di/NetworkModule.kt) is configured with.
private val errorBodyJson = Json { ignoreUnknownKeys = true }

// Every repository built on SatTrakkApi (steps 2.2/2.3) wraps its calls with this instead of
// handling Retrofit/OkHttp exceptions itself — see ApiResult for what each outcome means.
// isSuccessful alone (not a non-null body check) decides Success, because endpoints with no
// response content (e.g. DELETE /api/notes/{id}) are declared as Response<Void> — body() is
// always null there by design, and treating that as failure would be wrong.
suspend fun <T> safeApiCall(call: suspend () -> Response<T>): ApiResult<T> {
    return try {
        val response = call()
        when {
            response.isSuccessful -> {
                @Suppress("UNCHECKED_CAST")
                ApiResult.Success(response.body() as T)
            }
            response.code() == 401 -> ApiResult.AuthRequired
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
