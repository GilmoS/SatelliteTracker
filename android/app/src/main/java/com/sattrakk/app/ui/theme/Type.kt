package com.sattrakk.app.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

// Font families per the Figma spec: JetBrains Mono / Space Mono for numeric
// telemetry (pass times, az/el, countdowns), Inter for everything else.
//
// No .ttf files are bundled yet — these fall back to the platform monospace
// and default sans-serif families until the real font assets are added under
// res/font/. Swap the FontFamily.Default calls below for FontFamily(Font(...))
// once those assets land.
val InterFontFamily = FontFamily.Default
val MonoFontFamily = FontFamily.Monospace

val SatTrakkTypography = Typography(
    bodyLarge = TextStyle(
        fontFamily = InterFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 16.sp,
        lineHeight = 24.sp,
    ),
    titleLarge = TextStyle(
        fontFamily = InterFontFamily,
        fontWeight = FontWeight.SemiBold,
        fontSize = 22.sp,
        lineHeight = 28.sp,
    ),
    labelLarge = TextStyle(
        fontFamily = InterFontFamily,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp,
    ),
)

// Not part of Material3's Typography scale — used explicitly for numeric
// telemetry surfaces (pass tables, AR overlay readouts) rather than
// MaterialTheme.typography.
val TelemetryTextStyle = TextStyle(
    fontFamily = MonoFontFamily,
    fontWeight = FontWeight.Medium,
    fontSize = 16.sp,
    lineHeight = 22.sp,
)
