package com.sattrakk.app.data.permission

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import io.mockk.every
import io.mockk.mockk
import io.mockk.mockkStatic
import io.mockk.unmockkStatic
import org.junit.After
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

// sdkIntOverrideForTests (internal, test-only) substitutes for android.os.Build.VERSION.SDK_INT,
// which is 0 in a plain JVM unit test and can't be reassigned via reflection without fighting the
// JVM's final-field protections — see AndroidNotificationPermissionManager's doc comment.
class NotificationPermissionManagerTest {

    private val context = mockk<Context>()
    private val activity = mockk<Activity>()
    private lateinit var manager: AndroidNotificationPermissionManager

    @Before
    fun setUp() {
        mockkStatic(ContextCompat::class)
        mockkStatic(ActivityCompat::class)
        manager = AndroidNotificationPermissionManager(context)
    }

    @After
    fun tearDown() {
        unmockkStatic(ContextCompat::class)
        unmockkStatic(ActivityCompat::class)
    }

    @Test
    fun `isGranted below API 33 is always true regardless of underlying permission state`() {
        manager.sdkIntOverrideForTests = 32
        every { ContextCompat.checkSelfPermission(any(), any()) } returns PackageManager.PERMISSION_DENIED

        assertTrue(manager.isGranted())
    }

    @Test
    fun `isGranted on API 33+ reflects checkSelfPermission granted`() {
        manager.sdkIntOverrideForTests = 33
        every {
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS)
        } returns PackageManager.PERMISSION_GRANTED

        assertTrue(manager.isGranted())
    }

    @Test
    fun `isGranted on API 33+ reflects checkSelfPermission denied`() {
        manager.sdkIntOverrideForTests = 33
        every {
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS)
        } returns PackageManager.PERMISSION_DENIED

        assertFalse(manager.isGranted())
    }

    @Test
    fun `shouldShowRationale below API 33 is always false`() {
        manager.sdkIntOverrideForTests = 32
        every { ActivityCompat.shouldShowRequestPermissionRationale(any(), any()) } returns true

        assertFalse(manager.shouldShowRationale(activity))
    }

    @Test
    fun `shouldShowRationale on API 33+ reflects ActivityCompat true`() {
        manager.sdkIntOverrideForTests = 33
        every {
            ActivityCompat.shouldShowRequestPermissionRationale(activity, Manifest.permission.POST_NOTIFICATIONS)
        } returns true

        assertTrue(manager.shouldShowRationale(activity))
    }

    @Test
    fun `shouldShowRationale on API 33+ reflects ActivityCompat false`() {
        manager.sdkIntOverrideForTests = 33
        every {
            ActivityCompat.shouldShowRequestPermissionRationale(activity, Manifest.permission.POST_NOTIFICATIONS)
        } returns false

        assertFalse(manager.shouldShowRationale(activity))
    }
}
