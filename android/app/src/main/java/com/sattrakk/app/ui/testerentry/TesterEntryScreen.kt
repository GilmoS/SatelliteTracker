@file:OptIn(ExperimentalMaterial3Api::class)

package com.sattrakk.app.ui.testerentry

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle

// Beta program tester entry point, shown app-wide (in place of the nav graph) whenever
// SessionManager reports RequiresReauth -- see SatTrakkApp and android/CLAUDE.md's Tester Entry
// Screen section. Replaces the old dead-end ReauthScreen: on a 200 from AuthRepository.register(),
// TesterEntryViewModel already flips SessionManager back to Valid, so SatTrakkApp recomposes into
// MainNavHost on its own -- this screen has no explicit "navigate away" call to make.
//
// No design source exists for this screen (never part of the Claude Design mockups) -- built
// fresh in the app's existing M3 visual language: MaterialTheme.typography roles, an OutlinedTextField
// pair, a primary Button, and MaterialTheme.colorScheme.error for failure states, matching how
// SettingsScreen/FullPassListScreen already surface errors.
@Composable
fun TesterEntryScreen(
    modifier: Modifier = Modifier,
    viewModel: TesterEntryViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    // Local, screen-session-only form state -- deliberately not persisted beyond this composition
    // (no saved-draft requirement per the task scope), but rememberSaveable so a config change
    // (rotation) doesn't wipe what the tester already typed.
    var email by rememberSaveable { mutableStateOf("") }
    var displayName by rememberSaveable { mutableStateOf("") }

    val isLoading = state is TesterEntryUiState.Loading
    val canSubmit = email.isNotBlank() && email.contains("@") && displayName.isNotBlank() && !isLoading

    Scaffold(
        modifier = modifier,
        topBar = { TopAppBar(title = { Text("Beta registration") }) },
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(24.dp),
            verticalArrangement = Arrangement.Center,
        ) {
            Text(
                text = "Join the SatTrakk beta",
                style = MaterialTheme.typography.titleLarge,
            )
            Text(
                text = "Enter the email and name your team registered for the beta program.",
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 8.dp, bottom = 24.dp),
            )

            OutlinedTextField(
                value = email,
                onValueChange = { email = it },
                label = { Text("Email") },
                singleLine = true,
                enabled = !isLoading,
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Email,
                    capitalization = KeyboardCapitalization.None,
                ),
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedTextField(
                value = displayName,
                onValueChange = { displayName = it },
                label = { Text("Display name") },
                singleLine = true,
                enabled = !isLoading,
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.Words,
                ),
                modifier = Modifier.fillMaxWidth().padding(top = 12.dp),
            )

            StatusMessage(state, modifier = Modifier.padding(top = 16.dp))

            Button(
                onClick = { viewModel.register(email.trim(), displayName.trim()) },
                enabled = canSubmit,
                modifier = Modifier.fillMaxWidth().padding(top = 16.dp),
            ) {
                if (isLoading) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        color = MaterialTheme.colorScheme.onPrimary,
                        strokeWidth = 2.dp,
                    )
                } else {
                    Text("Register")
                }
            }
        }
    }
}

// The three distinct failure outcomes each get their own message -- NotAllowlisted (403) and
// AlreadyRegistered (409) are deliberately NOT the same wording (see android/CLAUDE.md): the
// former means "you're not on the beta list at all," the latter means "you're on the list, but a
// key already exists," which needs an admin's manual intervention rather than a different email.
@Composable
private fun StatusMessage(state: TesterEntryUiState, modifier: Modifier = Modifier) {
    val message = when (state) {
        TesterEntryUiState.NotAllowlisted ->
            "This email isn't on the beta tester list yet. Contact the development team to get added."
        TesterEntryUiState.AlreadyRegistered ->
            "This email already has an active registration. Contact an admin to reset it."
        is TesterEntryUiState.Error -> state.message
        TesterEntryUiState.Idle, TesterEntryUiState.Loading, TesterEntryUiState.Success -> null
    } ?: return

    Text(
        text = message,
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.error,
        textAlign = TextAlign.Start,
        modifier = modifier.fillMaxWidth(),
    )
}
