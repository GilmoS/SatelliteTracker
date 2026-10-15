package com.sattrakk.app

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import com.sattrakk.app.navigation.AppViewModel
import com.sattrakk.app.navigation.SatTrakkApp
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    // Same instance SatTrakkApp's hiltViewModel() resolves (both scoped to this Activity).
    private val appViewModel: AppViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // A pass-reminder tap while the app wasn't running (or FCM's own background-shown
        // notification, which relaunches via the launcher Intent). Only on a fresh start: on a
        // recreation getIntent() is still the original launch Intent, and the NavController's
        // restored back stack already reflects that link having been handled.
        if (savedInstanceState == null) appViewModel.onLaunchIntent(intent)
        setContent {
            SatTrakkApp()
        }
    }

    // A pass-reminder tap while this (singleTop) Activity is already running.
    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        appViewModel.onLaunchIntent(intent)
    }
}
