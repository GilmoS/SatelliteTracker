package com.sattrakk.app.navigation

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.unit.dp

// Small hand-drawn line-art icons for the bottom navigation bar + FAB. Not sourced from the
// design's inline SVGs (see android/CLAUDE.md — reproducing those verbatim would need SVG path
// parsing that isn't worth the added complexity for chrome this simple); these are original
// glyphs at the same size/stroke-weight convention (24dp, ~1.9dp stroke) chosen to be
// recognizable and visually distinct from each other, nothing more.

private val strokeWidth = 1.9.dp

@Composable
fun HomeIcon(color: Color, modifier: Modifier = Modifier) {
    Canvas(modifier = modifier.size(24.dp)) {
        val stroke = Stroke(strokeWidth.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round)
        val w = size.width
        val h = size.height
        val roof = Path().apply {
            moveTo(w * 0.1f, h * 0.5f)
            lineTo(w * 0.5f, h * 0.12f)
            lineTo(w * 0.9f, h * 0.5f)
        }
        drawPath(roof, color, style = stroke)
        val base = Path().apply {
            moveTo(w * 0.22f, h * 0.45f)
            lineTo(w * 0.22f, h * 0.88f)
            lineTo(w * 0.78f, h * 0.88f)
            lineTo(w * 0.78f, h * 0.45f)
        }
        drawPath(base, color, style = stroke)
    }
}

@Composable
fun PassesIcon(color: Color, modifier: Modifier = Modifier) {
    Canvas(modifier = modifier.size(24.dp)) {
        val stroke = Stroke(strokeWidth.toPx(), cap = StrokeCap.Round)
        val w = size.width
        listOf(0.32f, 0.5f, 0.68f).forEach { yFrac ->
            drawLine(color, Offset(w * 0.18f, size.height * yFrac), Offset(w * 0.82f, size.height * yFrac), strokeWidth = stroke.width, cap = StrokeCap.Round)
        }
    }
}

@Composable
fun MapIcon(color: Color, modifier: Modifier = Modifier) {
    Canvas(modifier = modifier.size(24.dp)) {
        val stroke = Stroke(strokeWidth.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round)
        val outline = Path().apply {
            moveTo(size.width * 0.15f, size.height * 0.22f)
            lineTo(size.width * 0.15f, size.height * 0.82f)
            lineTo(size.width * 0.85f, size.height * 0.72f)
            lineTo(size.width * 0.85f, size.height * 0.12f)
            close()
        }
        drawPath(outline, color, style = stroke)
        drawLine(color, Offset(size.width * 0.4f, size.height * 0.18f), Offset(size.width * 0.4f, size.height * 0.78f), strokeWidth = stroke.width * 0.7f)
        drawLine(color, Offset(size.width * 0.62f, size.height * 0.16f), Offset(size.width * 0.62f, size.height * 0.76f), strokeWidth = stroke.width * 0.7f)
    }
}

// Reused for both Sky View (nav bar) and the Dashboard FAB — a small orbiting-dot glyph.
@Composable
fun OrbitIcon(color: Color, modifier: Modifier = Modifier) {
    Canvas(modifier = modifier.size(24.dp)) {
        val stroke = Stroke(strokeWidth.toPx())
        drawCircle(color, radius = size.minDimension * 0.11f, center = center)
        rotate(-28f) {
            drawOval(
                color,
                topLeft = Offset(center.x - size.width * 0.42f, center.y - size.height * 0.19f),
                size = Size(size.width * 0.84f, size.height * 0.38f),
                style = stroke,
            )
        }
    }
}

@Composable
fun SettingsIcon(color: Color, modifier: Modifier = Modifier) {
    Canvas(modifier = modifier.size(24.dp)) {
        val stroke = Stroke(strokeWidth.toPx(), cap = StrokeCap.Round)
        val rows = listOf(0.3f to 0.65f, 0.72f to 0.32f)
        rows.forEach { (yFrac, handleXFrac) ->
            val y = size.height * yFrac
            drawLine(color, Offset(size.width * 0.16f, y), Offset(size.width * 0.84f, y), strokeWidth = stroke.width)
            drawCircle(color, radius = size.minDimension * 0.1f, center = Offset(size.width * handleXFrac, y))
        }
    }
}

@Composable
fun ChevronIcon(color: Color, modifier: Modifier = Modifier.size(20.dp)) {
    Canvas(modifier = modifier) {
        val stroke = Stroke(1.8.dp.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round)
        val path = Path().apply {
            moveTo(size.width * 0.38f, size.height * 0.25f)
            lineTo(size.width * 0.66f, size.height * 0.5f)
            lineTo(size.width * 0.38f, size.height * 0.75f)
        }
        drawPath(path, color, style = stroke)
    }
}

// Top app bar back navigation, used by any screen with a back arrow (e.g. Full Pass List).
@Composable
fun BackArrowIcon(color: Color, modifier: Modifier = Modifier.size(24.dp)) {
    Canvas(modifier = modifier) {
        val stroke = Stroke(1.9.dp.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round)
        val path = Path().apply {
            moveTo(size.width * 0.62f, size.height * 0.22f)
            lineTo(size.width * 0.34f, size.height * 0.5f)
            lineTo(size.width * 0.62f, size.height * 0.78f)
        }
        drawPath(path, color, style = stroke)
        drawLine(color, Offset(size.width * 0.36f, size.height * 0.5f), Offset(size.width * 0.84f, size.height * 0.5f), strokeWidth = stroke.width, cap = StrokeCap.Round)
    }
}

// Filter button glyph (funnel), used by Full Pass List's Filter Modal entry point.
@Composable
fun FilterIcon(color: Color, modifier: Modifier = Modifier.size(22.dp)) {
    Canvas(modifier = modifier) {
        val stroke = Stroke(1.9.dp.toPx(), cap = StrokeCap.Round, join = StrokeJoin.Round)
        val path = Path().apply {
            moveTo(size.width * 0.15f, size.height * 0.2f)
            lineTo(size.width * 0.85f, size.height * 0.2f)
            lineTo(size.width * 0.58f, size.height * 0.55f)
            lineTo(size.width * 0.58f, size.height * 0.82f)
            lineTo(size.width * 0.42f, size.height * 0.7f)
            lineTo(size.width * 0.42f, size.height * 0.55f)
            close()
        }
        drawPath(path, color, style = stroke)
    }
}

// Filter Modal's header dismiss control.
@Composable
fun CloseIcon(color: Color, modifier: Modifier = Modifier.size(22.dp)) {
    Canvas(modifier = modifier) {
        val stroke = Stroke(1.9.dp.toPx(), cap = StrokeCap.Round)
        drawLine(color, Offset(size.width * 0.24f, size.height * 0.24f), Offset(size.width * 0.76f, size.height * 0.76f), strokeWidth = stroke.width, cap = StrokeCap.Round)
        drawLine(color, Offset(size.width * 0.76f, size.height * 0.24f), Offset(size.width * 0.24f, size.height * 0.76f), strokeWidth = stroke.width, cap = StrokeCap.Round)
    }
}
