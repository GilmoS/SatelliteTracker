package com.sattrakk.app.ui.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Shapes
import androidx.compose.ui.unit.dp

// Corner radii observed on the design's M3 Home screen: 8dp (chips/badges), 12dp (metric cards),
// 16dp (the "next pass" card, list rows, the FAB) — exactly the stock Material 3 small/medium/
// large values, so no customization was actually needed beyond making the scale explicit.
// extraSmall/extraLarge aren't exercised by the Home screen; kept at the M3 baseline spec.
val SatTrakkShapes = Shapes(
    extraSmall = RoundedCornerShape(4.dp),
    small = RoundedCornerShape(8.dp),
    medium = RoundedCornerShape(12.dp),
    large = RoundedCornerShape(16.dp),
    extraLarge = RoundedCornerShape(28.dp),
)
