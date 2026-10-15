@file:OptIn(ExperimentalMaterial3Api::class)

package com.sattrakk.app.ui.skyview

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sattrakk.app.domain.model.ArCoreCompatibility
import com.sattrakk.app.navigation.OrbitIcon
import com.sattrakk.app.ui.theme.ScreenContentBottomPadding
import com.sattrakk.app.ui.theme.ScreenContentTopPadding

// Sky View groundwork (Milestone E, Step 7): shows whether this device can run the future AR
// mode. Informational only: it never opens the camera, creates an ARCore Session, or asks for any
// permission (none is needed for this check; see android/CLAUDE.md). No design source exists for
// this screen; it follows Settings' card/section-label language.
@Composable
fun SkyViewScreen(
    modifier: Modifier = Modifier,
    viewModel: SkyViewViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current

    // ON_RESUME of this nav entry fires on first entry (a newly added observer is caught up to the
    // current state), on every return to the tab, and on coming back from the Play Store, so the
    // check always reflects the device as it is now.
    LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { viewModel.checkCompatibility() }

    Scaffold(
        modifier = modifier,
        topBar = {
            Column {
                TopAppBar(title = { Text("Sky View") })
                HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
            }
        },
        // Same as every other tab: the app-root Scaffold already reserves the system insets.
        contentWindowInsets = WindowInsets(0),
    ) { innerPadding ->
        when (val current = state) {
            SkyViewUiState.Checking -> Box(
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }

            is SkyViewUiState.Result -> Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(innerPadding)
                    .verticalScroll(rememberScrollState())
                    .padding(start = 16.dp, top = ScreenContentTopPadding, end = 16.dp, bottom = ScreenContentBottomPadding),
            ) {
                SectionLabel("AR compatibility")
                StatusCard(
                    result = current,
                    onOpenArCoreListing = { openArCorePlayListing(context) },
                )
                Spacer(modifier = Modifier.height(18.dp))
                SectionLabel("Device check")
                DeviceCheckCard(current)
            }
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
private fun StatusCard(result: SkyViewUiState.Result, onOpenArCoreListing: () -> Unit) {
    val support = result.support
    val (title, body) = when (support) {
        SkyViewSupport.SUPPORTED ->
            "This device supports AR" to
                "Full AR mode, pointing your camera at the sky to follow a pass, is coming in a future update."
        SkyViewSupport.ARCORE_NOT_INSTALLED ->
            "Google Play Services for AR isn't installed" to
                "This device supports AR, but it needs Google Play Services for AR before the future AR mode can run."
        SkyViewSupport.ARCORE_TOO_OLD ->
            "Google Play Services for AR needs an update" to
                "This device supports AR, but the installed Google Play Services for AR is too old for the future AR mode."
        SkyViewSupport.UNSUPPORTED_DEVICE ->
            "This device doesn't support AR features" to
                "Sky View's AR mode won't be available on this device. Pass times and the map are unaffected."
        SkyViewSupport.UNDETERMINED ->
            "Couldn't check AR support" to
                "AR support couldn't be determined right now, often because there's no network connection. " +
                "It will be checked again next time you open this screen."
    }
    val accent = if (support == SkyViewSupport.SUPPORTED) {
        MaterialTheme.colorScheme.primary
    } else {
        MaterialTheme.colorScheme.onSurfaceVariant
    }

    Card(
        shape = MaterialTheme.shapes.large,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerLow),
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                OrbitIcon(accent)
                Column(modifier = Modifier.weight(1f)) {
                    Text(title, style = MaterialTheme.typography.bodyLarge)
                    Text(
                        body,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 2.dp),
                    )
                }
            }
            if (support == SkyViewSupport.ARCORE_NOT_INSTALLED || support == SkyViewSupport.ARCORE_TOO_OLD) {
                Spacer(modifier = Modifier.height(12.dp))
                Button(onClick = onOpenArCoreListing, modifier = Modifier.fillMaxWidth()) {
                    Text(if (support == SkyViewSupport.ARCORE_TOO_OLD) "Update in Play Store" else "Get it from Play Store")
                }
            }
        }
    }
}

@Composable
private fun DeviceCheckCard(result: SkyViewUiState.Result) {
    Card(
        shape = MaterialTheme.shapes.large,
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerLow),
    ) {
        Column(modifier = Modifier.padding(horizontal = 14.dp, vertical = 4.dp)) {
            CheckRow(
                label = "ARCore",
                value = when (result.arCoreStatus) {
                    ArCoreCompatibility.SUPPORTED -> "Supported"
                    ArCoreCompatibility.SUPPORTED_APK_NOT_INSTALLED -> "Supported, not installed"
                    ArCoreCompatibility.SUPPORTED_APK_TOO_OLD -> "Supported, update needed"
                    ArCoreCompatibility.UNSUPPORTED -> "Not supported"
                    ArCoreCompatibility.UNKNOWN -> "Unknown"
                },
                ok = result.arCoreStatus == ArCoreCompatibility.SUPPORTED,
            )
            CheckRow(
                label = "Orientation sensor",
                value = if (result.hasRotationVectorSensor) "Available" else "Not available",
                ok = result.hasRotationVectorSensor,
            )
        }
    }
}

@Composable
private fun CheckRow(label: String, value: String, ok: Boolean) {
    Row(
        modifier = Modifier.fillMaxWidth().height(48.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(label, style = MaterialTheme.typography.bodyLarge)
        Text(
            value,
            style = MaterialTheme.typography.bodySmall,
            color = if (ok) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

// Opens the "Google Play Services for AR" listing. A plain link, not ArCoreApk.requestInstall():
// that API drives ARCore's own install flow and belongs to the future AR milestone. The screen's
// ON_RESUME re-check picks up the result when the user comes back.
private fun openArCorePlayListing(context: Context) {
    val market = Intent(Intent.ACTION_VIEW, Uri.parse("market://details?id=$ARCORE_PACKAGE"))
    try {
        context.startActivity(market)
    } catch (_: ActivityNotFoundException) {
        // No Play Store app: fall back to the web listing, and do nothing if there's no browser either.
        runCatching {
            context.startActivity(
                Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=$ARCORE_PACKAGE")),
            )
        }
    }
}

private const val ARCORE_PACKAGE = "com.google.ar.core"
