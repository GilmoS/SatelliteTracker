package com.sattrakk.app.ui.fullpasslist

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier

// Placeholder — full content (merged upcoming/history list, filters) is out of scope for this
// task; see android/CLAUDE.md. Takes the same satelliteId/satelliteName nav args
// FullPassListViewModel already reads via SavedStateHandle so the nav graph route is complete,
// but doesn't invoke the ViewModel yet.
@Composable
fun FullPassListScreen(satelliteId: String, satelliteName: String, modifier: Modifier = Modifier) {
    Box(modifier = modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Text(text = "Full Pass List — $satelliteName", style = MaterialTheme.typography.titleLarge)
    }
}
