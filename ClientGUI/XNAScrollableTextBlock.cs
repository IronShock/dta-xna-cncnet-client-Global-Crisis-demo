using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.FontManagement;
using Rampastring.XNAUI.XNAControls;

namespace ClientGUI
{
    /// <summary>
    /// A panel with text that wraps to the control's width and can be scrolled
    /// vertically using the mouse wheel. A thin scrollbar is drawn when the
    /// content exceeds the control's height.
    /// </summary>
    public class XNAScrollableTextBlock : XNAPanel
    {
        private const int SCROLL_LINES_PER_NOTCH = 3;
        private const int SCROLLBAR_WIDTH = 3;

        private List<string> lines = new();
        private int scrollOffset = 0;
        private int lastRebuildWidth = -1;

        public XNAScrollableTextBlock(WindowManager windowManager) : base(windowManager)
        {
        }

        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                RebuildLines();
            }
        }

        public int FontIndex { get; set; }

        public int TextXMargin { get; set; } = 3;

        public int TextYPosition { get; set; } = 3;

        public Color ScrollbarColor { get; set; } = new Color(190, 200, 215, 220);

        public Color ScrollbarBackgroundColor { get; set; } = new Color(0, 0, 0, 90);

        private int LineHeight => (int)Math.Ceiling(Renderer.MeasureString("Test", FontIndex).Y) + 1;

        private int VisibleLineCount => Math.Max(1, Height / Math.Max(1, LineHeight));

        private int MaxScrollOffset => Math.Max(0, lines.Count - VisibleLineCount);

        private void RebuildLines()
        {
            lines = new List<string>();

            if (!string.IsNullOrEmpty(Text))
            {
                List<IFont> fonts = Renderer.GetFontList();
                if (fonts.Count > 0)
                {
                    int fontIndex = FontIndex;
                    if (fontIndex < 0)
                        fontIndex = 0;
                    else if (fontIndex >= fonts.Count)
                        fontIndex = fonts.Count - 1;

                    lines = TextParseReturnValue.GetFixedTextLines(fonts[fontIndex],
                        Width - TextXMargin * 2, Text, splitWords: true, keepBlankLines: true);
                }
            }

            lastRebuildWidth = Width;
            ClampScrollOffset();
        }

        private void ClampScrollOffset()
        {
            int maxOffset = MaxScrollOffset;
            if (scrollOffset < 0)
                scrollOffset = 0;
            else if (scrollOffset > maxOffset)
                scrollOffset = maxOffset;
        }

        protected override void OnClientRectangleUpdated()
        {
            base.OnClientRectangleUpdated();

            if (Width != lastRebuildWidth)
                RebuildLines();
            else
                ClampScrollOffset();
        }

        public override void OnMouseScrolled(InputEventArgs inputEventArgs)
        {
            int delta = Cursor.ScrollWheelValue;

            if (delta != 0)
            {
                if (delta < 0)
                    scrollOffset += SCROLL_LINES_PER_NOTCH;
                else
                    scrollOffset -= SCROLL_LINES_PER_NOTCH;

                ClampScrollOffset();
                inputEventArgs.Handled = true;
            }

            base.OnMouseScrolled(inputEventArgs);
        }

        public override void Draw(GameTime gameTime)
        {
            DrawPanel();

            int lineHeight = LineHeight;
            int startLine = Math.Max(0, scrollOffset);
            int endLine = Math.Min(lines.Count, startLine + VisibleLineCount + 1);

            for (int i = startLine; i < endLine; i++)
            {
                int y = TextYPosition + (i - scrollOffset) * lineHeight;
                if (y < 0 || y + lineHeight > Height)
                    continue;

                DrawStringWithShadow(lines[i], FontIndex,
                    new Vector2(TextXMargin, y), UISettings.ActiveSettings.TextColor);
            }

            if (lines.Count * lineHeight > Height)
                DrawScrollbar();

            if (DrawBorders)
                DrawPanelBorders();

            DrawChildren(gameTime);
        }

        private void DrawScrollbar()
        {
            FillRectangle(new Rectangle(Width - SCROLLBAR_WIDTH - 1, 1,
                SCROLLBAR_WIDTH, Height - 2), ScrollbarBackgroundColor);

            int totalLineHeight = Math.Max(1, lines.Count * LineHeight);
            int visible = Math.Max(1, VisibleLineCount);
            int thumbHeight = Math.Max(8, Height * visible / totalLineHeight);
            int maxOffset = MaxScrollOffset;
            int thumbY = maxOffset > 0
                ? (int)((long)(Height - thumbHeight) * scrollOffset / maxOffset)
                : 0;

            FillRectangle(new Rectangle(Width - SCROLLBAR_WIDTH - 1, thumbY,
                SCROLLBAR_WIDTH, thumbHeight), ScrollbarColor);
        }

        public override void Kill()
        {
            base.Kill();
        }
    }
}
