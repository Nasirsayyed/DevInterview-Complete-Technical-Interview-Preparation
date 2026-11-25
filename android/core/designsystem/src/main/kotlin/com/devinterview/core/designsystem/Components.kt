package com.devinterview.core.designsystem

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.*
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.semantics.*
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.devinterview.domain.model.AppError

// ---------------- Buttons ----------------
@Composable
fun PrimaryButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true, loading: Boolean = false, icon: ImageVector? = null) {
    val haptics = LocalHapticFeedback.current
    Button(
        onClick = { haptics.performHapticFeedback(HapticFeedbackType.TextHandleMove); onClick() },
        enabled = enabled && !loading,
        modifier = modifier.heightIn(min = 52.dp),
        shape = MaterialTheme.shapes.medium,
        contentPadding = PaddingValues(horizontal = 20.dp, vertical = 12.dp),
    ) {
        if (loading) {
            CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp, color = MaterialTheme.colorScheme.onPrimary)
            Spacer(Modifier.width(10.dp))
        } else if (icon != null) {
            Icon(icon, contentDescription = null, Modifier.size(20.dp))
            Spacer(Modifier.width(8.dp))
        }
        Text(text, style = MaterialTheme.typography.labelLarge)
    }
}

@Composable
fun SecondaryButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true, icon: ImageVector? = null) {
    OutlinedButton(onClick = onClick, enabled = enabled, modifier = modifier.heightIn(min = 52.dp), shape = MaterialTheme.shapes.medium) {
        if (icon != null) { Icon(icon, null, Modifier.size(20.dp)); Spacer(Modifier.width(8.dp)) }
        Text(text, style = MaterialTheme.typography.labelLarge)
    }
}

/** Gradient call-to-action used for premium upsells and hero actions. */
@Composable
fun GradientButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, brush: Brush = DiTheme.colors.heroGradient, icon: ImageVector? = null) {
    Box(
        modifier
            .heightIn(min = 52.dp)
            .clip(MaterialTheme.shapes.medium)
            .background(brush)
            .clickable(role = Role.Button, onClick = onClick)
            .padding(horizontal = 20.dp, vertical = 14.dp),
        contentAlignment = Alignment.Center,
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            if (icon != null) { Icon(icon, null, tint = Color.White, modifier = Modifier.size(20.dp)); Spacer(Modifier.width(8.dp)) }
            Text(text, color = Color.White, style = MaterialTheme.typography.labelLarge)
        }
    }
}

// ---------------- Inputs ----------------
@Composable
fun DiTextField(
    value: String, onValueChange: (String) -> Unit, label: String, modifier: Modifier = Modifier, error: String? = null,
    keyboardType: KeyboardType = KeyboardType.Text, imeAction: ImeAction = ImeAction.Next, isPassword: Boolean = false,
    singleLine: Boolean = true, minLines: Int = 1, leadingIcon: ImageVector? = null, onImeAction: () -> Unit = {}, supportingText: String? = null,
    enabled: Boolean = true,
) {
    var visible by rememberSaveable { mutableStateOf(false) }
    OutlinedTextField(
        value = value, onValueChange = onValueChange, label = { Text(label) }, modifier = modifier.fillMaxWidth(),
        isError = error != null, singleLine = singleLine, minLines = minLines, enabled = enabled,
        supportingText = (error ?: supportingText)?.let { { Text(it) } },
        leadingIcon = leadingIcon?.let { { Icon(it, contentDescription = null) } },
        trailingIcon = if (isPassword) {
            {
                IconButton(onClick = { visible = !visible }) {
                    Icon(if (visible) Icons.Filled.VisibilityOff else Icons.Filled.Visibility,
                        contentDescription = if (visible) "Hide password" else "Show password")
                }
            }
        } else null,
        visualTransformation = if (isPassword && !visible) PasswordVisualTransformation() else VisualTransformation.None,
        keyboardOptions = KeyboardOptions(keyboardType = if (isPassword) KeyboardType.Password else keyboardType, imeAction = imeAction),
        keyboardActions = KeyboardActions(onAny = { onImeAction() }),
        shape = MaterialTheme.shapes.medium,
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DropdownField(label: String, options: List<String>, selected: String?, onSelected: (String) -> Unit, modifier: Modifier = Modifier, display: (String) -> String = { it }) {
    var expanded by remember { mutableStateOf(false) }
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }, modifier = modifier) {
        OutlinedTextField(
            value = selected?.let(display).orEmpty(), onValueChange = {}, readOnly = true, label = { Text(label) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor(ExposedDropdownMenuAnchorType.PrimaryNotEditable).fillMaxWidth(), shape = MaterialTheme.shapes.medium,
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            options.forEach { option ->
                DropdownMenuItem(text = { Text(display(option)) }, onClick = { onSelected(option); expanded = false })
            }
        }
    }
}

// ---------------- Cards & layout ----------------
@Composable
fun DiCard(modifier: Modifier = Modifier, onClick: (() -> Unit)? = null, contentPadding: PaddingValues = PaddingValues(16.dp), content: @Composable ColumnScope.() -> Unit) {
    val colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    val border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant)
    if (onClick != null) {
        Card(onClick = onClick, modifier = modifier, shape = MaterialTheme.shapes.large, colors = colors, border = border, elevation = CardDefaults.cardElevation(Elevation.card)) {
            Column(Modifier.padding(contentPadding), content = content)
        }
    } else {
        Card(modifier = modifier, shape = MaterialTheme.shapes.large, colors = colors, border = border, elevation = CardDefaults.cardElevation(Elevation.card)) {
            Column(Modifier.padding(contentPadding), content = content)
        }
    }
}

