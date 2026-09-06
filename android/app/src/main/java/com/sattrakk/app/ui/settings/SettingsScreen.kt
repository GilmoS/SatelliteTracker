@file:OptIn(ExperimentalMaterial3Api::class)

package com.sattrakk.app.ui.settings

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.provider.Settings as AndroidProviderSettings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.navigation.BellIcon
import com.sattrakk.app.navigation.PlusIcon

// Real Settings screen content, replacing the one-line placeholder. Wired to the already-complete
// SettingsViewModel/SettingsUiState (built in a prior task — see android/CLAUDE.md's Settings
// screen section) per the code truth map's Screen 8/8 verdicts and this task's confirmed
// decisions. Three design elements are omitted outright ("Minimum elevation" pass-filter slider,
// "Outlook integration" card, and the raw design's toggle-fill visuals replaced by a standard M3
// Switch) — see android/CLAUDE.md for why each was omitted. The push-notification-permission
// section below "Alert timing" has NO source design element at all; it's original UI built to
// match this app's existing M3 visual language (see android/CLAUDE.md).
@Composable
fun SettingsScreen(
    modifier: Modifier = Modifier,
    viewModel: SettingsViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val activity = remember(context) { context as Activity }
    val lifecycleOwner = LocalLifecycleOwner.current
    val snackbarHostState = remember { SnackbarHostState() }

    val notificationPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) {
        // Refresh regardless of the callback's own granted flag -- refreshPermissionStatus() is
        // the one place that reads both isGranted() and shouldShowRationale() consistently.
        viewModel.refreshPermissionStatus(activity)
    }

    // Permission state can change externally while the app is backgrounded (the user grants it
    // from system settings and returns) -- per this task's confirmed decision #4, re-read it on
    // resume. An initial read also runs once up front, since a fresh navigation to this screen
    // doesn't necessarily produce its own ON_RESUME (the Activity may already be resumed).
    LaunchedEffect(Unit) { viewModel.refreshPermissionStatus(activity) }
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                viewModel.refreshPermissionStatus(activity)
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    // "Add satellite" is a real, tappable row (per this task's confirmed decision #1, never
    // rendered disabled) -- tapping it calls the existing addSatellite() stub, which only sets
    // stubMessage; surfaced here as a transient snackbar, then immediately consumed so it doesn't
    // reappear on a later recomposition/config change.
    LaunchedEffect(state.stubMessage) {
        val message = state.stubMessage ?: return@LaunchedEffect
        snackbarHostState.showSnackbar(message)
        viewModel.consumeStubMessage()
    }

    Scaffold(
        modifier = modifier,
        topBar = { TopAppBar(title = { Text("Settings") }) },
        snackbarHost = {
            SnackbarHost(snackbarHostState) { data ->
                Snackbar(snackbarData = data)
            }
        },
    ) { innerPadding ->
        if (state.isLoading) {
            Box(modifier = Modifier.fillMaxSize().padding(innerPadding), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        LazyColumn(
            modifier = Modifier.fillMaxSize().padding(innerPadding),
            contentPadding = PaddingValues(16.dp, 8.dp, 16.dp, 24.dp),
        ) {
            state.error?.let { error ->
                item {
                    Text(
                        text = error,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.fillMaxWidth().padding(bottom = 12.dp),
                    )
                }
            }

            item { SectionLabel("Satellites") }
            item {
                SatellitesCard(
                    satellites = state.satellites,
                    onToggleVisibility = viewModel::toggleSatelliteVisibility,
                    onAddSatellite = viewModel::addSatellite,
                )
            }
            item { Spacer(modifier = Modifier.height(18.dp)) }

            item { SectionLabel("Alert timing") }
            item {
                AlertTimingChips(
                    alertMinutes = state.alertMinutes,
                    onUpdateAlertMinutes = viewModel::updateAlertMinutes,
                )
            }
            item { Spacer(modifier = Modifier.height(18.dp)) }

            // New section -- no corresponding element in the source design (see android/CLAUDE.md).
            item { SectionLabel("Notifications") }
            item {
                PermissionStatusCard(
                    granted = state.pushPermissionGranted,
                    shouldShowRationale = state.pushPermissionShouldShowRationale,
                    onRequestPermission = {
                        notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                    },
                    onOpenAppSettings = {
                        val intent = Intent(AndroidProviderSettings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
                            data = Uri.fromParts("package", activity.packageName, null)
                        }
                        activity.startActivity(intent)
                    },
                )
            }
            item { Spacer(modifier = Modifier.height(18.dp)) }

            item { SectionLabel("About") }
            item { AboutRow() }
        }
    }
}

@Composable
private fun SectionLabel(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.titleSmall,
        color = MaterialTheme.colorScheme.primary,
        modifier = Modifier.padding(bottom = 6.dp),
    )
}

