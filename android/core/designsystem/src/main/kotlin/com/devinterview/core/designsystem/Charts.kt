package com.devinterview.core.designsystem

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.foundation.layout.*
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import kotlin.math.PI
import kotlin.math.cos
import kotlin.math.sin

data class ChartPoint(val label: String, val value: Float)

@Composable
private fun rememberEntrance(key: Any?): Float {
    val anim = remember(key) { Animatable(0f) }
    LaunchedEffect(key) { anim.animateTo(1f, tween(900, easing = FastOutSlowInEasing)) }
    return anim.value
}

/** Animated vertical bar chart with labels; tapping a bar selects it and shows its value. */
@Composable
fun BarChart(points: List<ChartPoint>, modifier: Modifier = Modifier, height: Dp = 160.dp, barColor: Color = MaterialTheme.colorScheme.primary, valueSuffix: String = "") {
    val progress = rememberEntrance(points)
    var selected by remember(points) { mutableStateOf(points.indexOfLast { it.value > 0 }.coerceAtLeast(0)) }
    val max = (points.maxOfOrNull { it.value } ?: 0f).coerceAtLeast(1f)
    val track = MaterialTheme.colorScheme.surfaceVariant
    val description = points.joinToString { "${it.label} ${it.value.toInt()}$valueSuffix" }
    Column(modifier.semantics { contentDescription = "Bar chart: $description" }) {
        if (points.isNotEmpty()) {
            Text("${points[selected].label}: ${points[selected].value.toInt()}$valueSuffix", style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(8.dp))
        }
        Row(Modifier.fillMaxWidth().height(height), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalAlignment = Alignment.Bottom) {
            points.forEachIndexed { index, p ->
                Column(Modifier.weight(1f).fillMaxHeight(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Bottom) {
                    Canvas(Modifier.fillMaxWidth().weight(1f).androidxClickable { selected = index }) {
                        drawRoundRect(track, cornerRadius = CornerRadius(12f, 12f))
                        val h = size.height * (p.value / max) * progress
                        drawRoundRect(
                            brush = Brush.verticalGradient(listOf(barColor, barColor.copy(alpha = if (index == selected) 0.85f else 0.45f))),
                            topLeft = Offset(0f, size.height - h), size = Size(size.width, h), cornerRadius = CornerRadius(12f, 12f),
                        )
                    }
                    Spacer(Modifier.height(4.dp))
                    Text(p.label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center, maxLines = 1)
                }
            }
        }
    }
}

private fun Modifier.androidxClickable(onClick: () -> Unit): Modifier =
    this.pointerInput(onClick) { detectTapGestures { onClick() } }

/** Animated line chart (e.g. mock interview score trend). */
@Composable
fun LineChart(points: List<ChartPoint>, modifier: Modifier = Modifier, height: Dp = 140.dp, color: Color = MaterialTheme.colorScheme.secondary, maxValue: Float? = null) {
    val progress = rememberEntrance(points)
    val max = maxValue ?: (points.maxOfOrNull { it.value } ?: 1f).coerceAtLeast(1f)
    val grid = MaterialTheme.colorScheme.outlineVariant
    Column(modifier.semantics { contentDescription = "Line chart: " + points.joinToString { "${it.label} ${it.value.toInt()}" } }) {
        Canvas(Modifier.fillMaxWidth().height(height)) {
            repeat(4) { i -> val y = size.height * i / 3f; drawLine(grid, Offset(0f, y), Offset(size.width, y), strokeWidth = 1f) }
            if (points.size < 2) {
                points.firstOrNull()?.let { drawCircle(color, 8f, Offset(size.width / 2, size.height * (1 - it.value / max))) }
                return@Canvas
            }
            val step = size.width / (points.size - 1)
            val path = Path()
            val area = Path()
            points.forEachIndexed { i, p ->
                val x = i * step
                val y = size.height * (1 - (p.value / max) * progress)
                if (i == 0) { path.moveTo(x, y); area.moveTo(x, size.height); area.lineTo(x, y) } else { path.lineTo(x, y); area.lineTo(x, y) }
            }
            area.lineTo(size.width, size.height); area.close()
            drawPath(area, Brush.verticalGradient(listOf(color.copy(alpha = 0.28f), Color.Transparent)))
            drawPath(path, color, style = Stroke(width = 6f, cap = StrokeCap.Round))
            points.forEachIndexed { i, p -> drawCircle(color, 7f, Offset(i * step, size.height * (1 - (p.value / max) * progress))) }
        }
        Row(Modifier.fillMaxWidth()) {
            Text(points.firstOrNull()?.label.orEmpty(), style = MaterialTheme.typography.labelSmall, modifier = Modifier.weight(1f))
            Text(points.lastOrNull()?.label.orEmpty(), style = MaterialTheme.typography.labelSmall)
        }
    }
}

/** Donut chart for correct/incorrect/skipped style breakdowns. */
@Composable
fun DonutChart(segments: List<Pair<ChartPoint, Color>>, modifier: Modifier = Modifier, size: Dp = 140.dp, centerText: String? = null) {
    val progress = rememberEntrance(segments)
    val total = segments.sumOf { it.first.value.toDouble() }.toFloat().coerceAtLeast(0.0001f)
    Row(modifier.semantics { contentDescription = segments.joinToString { "${it.first.label}: ${it.first.value.toInt()}" } }, verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.size(size), contentAlignment = Alignment.Center) {
            Canvas(Modifier.fillMaxSize()) {
                var start = -90f
                segments.forEach { (point, color) ->
                    val sweep = 360f * (point.value / total) * progress
                    drawArc(color, start, sweep, useCenter = false, style = Stroke(width = 28f, cap = StrokeCap.Butt))
                    start += sweep
                }
            }
            if (centerText != null) Text(centerText, style = MaterialTheme.typography.titleLarge)
        }
        Spacer(Modifier.width(16.dp))
        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            segments.forEach { (p, c) ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Canvas(Modifier.size(10.dp)) { drawCircle(c) }
                    Spacer(Modifier.width(6.dp))
                    Text("${p.label}: ${p.value.toInt()}", style = MaterialTheme.typography.bodySmall)
                }
            }
        }
    }
}

