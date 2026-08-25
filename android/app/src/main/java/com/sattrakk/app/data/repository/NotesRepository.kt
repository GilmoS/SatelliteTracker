package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.NoteDao
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.CreateNoteRequest
import com.sattrakk.app.data.remote.dto.UpdateNoteRequest
import com.sattrakk.app.data.util.cachedNetworkFirst
import com.sattrakk.app.data.util.safeApiCall
import com.sattrakk.app.domain.mapper.toDomain
import com.sattrakk.app.domain.mapper.toEntity
import com.sattrakk.app.domain.model.ApiResult
import com.sattrakk.app.domain.model.Note
import com.sattrakk.app.domain.model.mapSuccess
import java.util.UUID
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

// Reads follow the same TTL-gated, offline-capable caching strategy as Pass/Satellite. Writes
// (create/update/delete) are the deliberate asymmetry: notes are user-editable, not purely
// server-computed, so a write with no connectivity is NOT queued for later — it fails outright
// with NetworkError, same as any other repository failure, and the (future) UI is expected to
// surface that as "requires connection." See android/CLAUDE.md.
@Singleton
class NotesRepository @Inject constructor(
    private val api: SatTrakkApi,
    private val noteDao: NoteDao,
    private val cacheMetadataDao: CacheMetadataDao
) {

    suspend fun getNotes(passId: String, forceRefresh: Boolean = false): ApiResult<List<Note>> =
        cachedNetworkFirst(
            cacheKey = cacheKeyForPass(passId),
            ttlMillis = NOTES_TTL_MILLIS,
            forceRefresh = forceRefresh,
            metadataDao = cacheMetadataDao,
            readCache = { noteDao.getCachedForPass(passId).map { it.toDomain() } },
            fetchNetwork = {
                safeApiCall { api.getNotesForPass(UUID.fromString(passId)) }
                    .mapSuccess { dtos -> dtos.map { it.toDomain() } }
            },
            writeCache = { notes -> noteDao.replaceForPass(passId, notes.map { it.toEntity() }) }
        )

    // No offline support: on any non-Success result (including NetworkError) the local cache is
    // left untouched and the failure is returned as-is.
    suspend fun createNote(passId: String, content: String): ApiResult<Note> {
        val result = safeApiCall {
            api.addNote(UUID.fromString(passId), CreateNoteRequest(content = content))
        }
        if (result is ApiResult.Success) {
            noteDao.insert(result.data.toDomain().toEntity())
        }
        return result.mapSuccess { it.toDomain() }
    }

    suspend fun updateNote(noteId: String, content: String): ApiResult<Note> {
        val result = safeApiCall {
            api.updateNote(UUID.fromString(noteId), UpdateNoteRequest(content = content))
        }
        if (result is ApiResult.Success) {
            // REPLACE-on-conflict by primary key id doubles as an upsert here, same as
            // createNote — the affected row is either new or already present, either way this
            // leaves the cache consistent with what the server just confirmed.
            noteDao.insert(result.data.toDomain().toEntity())
        }
        return result.mapSuccess { it.toDomain() }
    }

    suspend fun deleteNote(noteId: String): ApiResult<Unit> {
        val result = safeApiCall { api.deleteNote(UUID.fromString(noteId)) }
        if (result is ApiResult.Success) {
            noteDao.deleteById(noteId)
        }
        return result.mapSuccess { }
    }

    private companion object {
        val NOTES_TTL_MILLIS = TimeUnit.HOURS.toMillis(1)
        fun cacheKeyForPass(passId: String) = "notes:$passId"
    }
}
