package com.devinterview.core.designsystem

import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.*
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.OffsetMapping
import androidx.compose.ui.text.input.TransformedText
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp

// ---------------- Syntax highlighting ----------------
data class SyntaxColors(val keyword: Color, val string: Color, val number: Color, val comment: Color, val type: Color, val plain: Color)

@Composable
fun syntaxColors(): SyntaxColors {
    val dark = MaterialTheme.colorScheme.background.luminanceApprox() < 0.5f
    return if (dark) SyntaxColors(Color(0xFFC792EA), Color(0xFFC3E88D), Color(0xFFF78C6C), Color(0xFF7F8C98), Color(0xFF82AAFF), Color(0xFFE2E8F0))
    else SyntaxColors(Color(0xFF7C3AED), Color(0xFF047857), Color(0xFFB45309), Color(0xFF64748B), Color(0xFF1D4ED8), Color(0xFF0F172A))
}

private fun Color.luminanceApprox() = 0.299f * red + 0.587f * green + 0.114f * blue

object CodeHighlighter {
    private val keywords = mapOf(
        "csharp" to "abstract as async await base bool break case catch class const continue decimal default delegate do double else enum event false finally for foreach get if in int interface internal is lock long namespace new null object out override params private protected public readonly record ref return sealed set static string struct switch this throw true try typeof using var virtual void while yield",
        "java" to "abstract boolean break byte case catch char class continue default do double else enum extends final finally float for if implements import instanceof int interface long new null package private protected public return short static super switch this throw throws true false try void volatile while var record",
        "kotlin" to "as break class continue data do else false for fun if in interface is null object package return sealed super this throw true try typealias val var when while override private public internal suspend import",
        "javascript" to "async await break case catch class const continue default delete do else export extends false finally for function if import in instanceof let new null return super switch this throw true try typeof undefined var void while yield of",
        "typescript" to "async await break case catch class const continue default do else enum export extends false finally for function if implements import in interface keyof let new null private public readonly return string number boolean switch this throw true try type typeof undefined var void while",
        "python" to "and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return True try while with yield self",
        "cpp" to "auto bool break case catch char class const continue default delete do double else enum false float for if include int long namespace new nullptr private protected public return short sizeof static struct switch template this throw true try typedef typename using vector virtual void while string",
        "sql" to "select from where join inner left right full outer on group by order having insert into values update set delete create table index primary key foreign references as and or not null is in exists distinct union all limit offset case when then else end with over partition count sum avg min max top",
    ).mapValues { it.value.split(' ').toSet() }

    private val tokenRegex = Regex("""("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|//.*|#.*|--.*|/\*[\s\S]*?\*/|\b\d+(?:\.\d+)?\b|\b[A-Za-z_][A-Za-z0-9_]*\b)""")

    fun highlight(code: String, language: String, colors: SyntaxColors): AnnotatedString {
        val lang = language.lowercase()
        val keys = keywords[lang] ?: keywords.getValue("csharp")
        val caseInsensitive = lang == "sql"
        return buildAnnotatedString {
            append(code)
            addStyle(SpanStyle(color = colors.plain), 0, code.length)
            for (m in tokenRegex.findAll(code)) {
                val t = m.value
                val style = when {
                    t.startsWith("\"") || t.startsWith("'") -> SpanStyle(color = colors.string)
                    t.startsWith("//") || t.startsWith("/*") || (t.startsWith("#") && lang == "python") || (t.startsWith("--") && lang == "sql") -> SpanStyle(color = colors.comment, fontStyle = androidx.compose.ui.text.font.FontStyle.Italic)
                    t.first().isDigit() -> SpanStyle(color = colors.number)
                    (if (caseInsensitive) t.lowercase() else t) in keys -> SpanStyle(color = colors.keyword, fontWeight = FontWeight.SemiBold)
                    t.first().isUpperCase() -> SpanStyle(color = colors.type)
                    else -> null
                }
                if (style != null) addStyle(style, m.range.first, m.range.last + 1)
            }
        }
    }
}

class SyntaxTransformation(private val language: String, private val colors: SyntaxColors) : VisualTransformation {
    override fun filter(text: AnnotatedString): TransformedText =
        TransformedText(CodeHighlighter.highlight(text.text, language, colors), OffsetMapping.Identity)
}

@Composable
fun CodeBlock(code: String, language: String?, modifier: Modifier = Modifier) {
    val colors = syntaxColors()
    val highlighted = remember(code, language, colors) { CodeHighlighter.highlight(code, language ?: "csharp", colors) }
    Box(
        modifier.fillMaxWidth().clip(MaterialTheme.shapes.medium).background(MaterialTheme.colorScheme.surfaceVariant)
            .horizontalScroll(rememberScrollState()).padding(14.dp)
            .semantics { contentDescription = "Code sample in ${language ?: "code"}: $code" }
    ) { Text(highlighted, style = CodeStyle) }
}

/** Editable code area with live syntax highlighting and line numbers. */
@Composable
fun CodeEditorField(value: String, onValueChange: (String) -> Unit, language: String, modifier: Modifier = Modifier) {
    val colors = syntaxColors()
    val transformation = remember(language, colors) { SyntaxTransformation(language, colors) }
    val lines = value.count { it == '\n' } + 1
    Row(modifier.clip(MaterialTheme.shapes.medium).background(MaterialTheme.colorScheme.surfaceVariant).padding(vertical = 12.dp)) {
        Text(
            (1..lines).joinToString("\n"), style = CodeStyle.copy(color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f)),
            modifier = Modifier.padding(horizontal = 10.dp),
        )
        Box(Modifier.horizontalScroll(rememberScrollState()).padding(end = 12.dp)) {
            BasicTextField(
                value = value, onValueChange = onValueChange, textStyle = CodeStyle.copy(color = colors.plain), visualTransformation = transformation,
                cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
                modifier = Modifier.widthIn(min = 600.dp).semantics { contentDescription = "Code editor, $language" },
            )
        }
    }
}