/** Radar chart for multi-dimensional scores (e.g. technical, communication, problem solving). Values are 0–100. */
@Composable
fun RadarChart(points: List<ChartPoint>, modifier: Modifier = Modifier, size: Dp = 220.dp, color: Color = MaterialTheme.colorScheme.primary) {
    val progress = rememberEntrance(points)
    val grid = MaterialTheme.colorScheme.outlineVariant
    val labelColor = MaterialTheme.colorScheme.onSurfaceVariant
    Box(modifier.size(size).semantics { contentDescription = "Scores: " + points.joinToString { "${it.label} ${it.value.toInt()}" } }) {
        Canvas(Modifier.fillMaxSize().padding(28.dp)) {
            if (points.size < 3) return@Canvas
            val center = Offset(this.size.width / 2, this.size.height / 2)
            val radius = minOf(this.size.width, this.size.height) / 2
            fun vertex(i: Int, r: Float): Offset {
                val angle = -PI / 2 + 2 * PI * i / points.size
                return Offset(center.x + (r * cos(angle)).toFloat(), center.y + (r * sin(angle)).toFloat())
            }
            for (ring in 1..4) {
                val p = Path()
                points.indices.forEach { i -> val v = vertex(i, radius * ring / 4); if (i == 0) p.moveTo(v.x, v.y) else p.lineTo(v.x, v.y) }
                p.close(); drawPath(p, grid, style = Stroke(1.5f))
            }
            val shape = Path()
            points.forEachIndexed { i, pt -> val v = vertex(i, radius * (pt.value / 100f) * progress); if (i == 0) shape.moveTo(v.x, v.y) else shape.lineTo(v.x, v.y) }
            shape.close()
            drawPath(shape, color.copy(alpha = 0.3f))
            drawPath(shape, color, style = Stroke(4f))
        }
        // Labels around the chart
        points.forEachIndexed { i, p ->
            val angle = -PI / 2 + 2 * PI * i / points.size
            val align = Alignment { s, space, _ ->
                val cx = space.width / 2f; val cy = space.height / 2f
                val r = minOf(space.width, space.height) / 2f - 10f
                androidx.compose.ui.unit.IntOffset((cx + r * cos(angle) - s.width / 2f).toInt(), (cy + r * sin(angle) - s.height / 2f).toInt())
            }
            Text(p.label, style = MaterialTheme.typography.labelSmall, color = labelColor, modifier = Modifier.align(align))
        }
    }
}
