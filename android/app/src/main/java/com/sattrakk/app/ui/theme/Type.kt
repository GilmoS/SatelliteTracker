package com.sattrakk.app.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

// Font families per the design (Google Fonts link in "SatelliteTracker M3.dc.html"): Roboto for
// UI text, JetBrains Mono for numeric telemetry (pass times, az/el, countdowns).
//
// No .ttf files are bundled yet. FontFamily.Default already resolves to Roboto on stock
// Android/AOSP, so the UI text family needs no fallback caveat; FontFamily.Monospace does NOT
// resolve to JetBrains Mono (it's Droid Sans Mono / Roboto Mono depending on device) and stays a
// placeholder until that font asset is added under res/font/.
val RobotoFontFamily = FontFamily.Default
val MonoFontFamily = FontFamily.Monospace

// Values below are read from the design's "M3 Home" screen inline styles. Sizes/weights follow
// the M3 baseline scale where the design matches it (labelLarge, titleSmall, labelMedium,
// bodySmall); labelSmall deviates from the M3 stock 11sp to the design's actual 10sp (metric-card
// labels) rather than forcing the nearest stock value.
val SatTrakkTypography = Typography(
    bodyLarge = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 16.sp,
        lineHeight = 24.sp,
    ),
    bodySmall = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 12.sp,
        lineHeight = 16.sp,
    ),
    titleLarge = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Normal,
        fontSize = 22.sp,
        lineHeight = 28.sp,
    ),
    titleSmall = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp,
    ),
    labelLarge = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Medium,
        fontSize = 14.sp,
        lineHeight = 20.sp,
    ),
    labelMedium = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Medium,
        fontSize = 12.sp,
        lineHeight = 16.sp,
    ),
    labelSmall = TextStyle(
        fontFamily = RobotoFontFamily,
        fontWeight = FontWeight.Medium,
        fontSize = 10.sp,
        lineHeight = 14.sp,
    ),
)

// Not part of Material3's Typography scale — used explicitly for numeric telemetry surfaces (pass
// tables, AR overlay readouts) rather than MaterialTheme.typography. This is a base style only:
// the design uses this family/weight at several different sizes depending on context (e.g. 32sp
// for the hero countdown vs. 14-15sp for metric/list values) — call sites `.copy(fontSize = ...)`
// this rather than the type scale growing a same-family entry per pixel size.
val TelemetryTextStyle = TextStyle(
    fontFamily = MonoFontFamily,
    fontWeight = FontWeight.Medium,
    fontSize = 16.sp,
    lineHeight = 22.sp,
)