// ---------------- Markdown ----------------
/**
 * Lightweight Markdown renderer for lesson and explanation content: headings, paragraphs, bullet/numbered lists,
 * fenced code blocks, tables, **bold**, *italic* and `inline code`.
 */
@Composable
fun MarkdownText(markdown: String, modifier: Modifier = Modifier) {
    val blocks = remember(markdown) { parseMarkdown(markdown) }
    val codeBackground = MaterialTheme.colorScheme.surfaceVariant
    Column(modifier, verticalArrangement = Arrangement.spacedBy(8.dp)) {
        blocks.forEach { block ->
            when (block) {
                is MdBlock.Heading -> Text(inline(block.text, codeBackground), style = when (block.level) {
                    1 -> MaterialTheme.typography.headlineSmall; 2 -> MaterialTheme.typography.titleLarge; else -> MaterialTheme.typography.titleMedium
                }, modifier = Modifier.padding(top = 6.dp))
                is MdBlock.Paragraph -> Text(inline(block.text, codeBackground), style = MaterialTheme.typography.bodyLarge)
                is MdBlock.ListItem -> Row(Modifier.padding(start = 4.dp)) {
                    Text(block.marker, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.primary, modifier = Modifier.width(24.dp))
                    Text(inline(block.text, codeBackground), style = MaterialTheme.typography.bodyLarge)
                }
                is MdBlock.Code -> CodeBlock(block.code, block.language)
                is MdBlock.Table -> Column(Modifier.fillMaxWidth().clip(MaterialTheme.shapes.small).background(codeBackground).horizontalScroll(rememberScrollState()).padding(8.dp)) {
                    block.rows.forEachIndexed { index, cells ->
                        Row {
                            cells.forEach { cell ->
                                Text(inline(cell, codeBackground), style = if (index == 0) MaterialTheme.typography.labelLarge else MaterialTheme.typography.bodySmall,
                                    modifier = Modifier.width(150.dp).padding(4.dp))
                            }
                        }
                    }
                }
            }
        }
    }
}

sealed interface MdBlock {
    data class Heading(val level: Int, val text: String) : MdBlock
    data class Paragraph(val text: String) : MdBlock
    data class ListItem(val marker: String, val text: String) : MdBlock
    data class Code(val code: String, val language: String?) : MdBlock
    data class Table(val rows: List<List<String>>) : MdBlock
}

fun parseMarkdown(markdown: String): List<MdBlock> {
    val blocks = mutableListOf<MdBlock>()
    val lines = markdown.replace("\r\n", "\n").split('\n')
    var i = 0
    val paragraph = StringBuilder()
    fun flush() { if (paragraph.isNotBlank()) blocks += MdBlock.Paragraph(paragraph.toString().trim()); paragraph.clear() }
    while (i < lines.size) {
        val line = lines[i]
        val trimmed = line.trim()
        when {
            trimmed.startsWith("```") -> {
                flush()
                val lang = trimmed.removePrefix("```").trim().ifBlank { null }
                val code = StringBuilder()
                i++
                while (i < lines.size && !lines[i].trim().startsWith("```")) { code.appendLine(lines[i]); i++ }
                blocks += MdBlock.Code(code.toString().trimEnd(), lang)
            }
            trimmed.startsWith("#") -> { flush(); val level = trimmed.takeWhile { it == '#' }.length; blocks += MdBlock.Heading(level, trimmed.drop(level).trim()) }
            trimmed.startsWith("- ") || trimmed.startsWith("* ") -> { flush(); blocks += MdBlock.ListItem("•", trimmed.drop(2)) }
            Regex("^\\d+\\. ").containsMatchIn(trimmed) -> { flush(); val n = trimmed.substringBefore('.'); blocks += MdBlock.ListItem("$n.", trimmed.substringAfter(". ")) }
            trimmed.startsWith("|") -> {
                flush()
                val rows = mutableListOf<List<String>>()
                while (i < lines.size && lines[i].trim().startsWith("|")) {
                    val cells = lines[i].trim().trim('|').split('|').map { it.trim() }
                    if (!cells.all { it.matches(Regex("^:?-{2,}:?$")) }) rows += cells
                    i++
                }
                blocks += MdBlock.Table(rows)
                continue
            }
            trimmed.isEmpty() -> flush()
            else -> paragraph.append(trimmed).append(' ')
        }
        i++
    }
    flush()
    return blocks
}

private fun inline(text: String, codeBackground: Color): AnnotatedString = buildAnnotatedString {
    val regex = Regex("""(\*\*[^*]+\*\*|`[^`]+`|\*[^*]+\*|_[^_]+_)""")
    var last = 0
    for (m in regex.findAll(text)) {
        append(text.substring(last, m.range.first))
        val t = m.value
        when {
            t.startsWith("**") -> withStyle(SpanStyle(fontWeight = FontWeight.Bold)) { append(t.removeSurrounding("**")) }
            t.startsWith("`") -> withStyle(SpanStyle(fontFamily = FontFamily.Monospace, background = codeBackground)) { append(t.removeSurrounding("`")) }
            else -> withStyle(SpanStyle(fontStyle = androidx.compose.ui.text.font.FontStyle.Italic)) { append(t.substring(1, t.length - 1)) }
        }
        last = m.range.last + 1
    }
    append(text.substring(last))
}
