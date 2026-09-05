package com.sattrakk.app.ui.reauth

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp

// Shown app-wide, in place of the nav graph, whenever SessionManager reports RequiresReauth (see
// SatTrakkApp). Not in the design — a minimal informational dead end. There is no self-service
// re-registration flow (that's a future step; see android/CLAUDE.md's SessionManager section), so
// this screen's only job is to explain what happened and stop, not to offer a retry action that
// doesn't exist yet.
@Composable
fun ReauthScreen(modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(32.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Text(
            text = "Re-registration required",
            style = MaterialTheme.typography.titleLarge,
            textAlign = TextAlign.Center,
        )
        Text(
            text = "Your API key is no longer valid. Contact the dev team to get re-registered as a beta tester.",
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(top = 12.dp),
        )
    }
}
