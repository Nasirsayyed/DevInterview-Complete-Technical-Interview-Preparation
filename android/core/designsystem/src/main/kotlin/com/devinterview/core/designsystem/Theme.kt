package com.devinterview.core.designsystem

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Brand palette. Contrast ratios of text roles against their surfaces meet WCAG AA in both themes. */
object Palette {
    val Indigo50 = Color(0xFFEEF2FF)
    val Indigo100 = Color(0xFFE0E7FF)
    val Indigo300 = Color(0xFFA5B4FC)
    val Indigo400 = Color(0xFF818CF8)
    val Indigo500 = Color(0xFF6366F1)
    val Indigo600 = Color(0xFF4F46E5)
    val Indigo700 = Color(0xFF4338CA)
    val Indigo900 = Color(0xFF1E1B4B)
    val Violet500 = Color(0xFF8B5CF6)
    val Cyan400 = Color(0xFF22D3EE)
    val Cyan600 = Color(0xFF0891B2)
    val Emerald500 = Color(0xFF10B981)
    val Emerald600 = Color(0xFF059669)
    val Amber500 = Color(0xFFF59E0B)
    val Rose500 = Color(0xFFF43F5E)
    val Rose600 = Color(0xFFE11D48)
    val Slate50 = Color(0xFFF8FAFC)
    val Slate100 = Color(0xFFF1F5F9)
    val Slate200 = Color(0xFFE2E8F0)
    val Slate400 = Color(0xFF94A3B8)
    val Slate500 = Color(0xFF64748B)
    val Slate700 = Color(0xFF334155)
    val Slate800 = Color(0xFF1E293B)
    val Slate900 = Color(0xFF0F172A)
    val Ink = Color(0xFF0B1120)
}

private val LightColors = lightColorScheme(
    primary = Palette.Indigo600, onPrimary = Color.White, primaryContainer = Palette.Indigo100, onPrimaryContainer = Palette.Indigo900,
    secondary = Palette.Cyan600, onSecondary = Color.White, secondaryContainer = Color(0xFFCFFAFE), onSecondaryContainer = Color(0xFF083344),
    tertiary = Palette.Violet500, onTertiary = Color.White,
    background = Palette.Slate50, onBackground = Palette.Slate900, surface = Color.White, onSurface = Palette.Slate900,
    surfaceVariant = Palette.Slate100, onSurfaceVariant = Palette.Slate700, surfaceContainer = Palette.Slate100,
    surfaceContainerHigh = Palette.Slate200, surfaceContainerLow = Color(0xFFF8FAFF),
    outline = Palette.Slate400, outlineVariant = Palette.Slate200, error = Palette.Rose600, onError = Color.White,
)

private val DarkColors = darkColorScheme(
    primary = Palette.Indigo400, onPrimary = Palette.Indigo900, primaryContainer = Palette.Indigo700, onPrimaryContainer = Palette.Indigo50,
    secondary = Palette.Cyan400, onSecondary = Color(0xFF083344), secondaryContainer = Color(0xFF155E75), onSecondaryContainer = Color(0xFFCFFAFE),
    tertiary = Color(0xFFC4B5FD), onTertiary = Color(0xFF2E1065),
    background = Palette.Ink, onBackground = Palette.Slate100, surface = Palette.Slate900, onSurface = Palette.Slate100,
    surfaceVariant = Palette.Slate800, onSurfaceVariant = Color(0xFFCBD5E1), surfaceContainer = Palette.Slate800,
    surfaceContainerHigh = Palette.Slate700, surfaceContainerLow = Color(0xFF111A2E),
    outline = Palette.Slate500, outlineVariant = Palette.Slate700, error = Color(0xFFFB7185), onError = Color(0xFF4C0519),
)

@Immutable
data class Spacing(val xxs: Dp = 2.dp, val xs: Dp = 4.dp, val sm: Dp = 8.dp, val md: Dp = 12.dp, val lg: Dp = 16.dp, val xl: Dp = 24.dp, val xxl: Dp = 32.dp)

@Immutable
data class ExtendedColors(
    val success: Color, val warning: Color, val danger: Color, val beginner: Color, val intermediate: Color, val advanced: Color, val expert: Color,
    val heroGradient: Brush, val cardGradient: Brush, val premium: Brush,
)