@Composable
private fun SatellitesCard(
    satellites: List<SatelliteVisibility>,
    onToggleVisibility: (String) -> Unit,
    onAddSatellite: () -> Unit,
) {
    Card(
        shape = MaterialTheme.shapes.large,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerLow),
    ) {
        Column {
            // A divider follows every satellite row, including the last one -- matching the
            // design, which shows one between EROS C3/RUNNER-1 and again before "Add satellite".
            satellites.forEach { satellite ->
                SatelliteRow(satellite, onToggleVisibility)
                RowDivider()
            }
            AddSatelliteRow(onClick = onAddSatellite)
        }
    }
}

@Composable
private fun SatelliteRow(satellite: SatelliteVisibility, onToggleVisibility: (String) -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().height(58.dp).padding(horizontal = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Box(
            modifier = Modifier.size(40.dp).background(MaterialTheme.colorScheme.secondaryContainer, CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = avatarInitials(satellite.satelliteName),
                style = MaterialTheme.typography.labelLarge,
                color = MaterialTheme.colorScheme.onSecondaryContainer,
            )
        }
        Column(modifier = Modifier.weight(1f)) {
            Text(satellite.satelliteName, style = MaterialTheme.typography.bodyLarge)
            Text(
                "NORAD ${satellite.noradId}",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 2.dp),
            )
        }
        // Switch checked = visible (i.e. !isHidden) -- the design's toggle is ON for a shown
        // satellite; hiding is the opt-out state (HiddenSatellitesStore), not the default.
        Switch(checked = !satellite.isHidden, onCheckedChange = { onToggleVisibility(satellite.satelliteId) })
    }
}

@Composable
private fun AddSatelliteRow(onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(50.dp)
            .clickable(onClick = onClick)
            .padding(horizontal = 14.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        PlusIcon(MaterialTheme.colorScheme.primary)
        Text(
            "Add satellite",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.primary,
        )
    }
}

@Composable
private fun RowDivider() {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 14.dp)
            .height(1.dp)
            .background(MaterialTheme.colorScheme.surfaceContainerHigh),
    )
}

private val AlertMinuteOptions = listOf(5, 10, 15, 30, 60)

@Composable
private fun AlertTimingChips(alertMinutes: Set<Int>, onUpdateAlertMinutes: (Set<Int>) -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        AlertMinuteOptions.forEach { minute ->
            val selected = alertMinutes.contains(minute)
            FilterChip(
                selected = selected,
                onClick = {
                    val updated = if (selected) alertMinutes - minute else alertMinutes + minute
                    onUpdateAlertMinutes(updated)
                },
                label = { Text("${minute}m") },
                modifier = Modifier.weight(1f),
            )
        }
    }
}

@Composable
private fun PermissionStatusCard(
    granted: Boolean,
    shouldShowRationale: Boolean,
    onRequestPermission: () -> Unit,
    onOpenAppSettings: () -> Unit,
) {
    Card(
        shape = MaterialTheme.shapes.large,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerLow),
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                BellIcon(if (granted) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant)
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        if (granted) "Push notifications enabled" else "Push notifications not enabled",
                        style = MaterialTheme.typography.bodyLarge,
                    )
                    Text(
                        if (granted) {
                            "Alerts for the timings below will be delivered to this device."
                        } else {
                            "Enable notifications to receive pass alerts on this device."
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 2.dp),
                    )
                }
            }
            if (!granted) {
                Spacer(modifier = Modifier.height(12.dp))
                if (shouldShowRationale) {
                    // Never asked yet, or denied once but not permanently -- the system dialog can
                    // still be shown again.
                    Button(onClick = onRequestPermission, modifier = Modifier.fillMaxWidth()) {
                        Text("Enable notifications")
                    }
                } else {
                    // Permanently denied -- the in-app system dialog won't reappear, so route to
                    // system app settings instead. Per this task's confirmed decision #4, this
                    // branch is driven purely by shouldShowRationale == false, which Android's own
                    // API also returns for "never asked yet" -- an accepted, platform-inherent
                    // ambiguity, not a bug (see android/CLAUDE.md).
                    Button(onClick = onOpenAppSettings, modifier = Modifier.fillMaxWidth()) {
                        Text("Open app settings")
                    }
                }
            }
        }
    }
}

@Composable
private fun AboutRow() {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text("Version 1.0", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(
            "Orbital data · N2YO.com",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

private fun avatarInitials(satelliteName: String): String =
    satelliteName.trim().filter { it.isLetterOrDigit() }.take(2).uppercase().ifEmpty { "?" }
