package com.devinterview.core.designsystem

import com.google.common.truth.Truth.assertThat
import org.junit.Test

class MarkdownParserTest {
    @Test fun `parses headings lists code and tables`() {
        val blocks = parseMarkdown(
            """
            ## Title
            Some **bold** text
            continues here.

            - one
            1. first
            ```csharp
            var x = 1;
            ```
            | A | B |
            |---|---|
            | 1 | 2 |
            """.trimIndent()
        )
        assertThat(blocks[0]).isEqualTo(MdBlock.Heading(2, "Title"))
        assertThat(blocks[1]).isEqualTo(MdBlock.Paragraph("Some **bold** text continues here."))
        assertThat(blocks[2]).isEqualTo(MdBlock.ListItem("•", "one"))
        assertThat(blocks[3]).isEqualTo(MdBlock.ListItem("1.", "first"))
        assertThat(blocks[4]).isEqualTo(MdBlock.Code("var x = 1;", "csharp"))
        assertThat(blocks[5]).isEqualTo(MdBlock.Table(listOf(listOf("A", "B"), listOf("1", "2"))))
    }

    @Test fun `highlighter marks keywords and strings`() {
        val colors = SyntaxColors(androidx.compose.ui.graphics.Color.Red, androidx.compose.ui.graphics.Color.Green, androidx.compose.ui.graphics.Color.Blue,
            androidx.compose.ui.graphics.Color.Gray, androidx.compose.ui.graphics.Color.Cyan, androidx.compose.ui.graphics.Color.Black)
        val text = CodeHighlighter.highlight("var s = \"hi\"; // note", "csharp", colors)
        val styles = text.spanStyles.map { text.text.substring(it.start, it.end) to it.item.color }
        assertThat(styles).contains("var" to androidx.compose.ui.graphics.Color.Red)
        assertThat(styles).contains("\"hi\"" to androidx.compose.ui.graphics.Color.Green)
        assertThat(styles).contains("// note" to androidx.compose.ui.graphics.Color.Gray)
    }
}
