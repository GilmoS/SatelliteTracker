package com.sattrakk.app.domain.model

// Uniform outcome type for every repository call built on top of SatTrakkApi (data/util/
// SafeApiCall.kt is what actually produces these). AuthRequired is the one case every future
// ViewModel must handle the same way: it means the stored API key is missing/invalid/inactive
// (the backend collapses all three into the same 401 — see the "ApiKey" auth scheme in the
// backend CLAUDE.md), and is this app's single point of truth for "the tester needs to
// re-register."
sealed class ApiResult<out T> {
    data class Success<T>(val data: T) : ApiResult<T>()
    data class Error(val code: Int?, val message: String) : ApiResult<Nothing>()
    object AuthRequired : ApiResult<Nothing>()
    object NetworkError : ApiResult<Nothing>()
}
