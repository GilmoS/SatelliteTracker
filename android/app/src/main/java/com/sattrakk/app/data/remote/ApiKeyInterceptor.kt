package com.sattrakk.app.data.remote

import com.sattrakk.app.data.local.ApiKeyStore
import javax.inject.Inject
import okhttp3.Interceptor
import okhttp3.Response

// Unconditional by design: no per-endpoint conditional logic. Anonymous GETs tolerate an
// X-Api-Key header harmlessly (the backend only reads it on [Authorize]d actions), and
// POST /api/auth/register is the one endpoint that must keep working with no key stored yet —
// both cases are already covered by "add the header only when a key exists."
class ApiKeyInterceptor @Inject constructor(private val apiKeyStore: ApiKeyStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val apiKey = apiKeyStore.getKey() ?: return chain.proceed(request)

        val authenticatedRequest = request.newBuilder().header("X-Api-Key", apiKey).build()

        return chain.proceed(authenticatedRequest)
    }
}