val LocalSpacing = staticCompositionLocalOf { Spacing() }
val LocalExtendedColors = staticCompositionLocalOf<ExtendedColors> { error("DevInterviewTheme not applied") }

private val AppTypography = Typography(
    displaySmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Bold, fontSize = 34.sp, lineHeight = 40.sp, letterSpacing = (-0.5).sp),
    headlineMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Bold, fontSize = 26.sp, lineHeight = 32.sp),
    headlineSmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 22.sp, lineHeight = 28.sp),
    titleLarge = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 20.sp, lineHeight = 26.sp),
    titleMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 16.sp, lineHeight = 22.sp),
    titleSmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Medium, fontSize = 14.sp, lineHeight = 20.sp),
    bodyLarge = TextStyle(fontFamily = FontFamily.SansSerif, fontSize = 16.sp, lineHeight = 24.sp),
    bodyMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontSize = 14.sp, lineHeight = 21.sp),
    bodySmall = TextStyle(fontFamily = FontFamily.SansSerif, fontSize = 12.sp, lineHeight = 17.sp),
    labelLarge = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.SemiBold, fontSize = 14.sp, letterSpacing = 0.1.sp),
    labelMedium = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Medium, fontSize = 12.sp, letterSpacing = 0.3.sp),
    labelSmall = TextStyle(fontFamily = FontFamily.SansSerif, fontWeight = FontWeight.Medium, fontSize = 11.sp, letterSpacing = 0.4.sp),
)

val CodeStyle = TextStyle(fontFamily = FontFamily.Monospace, fontSize = 13.sp, lineHeight = 19.sp)

private val AppShapes = Shapes(
    extraSmall = RoundedCornerShape(6.dp), small = RoundedCornerShape(10.dp), medium = RoundedCornerShape(16.dp),
    large = RoundedCornerShape(22.dp), extraLarge = RoundedCornerShape(28.dp),
)

object Elevation {
    val none = 0.dp
    val card = 1.dp
    val raised = 3.dp
    val overlay = 8.dp
}

@Composable
fun DevInterviewTheme(themeMode: String = "system", content: @Composable () -> Unit) {
    val dark = when (themeMode) { "dark" -> true; "light" -> false; else -> isSystemInDarkTheme() }
    val colors: ColorScheme = if (dark) DarkColors else LightColors
    val extended = ExtendedColors(
        success = if (dark) Color(0xFF34D399) else Palette.Emerald600,
        warning = Palette.Amber500,
        danger = colors.error,
        beginner = if (dark) Color(0xFF34D399) else Palette.Emerald600,
        intermediate = if (dark) Palette.Cyan400 else Palette.Cyan600,
        advanced = if (dark) Color(0xFFFBBF24) else Color(0xFFB45309),
        expert = if (dark) Color(0xFFFB7185) else Palette.Rose600,
        heroGradient = Brush.linearGradient(listOf(Palette.Indigo600, Palette.Violet500, if (dark) Palette.Cyan600 else Palette.Cyan400)),
        cardGradient = Brush.linearGradient(if (dark) listOf(Color(0xFF1E1B4B), Palette.Slate900) else listOf(Palette.Indigo50, Color.White)),
        premium = Brush.linearGradient(listOf(Color(0xFFF59E0B), Color(0xFFEC4899), Palette.Violet500)),
    )
    CompositionLocalProvider(LocalSpacing provides Spacing(), LocalExtendedColors provides extended) {
        MaterialTheme(colorScheme = colors, typography = AppTypography, shapes = AppShapes, content = content)
    }
}

object DiTheme {
    val spacing: Spacing @Composable get() = LocalSpacing.current
    val colors: ExtendedColors @Composable get() = LocalExtendedColors.current
}

@Composable
fun difficultyColor(difficulty: String): Color = when (difficulty.lowercase()) {
    "beginner", "easy" -> DiTheme.colors.beginner
    "intermediate", "medium" -> DiTheme.colors.intermediate
    "advanced", "hard" -> DiTheme.colors.advanced
    "expert" -> DiTheme.colors.expert
    else -> MaterialTheme.colorScheme.outline
}
