package com.sattrakk.app.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

// Dark theme only, by design — no light ColorScheme exists, and system dark-mode
// preference is intentionally not read. Mission Control has no light mode yet.
private val MissionControlColorScheme = darkColorScheme(
    primary = ElectricBlue,
    secondary = Amber,
    background = NearBlack,
    surface = SurfaceDark,
    surfaceVariant = SurfaceVariantDark,
    onPrimary = NearBlack,
    onSecondary = NearBlack,
    onBackground = OnSurfaceLight,
    onSurface = OnSurfaceLight,
    onSurfaceVariant = OnSurfaceMuted,
    error = ErrorRed,
)

@Composable
fun SatTrakkTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MissionControlColorScheme,
        typography = SatTrakkTypography,
        content = content,
    )
}
