package com.sattrakk.app.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

// Dark theme only, by design — no light ColorScheme exists, and system dark-mode preference is
// intentionally not read. See Color.kt for where each value below came from (the design's M3 Home
// screen) vs. where a role is left on Compose's own M3 baseline default because it isn't present
// anywhere on that screen (secondary/onSecondary, tertiaryContainer/onTertiaryContainer).
private val SatTrakkColorScheme = darkColorScheme(
    primary = Primary,
    onPrimary = OnPrimary,
    primaryContainer = PrimaryContainer,
    onPrimaryContainer = OnPrimaryContainer,
    secondaryContainer = SecondaryContainer,
    onSecondaryContainer = OnSecondaryContainer,
    tertiary = Tertiary,
    background = Background,
    onBackground = OnBackground,
    surface = Surface,
    onSurface = OnSurface,
    onSurfaceVariant = OnSurfaceVariant,
    outline = Outline,
    outlineVariant = OutlineVariant,
    surfaceContainerLow = SurfaceContainerLow,
    surfaceContainer = SurfaceContainer,
    surfaceContainerHigh = SurfaceContainerHigh,
    error = Error,
)

@Composable
fun SatTrakkTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = SatTrakkColorScheme,
        typography = SatTrakkTypography,
        shapes = SatTrakkShapes,
        content = content,
    )
}
