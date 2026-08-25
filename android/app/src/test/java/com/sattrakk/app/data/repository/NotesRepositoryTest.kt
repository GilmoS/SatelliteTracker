package com.sattrakk.app.data.repository

import com.sattrakk.app.data.local.CacheMetadataDao
import com.sattrakk.app.data.local.NoteDao
import com.sattrakk.app.data.local.entity.CacheMetadataEntity
import com.sattrakk.app.data.local.entity.NoteEntity
import com.sattrakk.app.data.remote.SatTrakkApi
import com.sattrakk.app.data.remote.dto.NoteDto
import com.sattrakk.app.domain.model.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.io.IOException
import java.time.OffsetDateTime
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

class NotesRepositoryTest {

    private val api = mockk<SatTrakkApi>()
    private val noteDao = mockk<NoteDao>(relaxUnitFun = true)
    private val cacheMetadataDao = mockk<CacheMetadataDao>(relaxUnitFun = true)
    private lateinit var repository: NotesRepository

    private val passId = UUID.randomUUID()
    private val noteId = UUID.randomUUID()
    private val cacheKey = "notes:$passId"

    private val cachedEntity = NoteEntity(
        id = noteId.toString(),
        passId = passId.toString(),
        content = "Cached note",
        createdAtEpochMillis = 1_000L,
        updatedAtEpochMillis = 1_000L
    )

    private fun noteDto(content: String = "Fresh note") = NoteDto(
        id = noteId,
        passId = passId,
        content = content,
        createdAt = OffsetDateTime.now(),
        updatedAt = OffsetDateTime.now()
    )

    @Before
    fun setUp() {
        repository = NotesRepository(api, noteDao, cacheMetadataDao)
    }

    @Test
    fun `fresh cache returns cached data without calling network`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns CacheMetadataEntity(cacheKey, System.currentTimeMillis())
        coEvery { noteDao.getCachedForPass(passId.toString()) } returns listOf(cachedEntity)

        val result = repository.getNotes(passId.toString())

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
        coVerify(exactly = 0) { api.getNotesForPass(any()) }
    }

    @Test
    fun `stale cache with network success refreshes cache and returns fresh data`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        coEvery { api.getNotesForPass(passId) } returns Response.success(listOf(noteDto()))

        val result = repository.getNotes(passId.toString())

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { api.getNotesForPass(passId) }
        coVerify(exactly = 1) { noteDao.replaceForPass(passId.toString(), any()) }
        coVerify(exactly = 1) { cacheMetadataDao.upsert(any()) }
    }

    @Test
    fun `missing cache, network fails with NetworkError, propagates NetworkError`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns null
        coEvery { api.getNotesForPass(passId) } throws IOException("offline")

        val result = repository.getNotes(passId.toString())

        assertTrue(result is ApiResult.NetworkError)
    }

    @Test
    fun `stale cache, network fails with NetworkError, Room has data, falls back to stale cache`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        coEvery { noteDao.getCachedForPass(passId.toString()) } returns listOf(cachedEntity)
        coEvery { api.getNotesForPass(passId) } throws IOException("offline")

        val result = repository.getNotes(passId.toString())

        assertTrue(result is ApiResult.Success)
        assertEquals(1, (result as ApiResult.Success).data.size)
    }

    @Test
    fun `network fails with AuthRequired, propagates AuthRequired even when stale cache exists`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns
            CacheMetadataEntity(cacheKey, System.currentTimeMillis() - TimeUnit.HOURS.toMillis(2))
        coEvery { api.getNotesForPass(passId) } returns
            Response.error(401, "".toResponseBody("application/json".toMediaType()))

        val result = repository.getNotes(passId.toString())

        assertTrue(result is ApiResult.AuthRequired)
    }

    @Test
    fun `forceRefresh true skips fresh cache and calls network`() = runTest {
        coEvery { cacheMetadataDao.get(cacheKey) } returns CacheMetadataEntity(cacheKey, System.currentTimeMillis())
        coEvery { api.getNotesForPass(passId) } returns Response.success(listOf(noteDto()))

        repository.getNotes(passId.toString(), forceRefresh = true)

        coVerify(exactly = 1) { api.getNotesForPass(passId) }
    }

    @Test
    fun `createNote success updates the local notes cache immediately`() = runTest {
        coEvery { api.addNote(passId, any()) } returns Response.success(noteDto())

        val result = repository.createNote(passId.toString(), "Fresh note")

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { noteDao.insert(any()) }
    }

    @Test
    fun `createNote failure returns the failure with no Room fallback and no cache mutation`() = runTest {
        coEvery { api.addNote(passId, any()) } throws IOException("offline")

        val result = repository.createNote(passId.toString(), "Fresh note")

        assertTrue(result is ApiResult.NetworkError)
        coVerify(exactly = 0) { noteDao.insert(any()) }
    }

    @Test
    fun `updateNote success updates the local notes cache immediately`() = runTest {
        coEvery { api.updateNote(noteId, any()) } returns Response.success(noteDto(content = "Edited"))

        val result = repository.updateNote(noteId.toString(), "Edited")

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { noteDao.insert(any()) }
    }

    @Test
    fun `updateNote failure returns the failure with no Room fallback`() = runTest {
        val errorBody = """{"error":"not found"}""".toResponseBody("application/json".toMediaType())
        coEvery { api.updateNote(noteId, any()) } returns Response.error(404, errorBody)

        val result = repository.updateNote(noteId.toString(), "Edited")

        assertTrue(result is ApiResult.Error)
        coVerify(exactly = 0) { noteDao.insert(any()) }
    }

    @Test
    fun `deleteNote success removes the row from the local notes cache immediately`() = runTest {
        coEvery { api.deleteNote(noteId) } returns Response.success(null)

        val result = repository.deleteNote(noteId.toString())

        assertTrue(result is ApiResult.Success)
        coVerify(exactly = 1) { noteDao.deleteById(noteId.toString()) }
    }

    @Test
    fun `deleteNote failure returns the failure with no Room fallback`() = runTest {
        coEvery { api.deleteNote(noteId) } throws IOException("offline")

        val result = repository.deleteNote(noteId.toString())

        assertTrue(result is ApiResult.NetworkError)
        coVerify(exactly = 0) { noteDao.deleteById(any()) }
    }
}
