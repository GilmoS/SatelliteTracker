package com.sattrakk.app.data.push

import android.content.Intent
import android.net.Uri
import com.sattrakk.app.navigation.SatTrakkDestination
import io.mockk.every
import io.mockk.mockk
import io.mockk.mockkStatic
import io.mockk.unmockkStatic
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PassNotificationDeepLinkTest {

    private val passId = "3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b"

    private fun extras(vararg pairs: Pair<String, String>): (String) -> String? = mapOf(*pairs)::get

    @After
    fun tearDown() {
        unmockkStatic(Uri::class)
    }

    @Test
    fun `pass reminder extras yield the passId`() {
        val result = PassNotificationDeepLink.passIdFrom(extras("type" to "pass_reminder", "passId" to passId))

        assertEquals(passId, result)
    }

    @Test
    fun `missing passId yields null`() {
        assertNull(PassNotificationDeepLink.passIdFrom(extras("type" to "pass_reminder")))
    }

    @Test
    fun `blank passId yields null`() {
        assertNull(PassNotificationDeepLink.passIdFrom(extras("type" to "pass_reminder", "passId" to "  ")))
    }

    @Test
    fun `malformed passId yields null`() {
        assertNull(PassNotificationDeepLink.passIdFrom(extras("type" to "pass_reminder", "passId" to "not-a-guid")))
    }

    @Test
    fun `unknown type yields null`() {
        assertNull(PassNotificationDeepLink.passIdFrom(extras("type" to "something_else", "passId" to passId)))
    }

    @Test
    fun `missing type yields null`() {
        assertNull(PassNotificationDeepLink.passIdFrom(extras("passId" to passId)))
    }

    @Test
    fun `plain launcher intent with no extras yields null`() {
        assertNull(PassNotificationDeepLink.passIdFrom(extras()))
        assertNull(PassNotificationDeepLink.passIdFrom(null as Intent?))
    }

    @Test
    fun `Intent overload reads the same extras`() {
        val intent = mockk<Intent>()
        every { intent.getStringExtra("type") } returns "pass_reminder"
        every { intent.getStringExtra("passId") } returns passId

        assertEquals(passId, PassNotificationDeepLink.passIdFrom(intent))
    }

    // android.net.Uri is a stub on the JVM unit-test classpath, so Uri.encode is replaced with an
    // identity function (a GUID has no characters it would encode anyway).
    @Test
    fun `parsed passId builds the Pass Details route`() {
        mockkStatic(Uri::class)
        every { Uri.encode(any()) } answers { firstArg() }
        val parsed = PassNotificationDeepLink.passIdFrom(extras("type" to "pass_reminder", "passId" to passId))!!

        assertEquals("pass_details/$passId", SatTrakkDestination.PassDetails.buildRoute(parsed))
    }
}
