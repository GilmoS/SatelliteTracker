package com.sattrakk.app.ui.passdetails

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier

/** Placeholder — will show AOS/LOS/max-elevation and notes/calendar actions for one pass. */
@Composable
fun PassDetailsScreen(modifier: Modifier = Modifier) {
    Box(modifier = modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Text(text = "Pass Details", style = MaterialTheme.typography.titleLarge)
    }
}
