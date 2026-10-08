using System;
using System.Collections.Generic;

using ClientCore;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.FontManagement;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// Text decorations that can be applied to a text fragment.
    /// </summary>
    [Flags]
    public enum DatabaseTextStyle
    {
        None = 0,
        Underline = 1,
        Strikethrough = 2
    }

    /// <summary>
    /// A single text fragment inside a <see cref="DatabaseParagraph"/>: a piece of
    /// text rendered with a specific font and style.
    /// </summary>
    public sealed class DatabaseRun
    {
        public string Text;
        public int FontIndex;
        public DatabaseTextStyle Style;
    }

    /// <summary>
    /// A paragraph inside a <see cref="DatabaseRichTextBlock"/>: either a wrapped text
    /// block (made of one or more <see cref="DatabaseRun"/> fragments) or an image that
    /// is scaled to the available width.
    /// </summary>
    public sealed class DatabaseParagraph
    {
        public List<DatabaseRun> Runs;
        public Texture2D Image;
        public int Width;
        public int Height;
        public bool IsImage => Image != null;
    }

    /// <summary>
    /// A panel that renders mixed text and image content (as used by the Database
    /// window's article viewer) and can be scrolled vertically with the mouse wheel.
    /// Text paragraphs may contain multiple font runs (switched with [FONT:n] markers),
    /// which flow together on the same line without forced paragraph breaks.
    /// All visual settings can be overridden through INI attributes (typically from
    /// the [richTextBlock] section of the Database window's INI file).
    /// </summary>
    public class DatabaseRichTextBlock : XNAPanel
    {
        private const int DEFAULT_SCROLL_STEP = 40;
        private const int DEFAULT_IMAGE_MARGIN = 8;
        private const int DEFAULT_PARAGRAPH_SPACING = 12;
        private const int DEFAULT_SCROLLBAR_WIDTH = 4;
        private const int DECORATION_THICKNESS = 2;

        private sealed class TextLine
        {
            public List<TextSegment> Segments = new();
            public int Width;
            public int Height;
        }

        private sealed class TextSegment
        {
            public string Text;
            public int FontIndex;
            public DatabaseTextStyle Style;
        }

        private readonly List<DatabaseParagraph> paragraphs = new();
        private readonly List<TextLine> allLines = new();
        private readonly List<int> paragraphLineStart = new();
        private readonly List<int> paragraphHeight = new();

        private float scrollOffset;
        private int contentHeight;
        private bool rebuildNeeded = true;

        public DatabaseRichTextBlock(WindowManager windowManager) : base(windowManager)
        {
            DrawMode = ControlDrawMode.UNIQUE_RENDER_TARGET;
        }

        public int FontIndex { get; set; } = 1;

        public int ScrollStep { get; set; } = DEFAULT_SCROLL_STEP;

        public int ImageMargin { get; set; } = DEFAULT_IMAGE_MARGIN;

        public int ParagraphSpacing { get; set; } = DEFAULT_PARAGRAPH_SPACING;

        public int ScrollbarWidth { get; set; } = DEFAULT_SCROLLBAR_WIDTH;

        public Color ScrollbarColor { get; set; } = new Color(190, 190, 190, 220);

        public Color ScrollbarBackgroundColor { get; set; } = new Color(0, 0, 0, 90);

        /// <summary>
        /// The color used for underline and strikethrough decorations. Transparent means
        /// "use the text color" (the default).
        /// </summary>
        public Color TextDecorationColor { get; set; } = Color.Transparent;

        public int MaxScrollOffset => Math.Max(0, contentHeight - Height);

        private int ContentWidth => Math.Max(1, Width - ScrollbarWidth - 8);

        private static int GetLineHeight(int fontIndex)
        {
            return Math.Max(1, (int)Math.Ceiling(Renderer.MeasureString("Test", fontIndex).Y) + 1);
        }

        private static int GetTextWidth(string text, int fontIndex)
        {
            return (int)Math.Ceiling(Renderer.MeasureString(text, fontIndex).X);
        }

        private static int ClampFontIndex(int fontIndex)
        {
            List<IFont> fonts = Renderer.GetFontList();
            int count = fonts.Count;
            if (count == 0)
                return 0;

            if (fontIndex < 0)
                return 0;
            if (fontIndex >= count)
                return count - 1;

            return fontIndex;
        }

        /// <summary>
        /// Clears all paragraphs.
        /// </summary>
        public void ClearContent()
        {
            paragraphs.Clear();
            allLines.Clear();
            paragraphLineStart.Clear();
            paragraphHeight.Clear();
            contentHeight = 0;
            scrollOffset = 0;
            rebuildNeeded = true;
        }

        /// <summary>
        /// Adds a text paragraph (wrapped to the control width) using the control's
        /// default font.
        /// </summary>
        public void AddText(string text)
        {
            AddText(text, FontIndex);
        }

        /// <summary>
        /// Adds a text paragraph (wrapped to the control width) using a specific font.
        /// </summary>
        public void AddText(string text, int fontIndex)
        {
            AddText(text, fontIndex, DatabaseTextStyle.None);
        }

        /// <summary>
        /// Adds a text paragraph (wrapped to the control width) using a specific font
        /// and text decoration style.
        /// </summary>
        public void AddText(string text, int fontIndex, DatabaseTextStyle style)
        {
            var paragraph = new DatabaseParagraph
            {
                Runs = new List<DatabaseRun>()
            };
            paragraph.Runs.Add(new DatabaseRun
            {
                Text = text ?? string.Empty,
                FontIndex = ClampFontIndex(fontIndex),
                Style = style
            });
            paragraphs.Add(paragraph);
            rebuildNeeded = true;
        }

        /// <summary>
        /// Appends a text fragment to the current text paragraph so the paragraph keeps
        /// flowing without a line break. If the last paragraph is an image or there is
        /// no paragraph yet, a new text paragraph is started instead.
        /// </summary>
        public void AppendText(string text, int fontIndex)
        {
            AppendText(text, fontIndex, DatabaseTextStyle.None);
        }

        /// <summary>
        /// Appends a text fragment with a specific decoration style to the current text
        /// paragraph so the paragraph keeps flowing without a line break.
        /// </summary>
        public void AppendText(string text, int fontIndex, DatabaseTextStyle style)
        {
            DatabaseParagraph last = paragraphs.Count > 0 ? paragraphs[paragraphs.Count - 1] : null;

            if (last != null && last.IsImage == false && last.Runs != null)
            {
                last.Runs.Add(new DatabaseRun
                {
                    Text = text ?? string.Empty,
                    FontIndex = ClampFontIndex(fontIndex),
                    Style = style
                });
            }
            else
            {
                AddText(text, fontIndex, style);
            }

            rebuildNeeded = true;
        }

        /// <summary>
        /// Adds an image paragraph, scaled down to fit the control width while keeping
        /// its aspect ratio.
        /// </summary>
        public void AddImage(Texture2D texture)
        {
            if (texture == null)
                return;

            paragraphs.Add(new DatabaseParagraph { Image = texture });
            rebuildNeeded = true;
        }

        private void Rebuild()
        {
            allLines.Clear();
            paragraphLineStart.Clear();
            paragraphHeight.Clear();
            contentHeight = 0;

            for (int i = 0; i < paragraphs.Count; i++)
            {
                DatabaseParagraph paragraph = paragraphs[i];

                if (paragraph.IsImage)
                {
                    int maxWidth = ContentWidth - ImageMargin * 2;
                    int maxHeight = Math.Max(120, Height * 2);
                    float scale = Math.Min(1.0f,
                        Math.Min((float)maxWidth / paragraph.Image.Width,
                                 (float)maxHeight / paragraph.Image.Height));
                    paragraph.Width = (int)(paragraph.Image.Width * scale);
                    paragraph.Height = (int)(paragraph.Image.Height * scale);
                    paragraphLineStart.Add(allLines.Count);
                    paragraphHeight.Add(paragraph.Height);
                    contentHeight += paragraph.Height;
                }
                else
                {
                    paragraphLineStart.Add(allLines.Count);
                    List<TextLine> lines = WrapParagraph(paragraph.Runs, ContentWidth);
                    allLines.AddRange(lines);

                    int h = 0;
                    foreach (TextLine line in lines)
                        h += line.Height;
                    paragraphHeight.Add(h);
                    contentHeight += h;
                }

                if (i < paragraphs.Count - 1)
                    contentHeight += ParagraphSpacing;
            }

            rebuildNeeded = false;
            ClampScrollOffset();
        }

        /// <summary>
        /// Greedy word-wraps the given text runs into lines, measuring each word with its
        /// own font. Runs flow together, so [FONT:n] font changes never force a line
        /// break; only the line overflowing the available width does. Spaces are kept as
        /// styled segments so underline/strikethrough decorations stay continuous across
        /// the whole span (including spaces and whitespace-only content).
        /// </summary>
        private static List<TextLine> WrapParagraph(List<DatabaseRun> runs, int maxWidth)
        {
            var lines = new List<TextLine>();
            TextLine currentLine = new TextLine();

            void AddSegment(TextSegment segment, bool isSpace)
            {
                int segmentWidth = GetTextWidth(segment.Text, segment.FontIndex);
                int lineHeight = GetLineHeight(segment.FontIndex);

                // Break at word boundaries only: a word that doesn't fit on the current
                // (non-empty) line starts a new line. Spaces never force a break.
                if (!isSpace && currentLine.Segments.Count > 0 && currentLine.Width + segmentWidth > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new TextLine();
                }

                currentLine.Segments.Add(segment);
                currentLine.Width += segmentWidth;
                if (lineHeight > currentLine.Height)
                    currentLine.Height = lineHeight;
            }

            if (runs != null)
            {
                foreach (DatabaseRun run in runs)
                {
                    int fontIndex = ClampFontIndex(run.FontIndex);
                    bool producedSegment = false;

                    string text = run.Text ?? string.Empty;
                    int i = 0;
                    while (i < text.Length)
                    {
                        int start = i;
                        bool isSpace = text[i] == ' ';
                        while (i < text.Length && (text[i] == ' ') == isSpace)
                            i++;

                        AddSegment(new TextSegment
                        {
                            Text = text.Substring(start, i - start),
                            FontIndex = fontIndex,
                            Style = run.Style
                        }, isSpace);
                        producedSegment = true;
                    }

                    // A styled run with no content still yields a minimal segment so the
                    // underline/strikethrough decoration remains visible.
                    if (!producedSegment && run.Style != DatabaseTextStyle.None)
                    {
                        AddSegment(new TextSegment
                        {
                            Text = " ",
                            FontIndex = fontIndex,
                            Style = run.Style
                        }, isSpace: true);
                    }
                }
            }

            if (currentLine.Segments.Count > 0)
                lines.Add(currentLine);

            return lines;
        }

        private void ClampScrollOffset()
        {
            if (scrollOffset < 0)
                scrollOffset = 0;
            else if (scrollOffset > MaxScrollOffset)
                scrollOffset = MaxScrollOffset;
        }

        /// <summary>
        /// Disposes all images owned by this block (for example when the window is closed).
        /// </summary>
        public void DisposeImages()
        {
            foreach (DatabaseParagraph paragraph in paragraphs)
            {
                if (paragraph.Image != null)
                {
                    paragraph.Image.Dispose();
                    paragraph.Image = null;
                }
            }
        }

        protected override void OnClientRectangleUpdated()
        {
            base.OnClientRectangleUpdated();
            rebuildNeeded = true;
        }

        public override void OnMouseScrolled(InputEventArgs inputEventArgs)
        {
            int delta = Cursor.ScrollWheelValue;
            if (delta != 0)
            {
                scrollOffset -= delta > 0 ? ScrollStep : -ScrollStep;
                ClampScrollOffset();
                inputEventArgs.Handled = true;
            }

            base.OnMouseScrolled(inputEventArgs);
        }

        public override void Draw(GameTime gameTime)
        {
            DrawPanel();

            if (rebuildNeeded)
                Rebuild();

            if (paragraphs.Count == 0)
            {
                if (DrawBorders)
                    DrawPanelBorders();
                DrawChildren(gameTime);
                return;
            }

            int y = -(int)scrollOffset;

            for (int i = 0; i < paragraphs.Count; i++)
            {
                DatabaseParagraph paragraph = paragraphs[i];

                if (paragraph.IsImage)
                {
                    if (y + paragraph.Height > 0 && y < Height)
                    {
                        int drawX = (Width - paragraph.Width) / 2;
                        DrawTexture(paragraph.Image,
                            new Rectangle(drawX, y, paragraph.Width, paragraph.Height), Color.White);
                    }
                }
                else
                {
                    int start = paragraphLineStart[i];
                    int end = i + 1 < paragraphs.Count ? paragraphLineStart[i + 1] : allLines.Count;
                    int lineY = y;

                    for (int l = start; l < end; l++)
                    {
                        TextLine line = allLines[l];

                        if (lineY + line.Height > 0 && lineY < Height)
                        {
                            int x = 4;
                            for (int s = 0; s < line.Segments.Count; s++)
                            {
                                TextSegment segment = line.Segments[s];
                                // Bottom-align segments of different font sizes on the
                                // same line: smaller fonts are pushed down so their
                                // baselines line up with the largest font's baseline.
                                int segmentLineHeight = GetLineHeight(segment.FontIndex);
                                int drawY = lineY + (line.Height - segmentLineHeight);
                                if (!string.IsNullOrWhiteSpace(segment.Text))
                                {
                                    DrawStringWithShadow(segment.Text, segment.FontIndex,
                                        new Vector2(x, drawY), UISettings.ActiveSettings.TextColor);
                                }

                                // Decorations cover the full segment (including spaces),
                                // so underline/strikethrough stays continuous.
                                if (segment.Style != DatabaseTextStyle.None)
                                    DrawDecorations(segment, x, drawY, segmentLineHeight);

                                x += GetTextWidth(segment.Text, segment.FontIndex);
                            }
                        }

                        lineY += line.Height;
                    }
                }

                y += paragraphHeight[i];
                if (i < paragraphs.Count - 1)
                    y += ParagraphSpacing;
            }

            if (contentHeight > Height)
                DrawScrollbar();

            if (DrawBorders)
                DrawPanelBorders();

            DrawChildren(gameTime);
        }

        private void DrawDecorations(TextSegment segment, int x, int drawY, int segmentLineHeight)
        {
            Color decorationColor = TextDecorationColor.A == 0
                ? UISettings.ActiveSettings.TextColor
                : TextDecorationColor;
            int textWidth = GetTextWidth(segment.Text, segment.FontIndex);

            if ((segment.Style & DatabaseTextStyle.Underline) != 0)
            {
                FillRectangle(new Rectangle(x, drawY + segmentLineHeight - DECORATION_THICKNESS,
                    textWidth, DECORATION_THICKNESS), decorationColor);
            }

            if ((segment.Style & DatabaseTextStyle.Strikethrough) != 0)
            {
                FillRectangle(new Rectangle(x, drawY + segmentLineHeight / 2,
                    textWidth, DECORATION_THICKNESS), decorationColor);
            }
        }

        private void DrawScrollbar()
        {
            FillRectangle(new Rectangle(Width - ScrollbarWidth - 1, 1,
                ScrollbarWidth, Height - 2), ScrollbarBackgroundColor);

            int thumbHeight = Math.Max(8, Height * Height / Math.Max(1, contentHeight));
            int maxOffset = MaxScrollOffset;
            int thumbY = maxOffset > 0
                ? (int)((long)(Height - thumbHeight) * scrollOffset / maxOffset)
                : 0;

            FillRectangle(new Rectangle(Width - ScrollbarWidth - 1, thumbY,
                ScrollbarWidth, thumbHeight), ScrollbarColor);
        }

        protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "FontIndex":
                    FontIndex = Conversions.IntFromString(value, FontIndex);
                    return;
                case "ScrollStep":
                    ScrollStep = Conversions.IntFromString(value, ScrollStep);
                    return;
                case "ImageMargin":
                    ImageMargin = Conversions.IntFromString(value, ImageMargin);
                    return;
                case "ParagraphSpacing":
                    ParagraphSpacing = Conversions.IntFromString(value, ParagraphSpacing);
                    return;
                case "ScrollbarWidth":
                    ScrollbarWidth = Conversions.IntFromString(value, ScrollbarWidth);
                    return;
                case "ScrollbarColor":
                    ScrollbarColor = AssetLoader.GetColorFromString(value);
                    return;
                case "ScrollbarBackgroundColor":
                    ScrollbarBackgroundColor = AssetLoader.GetColorFromString(value);
                    return;
                case "TextDecorationColor":
                    TextDecorationColor = AssetLoader.GetColorFromString(value);
                    return;
            }

            base.ParseControlINIAttribute(iniFile, key, value);
        }
    }
}
