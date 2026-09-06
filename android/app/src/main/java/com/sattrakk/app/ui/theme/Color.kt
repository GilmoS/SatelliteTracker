package com.sattrakk.app.ui.theme

import androidx.compose.ui.graphics.Color

// Extracted from the Claude Design project "Map detail and AR improvements"
// (claude.ai/design/p/fb57c4cc-1710-43cf-8246-39cc22b4dc34), file "SatelliteTracker M3.dc.html",
// the "2a" option — Material 3 baseline (not 2b/Expressive) — via the design MCP, reading the
// "M3 Home" screen's inline styles. Dark scheme only; the design has no light variant. Replaces
// the earlier placeholder "Mission Control" palette that predates any design-file access.
//
// Roles not present anywhere on the Home screen (secondary/onSecondary, tertiaryContainer/
// onTertiaryContainer, error/onError) are NOT guessed from this screen — see Theme.kt for which
// of those keep Compose's M3 baseline defaults vs. carry over the prior placeholder value, and
// android/CLAUDE.md for the full breakdown.

val Primary = Color(0xFF7FD1F7)
val OnPrimary = Color(0xFF003548)
val PrimaryContainer = Color(0xFF004D66)
val OnPrimaryContainer = Color(0xFFC4E9FF)

// Only ever observed as a "container" tone (selected list row, active nav-bar pill) — no bare
// `secondary` surface exists on the Home screen to sample.
val SecondaryContainer = Color(0xFF354A54)
val OnSecondaryContainer = Color(0xFFD1E6F0)
// A dimmer variant of OnSecondaryContainer used for the secondary text/chevron on a highlighted
// (next-pass) row — a real, deliberately-used tone in the design that doesn't correspond to a
// named M3 ColorScheme role, so it's kept as a standalone constant rather than forced into one.
val OnSecondaryContainerVariant = Color(0xFFB4CAD6)

// Amber accent — used for the (decorative, omitted) notification badge and alert-chip icon.
// Captured for future screens even though nothing in this task's scope renders it.
val Tertiary = Color(0xFFFFB955)

val Background = Color(0xFF0F1417)
val OnBackground = Color(0xFFDFE3E7)
val Surface = Color(0xFF0F1417)
val OnSurface = Color(0xFFDFE3E7)
val OnSurfaceVariant = Color(0xFFBFC8CD)
val Outline = Color(0xFF89939A)
val OutlineVariant = Color(0xFF3F484D)

// Three tonal-elevation tiers actually used on the Home screen, darkest to lightest: the bottom
// navigation bar, the elevated "next pass" card, then the metric cards on top of it.
val SurfaceContainerLow = Color(0xFF171C1F)
val SurfaceContainer = Color(0xFF1B2125)
val SurfaceContainerHigh = Color(0xFF262B2F)

// Not present anywhere on the Home screen — carried over from the pre-design placeholder palette
// since no in-scope screen exercises an error state. Revisit once a screen that surfaces one
// (e.g. Settings, Pass Filter) is built against the design.
val Error = Color(0xFFEF5350)