@Composable
fun GradientCard(modifier: Modifier = Modifier, brush: Brush = DiTheme.colors.heroGradient, onClick: (() -> Unit)? = null, content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier
            .clip(MaterialTheme.shapes.large)
            .background(brush)
            .then(if (onClick != null) Modifier.clickable(onClick = onClick) else Modifier)
            .padding(20.dp),
        content = content,
    )
}

@Composable
fun SectionHeader(title: String, modifier: Modifier = Modifier, action: String? = null, onAction: (() -> Unit)? = null) {
    Row(modifier.fillMaxWidth().padding(top = 8.dp, bottom = 4.dp).semantics { heading() }, verticalAlignment = Alignment.CenterVertically) {
        Text(title, style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
        if (action != null && onAction != null) TextButton(onClick = onAction) { Text(action) }
    }
}

@Composable
fun StatTile(label: String, value: String, icon: ImageVector, modifier: Modifier = Modifier, tint: Color = MaterialTheme.colorScheme.primary) {
    DiCard(modifier.semantics(mergeDescendants = true) {}, contentPadding = PaddingValues(14.dp)) {
        Box(Modifier.size(36.dp).clip(RoundedCornerShape(10.dp)).background(tint.copy(alpha = 0.14f)), contentAlignment = Alignment.Center) {
            Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(20.dp))
        }
        Spacer(Modifier.height(10.dp))
        AnimatedCounter(value, style = MaterialTheme.typography.headlineSmall)
        Text(label, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

/** Counts numeric values up when they first appear; non-numeric values are shown as-is. */
@Composable
fun AnimatedCounter(value: String, style: androidx.compose.ui.text.TextStyle, color: Color = Color.Unspecified) {
    val number = value.filter { it.isDigit() }.toIntOrNull()
    if (number == null || value.any { it.isLetter() && it != '%' }) {
        Text(value, style = style, color = color); return
    }
    val animated by animateIntAsState(targetValue = number, animationSpec = tween(900, easing = FastOutSlowInEasing), label = "counter")
    Text(value.replace(number.toString(), animated.toString()), style = style, color = color)
}

// ---------------- Chips ----------------
@Composable
fun DifficultyChip(difficulty: String, modifier: Modifier = Modifier) {
    val color = difficultyColor(difficulty)
    Text(
        difficulty, modifier = modifier.clip(CircleShape).background(color.copy(alpha = 0.14f)).padding(horizontal = 10.dp, vertical = 4.dp),
        color = color, style = MaterialTheme.typography.labelMedium,
    )
}

@Composable
fun TagChip(text: String, modifier: Modifier = Modifier) {
    Text(
        text, modifier = modifier.clip(CircleShape).background(MaterialTheme.colorScheme.surfaceVariant).padding(horizontal = 10.dp, vertical = 4.dp),
        style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
}

@Composable
fun PremiumBadge(modifier: Modifier = Modifier) {
    Row(modifier.clip(CircleShape).background(DiTheme.colors.premium).padding(horizontal = 8.dp, vertical = 3.dp), verticalAlignment = Alignment.CenterVertically) {
        Icon(Icons.Filled.WorkspacePremium, contentDescription = null, tint = Color.White, modifier = Modifier.size(12.dp))
        Spacer(Modifier.width(3.dp))
        Text("PRO", color = Color.White, style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
    }
}

// ---------------- Progress ----------------
@Composable
fun ScoreRing(score: Int, modifier: Modifier = Modifier, size: Dp = 120.dp, stroke: Dp = 12.dp, label: String? = null, color: Color = MaterialTheme.colorScheme.primary) {
    val progress by animateFloatAsState((score.coerceIn(0, 100)) / 100f, tween(1100, easing = FastOutSlowInEasing), label = "ring")
    val track = MaterialTheme.colorScheme.surfaceVariant
    Box(modifier.size(size).semantics { contentDescription = "${label ?: "Score"}: $score out of 100" }, contentAlignment = Alignment.Center) {
        Canvas(Modifier.fillMaxSize()) {
            val s = Stroke(stroke.toPx(), cap = StrokeCap.Round)
            drawArc(track, -90f, 360f, false, style = s)
            drawArc(color, -90f, 360f * progress, false, style = s)
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Text("${(progress * 100).toInt()}", style = MaterialTheme.typography.headlineMedium)
            if (label != null) Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
fun AnimatedProgressBar(progress: Float, modifier: Modifier = Modifier, color: Color = MaterialTheme.colorScheme.primary) {
    val animated by animateFloatAsState(progress.coerceIn(0f, 1f), tween(800), label = "bar")
    LinearProgressIndicator(
        progress = { animated }, modifier = modifier.fillMaxWidth().height(8.dp).clip(CircleShape), color = color,
        trackColor = MaterialTheme.colorScheme.surfaceVariant, strokeCap = StrokeCap.Round, drawStopIndicator = {},
    )
}

// ---------------- States ----------------
@Composable
fun LoadingState(modifier: Modifier = Modifier) {
    Column(modifier.fillMaxWidth().padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) { repeat(5) { SkeletonCard() } }
}

@Composable
fun SkeletonCard(modifier: Modifier = Modifier, height: Dp = 84.dp) {
    val transition = rememberInfiniteTransition(label = "shimmer")
    val x by transition.animateFloat(0f, 1000f, infiniteRepeatable(tween(1200, easing = LinearEasing)), label = "x")
    val base = MaterialTheme.colorScheme.surfaceVariant
    val highlight = MaterialTheme.colorScheme.surface
    Box(
        modifier.fillMaxWidth().height(height).clip(MaterialTheme.shapes.large)
            .background(Brush.linearGradient(listOf(base, highlight, base), start = androidx.compose.ui.geometry.Offset(x - 300f, 0f), end = androidx.compose.ui.geometry.Offset(x, 0f)))
            .semantics { contentDescription = "Loading" }
    )
}

@Composable
fun EmptyState(title: String, message: String, modifier: Modifier = Modifier, icon: ImageVector = Icons.Filled.Inbox, action: String? = null, onAction: (() -> Unit)? = null) {
    Column(modifier.fillMaxWidth().padding(32.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        Box(Modifier.size(72.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primaryContainer), contentAlignment = Alignment.Center) {
            Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(36.dp))
        }
        Spacer(Modifier.height(16.dp))
        Text(title, style = MaterialTheme.typography.titleMedium, textAlign = TextAlign.Center)
        Spacer(Modifier.height(6.dp))
        Text(message, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, textAlign = TextAlign.Center)
        if (action != null && onAction != null) { Spacer(Modifier.height(16.dp)); SecondaryButton(action, onAction) }
    }
}

@Composable
fun ErrorState(error: AppError, onRetry: (() -> Unit)?, modifier: Modifier = Modifier, onUpgrade: (() -> Unit)? = null) {
    when (error) {
        is AppError.PremiumRequired -> PremiumGate(error.message, onUpgrade, modifier)
        else -> {
            val icon = when (error) {
                is AppError.Network, is AppError.Offline -> Icons.Filled.WifiOff
                is AppError.Timeout -> Icons.Filled.HourglassEmpty
                is AppError.Unavailable -> Icons.Filled.CloudOff
                else -> Icons.Filled.ErrorOutline
            }
            EmptyState(
                title = when (error) {
                    is AppError.Network, is AppError.Offline -> "You're offline"
                    is AppError.Unavailable -> "Temporarily unavailable"
                    is AppError.QuotaExceeded -> "Daily limit reached"
                    else -> "Something went wrong"
                },
                message = error.message, icon = icon, modifier = modifier,
                action = if (onRetry != null && error !is AppError.QuotaExceeded) "Try again" else null, onAction = onRetry,
            )
        }
    }
}

@Composable
fun PremiumGate(message: String, onUpgrade: (() -> Unit)?, modifier: Modifier = Modifier) {
    GradientCard(modifier.fillMaxWidth().padding(16.dp), brush = DiTheme.colors.premium) {
        Icon(Icons.Filled.WorkspacePremium, contentDescription = null, tint = Color.White, modifier = Modifier.size(40.dp))
        Spacer(Modifier.height(8.dp))
        Text("Premium feature", color = Color.White, style = MaterialTheme.typography.titleLarge)
        Text(message, color = Color.White.copy(alpha = 0.92f), style = MaterialTheme.typography.bodyMedium)
        if (onUpgrade != null) {
            Spacer(Modifier.height(12.dp))
            Button(onClick = onUpgrade, colors = ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = Palette.Indigo700)) { Text("See plans") }
        }
    }
}

@Composable
fun OfflineBanner(isOnline: Boolean, pendingChanges: Int, modifier: Modifier = Modifier) {
    AnimatedVisibility(!isOnline || pendingChanges > 0, modifier = modifier) {
        val offline = !isOnline
        Row(
            Modifier.fillMaxWidth().background(if (offline) MaterialTheme.colorScheme.errorContainer else MaterialTheme.colorScheme.secondaryContainer)
                .padding(horizontal = 16.dp, vertical = 8.dp).semantics { liveRegion = LiveRegionMode.Polite },
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(if (offline) Icons.Filled.CloudOff else Icons.Filled.CloudSync, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(8.dp))
            Text(
                if (offline) "Offline — saved content is available. ${if (pendingChanges > 0) "$pendingChanges change(s) will sync later." else ""}"
                else "Syncing $pendingChanges change(s)…",
                style = MaterialTheme.typography.labelMedium,
            )
        }
    }
}

// ---------------- App bars & dialogs ----------------
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DiTopBar(title: String, onBack: (() -> Unit)? = null, actions: @Composable RowScope.() -> Unit = {}) {
    TopAppBar(
        title = { Text(title, maxLines = 1, overflow = TextOverflow.Ellipsis) },
        navigationIcon = { if (onBack != null) IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back") } },
        actions = actions,
        colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
    )
}

@Composable
fun ConfirmDialog(title: String, message: String, confirm: String, onConfirm: () -> Unit, onDismiss: () -> Unit, destructive: Boolean = false) {
    AlertDialog(
        onDismissRequest = onDismiss, title = { Text(title) }, text = { Text(message) },
        confirmButton = {
            TextButton(onClick = onConfirm) { Text(confirm, color = if (destructive) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

/** Pull-to-refresh wrapper around Material 3's PullToRefreshBox. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun Refreshable(refreshing: Boolean, onRefresh: () -> Unit, modifier: Modifier = Modifier, content: @Composable BoxScope.() -> Unit) {
    PullToRefreshBox(isRefreshing = refreshing, onRefresh = onRefresh, modifier = modifier, content = content)
}

/** Base URL of the API, used to resolve server-relative media paths such as uploaded profile photos. */
val LocalApiBaseUrl = androidx.compose.runtime.staticCompositionLocalOf { "" }

@Composable
fun Avatar(name: String, photoUrl: String?, modifier: Modifier = Modifier, size: Dp = 44.dp) {
    if (!photoUrl.isNullOrBlank()) {
        val base = LocalApiBaseUrl.current
        coil3.compose.AsyncImage(
            model = if (photoUrl.startsWith("/")) base.trimEnd('/') + photoUrl else photoUrl, contentDescription = "Profile photo of $name",
            modifier = modifier.size(size).clip(CircleShape), contentScale = androidx.compose.ui.layout.ContentScale.Crop,
        )
    } else {
        Box(modifier.size(size).clip(CircleShape).background(DiTheme.colors.heroGradient).semantics { contentDescription = "Avatar of $name" }, contentAlignment = Alignment.Center) {
            Text(name.split(" ").mapNotNull { it.firstOrNull()?.uppercase() }.take(2).joinToString(""), color = Color.White, style = MaterialTheme.typography.titleMedium)
        }
    }
}

@Composable
fun KeyValueRow(key: String, value: String, modifier: Modifier = Modifier) {
    Row(modifier.fillMaxWidth().padding(vertical = 6.dp)) {
        Text(key, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
        Text(value, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium)
    }
}

@Composable
fun BulletList(items: List<String>, modifier: Modifier = Modifier, icon: ImageVector = Icons.Filled.CheckCircle, tint: Color = MaterialTheme.colorScheme.primary) {
    Column(modifier, verticalArrangement = Arrangement.spacedBy(6.dp)) {
        items.forEach { item ->
            Row(verticalAlignment = Alignment.Top) {
                Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(18.dp).padding(top = 2.dp))
                Spacer(Modifier.width(8.dp))
                Text(item, style = MaterialTheme.typography.bodyMedium)
            }
        }
    }
}
