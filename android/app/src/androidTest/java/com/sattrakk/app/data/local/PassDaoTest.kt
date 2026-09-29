package com.sattrakk.app.data.local

import androidx.room.Room
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.sattrakk.app.data.local.entity.PassEntity
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

// Instrumented against a real in-memory Room database, not a mocked PassDao: the point is the
// Map drawer's actual SQL (getNotifyEnabled) and the notify column's actual schema, which every
// JVM repository test mocks away. See android/CLAUDE.md's notify-default fix section.
@RunWith(AndroidJUnit4::class)
class PassDaoTest {

    private lateinit var db: AppDatabase
    private lateinit var dao: PassDao

    @Before
    fun setUp() {
        db = Room.inMemoryDatabaseBuilder(
            InstrumentationRegistry.getInstrumentation().targetContext,
            AppDatabase::class.java
        ).build()
        dao = db.passDao()
    }

    @After
    fun tearDown() {
        db.close()
    }

    private fun pass(id: String, satelliteId: String, aos: Long, notify: Boolean) = PassEntity(
        id = id,
        satelliteId = satelliteId,
        tleId = "tle",
        orbitNumber = 1,
        aosEpochMillis = aos,
        losEpochMillis = aos + 600_000L,
        maxElevation = 45.0,
        aosAzimuth = 10.0,
        losAzimuth = 200.0,
        durationSec = 600,
        notify = notify,
        outlookSynced = false,
        calculatedAtEpochMillis = 0L
    )

    @Test
    fun getNotifyEnabled_returnsOnlyNotifyTrueRows_acrossSatellites_inAosOrder() = runBlocking {
        dao.replaceForSatellite("sat-a", listOf(
            pass("a-off", "sat-a", aos = 1_000L, notify = false),
            pass("a-on", "sat-a", aos = 3_000L, notify = true)
        ))
        dao.replaceForSatellite("sat-b", listOf(
            pass("b-on", "sat-b", aos = 2_000L, notify = true),
            pass("b-off", "sat-b", aos = 4_000L, notify = false)
        ))

        assertEquals(listOf("b-on", "a-on"), dao.getNotifyEnabled().map { it.id })
    }

    @Test
    fun getNotifyEnabled_isEmpty_whenNoPassWasOptedIn() = runBlocking {
        dao.replaceForSatellite("sat-a", listOf(
            pass("p1", "sat-a", aos = 1_000L, notify = false),
            pass("p2", "sat-a", aos = 2_000L, notify = false)
        ))

        assertEquals(emptyList<String>(), dao.getNotifyEnabled().map { it.id })
    }

    @Test
    fun getNotifyEnabled_followsUpdateNotify_inBothDirections() = runBlocking {
        dao.replaceForSatellite("sat-a", listOf(
            pass("p1", "sat-a", aos = 1_000L, notify = false),
            pass("p2", "sat-a", aos = 2_000L, notify = true)
        ))

        dao.updateNotify("p1", true)
        dao.updateNotify("p2", false)

        assertEquals(listOf("p1"), dao.getNotifyEnabled().map { it.id })
    }

    // Hypothesis 3 of the notify-default diagnosis: the column itself must carry no SQL default
    // that could turn a row "on" behind PassRepository's back.
    @Test
    fun notifyColumn_hasNoSqlDefaultValue() {
        db.openHelper.readableDatabase.query("PRAGMA table_info(passes)").use { cursor ->
            val nameIndex = cursor.getColumnIndexOrThrow("name")
            val defaultIndex = cursor.getColumnIndexOrThrow("dflt_value")
            var found = false
            while (cursor.moveToNext()) {
                if (cursor.getString(nameIndex) == "notify") {
                    found = true
                    assertNull(cursor.getString(defaultIndex))
                }
            }
            assertEquals(true, found)
        }
    }
}
