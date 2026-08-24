package com.sattrakk.app.data.remote

import com.sattrakk.app.data.remote.dto.CreateNoteRequest
import com.sattrakk.app.data.remote.dto.CreateSatelliteRequest
import com.sattrakk.app.data.remote.dto.NoteDto
import com.sattrakk.app.data.remote.dto.NotifyStatusDto
import com.sattrakk.app.data.remote.dto.PassDto
import com.sattrakk.app.data.remote.dto.PassTrackDto
import com.sattrakk.app.data.remote.dto.PatchNotifyRequest
import com.sattrakk.app.data.remote.dto.PositionDto
import com.sattrakk.app.data.remote.dto.RegisterRequest
import com.sattrakk.app.data.remote.dto.RegisterResponse
import com.sattrakk.app.data.remote.dto.SatelliteDto
import com.sattrakk.app.data.remote.dto.ScheduleCalendarRequest
import com.sattrakk.app.data.remote.dto.SettingsDto
import com.sattrakk.app.data.remote.dto.TleDto
import com.sattrakk.app.data.remote.dto.TrackDto
import com.sattrakk.app.data.remote.dto.UpdateAlertMinutesRequest
import com.sattrakk.app.data.remote.dto.UpdateFcmTokenRequest
import com.sattrakk.app.data.remote.dto.UpdateNoteRequest
import com.sattrakk.app.data.remote.dto.UpdateSatelliteRequest
import com.sattrakk.app.data.remote.dto.UpdateSettingsRequest
import com.sattrakk.app.data.remote.dto.UserSettingsDto
import java.util.UUID
import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.PATCH
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Streaming

// Endpoint grouping and doc comments mirror the backend's controller layout (see backend
// CLAUDE.md) so a change on one side is easy to find on the other. Every method returns
// retrofit2.Response<T> — never the bare body — because data/util/SafeApiCall.kt needs the raw
// Response (status code, error body) to build a uniform ApiResult. No implementation logic here
// beyond the Retrofit annotations themselves; repositories (steps 2.2/2.3) own request/response
// handling.
interface SatTrakkApi {

    // ---- Auth ----

    // Not gated by X-Api-Key — this is the one endpoint that must work with no key stored yet.
    @POST("api/auth/register")
    suspend fun register(@Body request: RegisterRequest): Response<RegisterResponse>

    // ---- Satellites ----

    @GET("api/satellites")
    suspend fun getSatellites(): Response<List<SatelliteDto>>

    @GET("api/satellites/{id}")
    suspend fun getSatelliteById(@Path("id") id: UUID): Response<SatelliteDto>

    @POST("api/satellites")
    suspend fun createSatellite(@Body request: CreateSatelliteRequest): Response<SatelliteDto>

    @PUT("api/satellites/{id}")
    suspend fun updateSatellite(@Path("id") id: UUID, @Body request: UpdateSatelliteRequest): Response<SatelliteDto>

    // ---- Real-time (RealTimeController) — live, N2YO-backed, "now"-anchored ----

    @GET("api/satellites/{id}/position")
    suspend fun getSatellitePosition(@Path("id") id: UUID): Response<PositionDto>

    @GET("api/satellites/{id}/track")
    suspend fun getSatelliteTrack(@Path("id") id: UUID): Response<TrackDto>

    // ---- TLEs ----

    @GET("api/tles/{satelliteId}")
    suspend fun getLatestTle(@Path("satelliteId") satelliteId: UUID): Response<TleDto>

    @GET("api/tles/{satelliteId}/history")
    suspend fun getTleHistory(@Path("satelliteId") satelliteId: UUID): Response<List<TleDto>>

    @POST("api/tles/{satelliteId}/fetch")
    suspend fun fetchTle(@Path("satelliteId") satelliteId: UUID): Response<TleDto>

    // ---- Passes ----

    @GET("api/passes/{satelliteId}")
    suspend fun getUpcomingPasses(@Path("satelliteId") satelliteId: UUID): Response<List<PassDto>>

    @GET("api/passes/{satelliteId}/history")
    suspend fun getPassHistory(@Path("satelliteId") satelliteId: UUID): Response<List<PassDto>>

    @GET("api/passes/pass/{id}")
    suspend fun getPassById(@Path("id") id: UUID): Response<PassDto>

    // Fixed, TleId-anchored ground track for one already-calculated pass — distinct from
    // getSatelliteTrack above. See backend CLAUDE.md, "Two different /track endpoints."
    @GET("api/passes/{id}/track")
    suspend fun getPassTrack(@Path("id") id: UUID): Response<PassTrackDto>

    @PATCH("api/passes/{id}/notify")
    suspend fun patchPassNotify(@Path("id") id: UUID, @Body request: PatchNotifyRequest): Response<NotifyStatusDto>

    // ---- Notes ----

    @GET("api/passes/{passId}/notes")
    suspend fun getNotesForPass(@Path("passId") passId: UUID): Response<List<NoteDto>>

    @POST("api/passes/{passId}/notes")
    suspend fun addNote(@Path("passId") passId: UUID, @Body request: CreateNoteRequest): Response<NoteDto>

    @PUT("api/notes/{id}")
    suspend fun updateNote(@Path("id") id: UUID, @Body request: UpdateNoteRequest): Response<NoteDto>

    // No response body (backend returns Ok() with none) — Void, not Unit; see SafeApiCall.kt.
    @DELETE("api/notes/{id}")
    suspend fun deleteNote(@Path("id") id: UUID): Response<Void>

    // ---- Calendar ----

    // Binary .ics response, not JSON — the client opens it via ACTION_VIEW/ACTION_SEND rather
    // than parsing it, so it stays a raw ResponseBody instead of a generated DTO.
    @Streaming
    @POST("api/calendar/schedule")
    suspend fun scheduleCalendar(@Body request: ScheduleCalendarRequest): Response<ResponseBody>

    // ---- Settings (global, Milestone D-era) ----

    @GET("api/settings")
    suspend fun getSettings(): Response<SettingsDto>

    @PUT("api/settings")
    suspend fun updateSettings(@Body request: UpdateSettingsRequest): Response<SettingsDto>

    // ---- Settings/me (per-tester, Milestone E Step 1.4) ----

    @GET("api/settings/me")
    suspend fun getMySettings(): Response<UserSettingsDto>

    @PUT("api/settings/me")
    suspend fun updateMyAlertMinutes(@Body request: UpdateAlertMinutesRequest): Response<UserSettingsDto>

    @PUT("api/settings/me/fcm-token")
    suspend fun updateMyFcmToken(@Body request: UpdateFcmTokenRequest): Response<UserSettingsDto>
}
