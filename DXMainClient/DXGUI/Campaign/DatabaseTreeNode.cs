using System;
using System.Collections.Generic;

using ClientCore;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A single node of the Database's collapsible table-of-contents tree.
    /// Category nodes can be expanded/collapsed to show or hide their children;
    /// article nodes load their content when clicked.
    /// All visual settings (colors, fonts, row size, indent, arrow glyphs) can be
    /// overridden through INI attributes, and are typically set from the Database
    /// window's [TreePanel] section.
    /// </summary>
    public class DatabaseTreeNode : XNAControl
    {
        private const int DEFAULT_INDENT_PER_DEPTH = 18;
        private const int DEFAULT_ARROW_WIDTH = 14;
        private const int DEFAULT_ROW_HEIGHT = 24;
        private const int DEFAULT_FONT_INDEX = 1;

        private static readonly Color DefaultIdleColor = new Color(40, 40, 40, 220);
        private static readonly Color DefaultHoverColor = new Color(60, 60, 60, 240);
        private static readonly Color DefaultSelectedColor = new Color(85, 85, 85, 240);
        private static readonly Color DefaultTextColor = new Color(230, 230, 230, 255);

        private bool isHovered;

        public DatabaseTreeNode(WindowManager windowManager) : base(windowManager)
        {
            InputEnabled = true;
            DrawMode = ControlDrawMode.UNIQUE_RENDER_TARGET;
        }

        /// <summary>
        /// The node's display text.
        /// </summary>
        public string NodeText { get; set; }

        /// <summary>
        /// True for category (folder) nodes that can be expanded/collapsed.
        /// </summary>
        public bool IsCategory { get; set; }

        /// <summary>
        /// The node's nesting depth (0 = root level).
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Whether this category node is currently expanded.
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Child nodes (only used for category nodes).
        /// </summary>
        public List<DatabaseTreeNode> Children { get; } = new();

        /// <summary>
        /// Raised when a category node is expanded or collapsed.
        /// </summary>
        public event EventHandler Toggled;

        /// <summary>
        /// Raised when an article (leaf) node is clicked.
        /// </summary>
        public event EventHandler Selected;

        /// <summary>
        /// The INI section name this node was loaded from.
        /// </summary>
        public string SectionName { get; set; }

        /// <summary>
        /// The raw INI section holding this node's article content.
        /// </summary>
        public IniSection ContentSection { get; set; }

        /// <summary>
        /// Whether this node is currently highlighted as the selected article.
        /// </summary>
        public bool SelectedFlag { get; set; }

        /// <summary>
        /// Horizontal offset (in pixels) added per nesting depth.
        /// </summary>
        public int IndentPerDepth { get; set; } = DEFAULT_INDENT_PER_DEPTH;

        /// <summary>
        /// The width reserved for the expand/collapse arrow.
        /// </summary>
        public int ArrowWidth { get; set; } = DEFAULT_ARROW_WIDTH;

        /// <summary>
        /// The height of a single node row.
        /// </summary>
        public int RowHeight { get; set; } = DEFAULT_ROW_HEIGHT;

        /// <summary>
        /// The font used to draw the node text and arrows.
        /// </summary>
        public int FontIndex { get; set; } = DEFAULT_FONT_INDEX;

        public Color IdleColor { get; set; } = DefaultIdleColor;

        public Color HoverColor { get; set; } = DefaultHoverColor;

        public Color SelectedColor { get; set; } = DefaultSelectedColor;

        public Color TextColor { get; set; } = DefaultTextColor;

        /// <summary>
        /// The glyph shown for an expanded category node.
        /// </summary>
        public string ExpandedArrow { get; set; } = "v";

        /// <summary>
        /// The glyph shown for a collapsed category node.
        /// </summary>
        public string CollapsedArrow { get; set; } = ">";

        public void SetRowSize()
        {
            ClientRectangle = new Rectangle(X, Y, Width, RowHeight);
        }

        protected override void ParseControlINIAttribute(IniFile iniFile, string key, string value)
        {
            switch (key)
            {
                case "IndentPerDepth":
                    IndentPerDepth = Conversions.IntFromString(value, IndentPerDepth);
                    return;
                case "ArrowWidth":
                    ArrowWidth = Conversions.IntFromString(value, ArrowWidth);
                    return;
                case "RowHeight":
                    RowHeight = Conversions.IntFromString(value, RowHeight);
                    return;
                case "FontIndex":
                    FontIndex = Conversions.IntFromString(value, FontIndex);
                    return;
                case "IdleColor":
                    IdleColor = AssetLoader.GetColorFromString(value);
                    return;
                case "HoverColor":
                    HoverColor = AssetLoader.GetColorFromString(value);
                    return;
                case "SelectedColor":
                    SelectedColor = AssetLoader.GetColorFromString(value);
                    return;
                case "TextColor":
                    TextColor = AssetLoader.GetColorFromString(value);
                    return;
                case "ExpandedArrow":
                    ExpandedArrow = value;
                    return;
                case "CollapsedArrow":
                    CollapsedArrow = value;
                    return;
            }

            base.ParseControlINIAttribute(iniFile, key, value);
        }

        public override void OnMouseEnter()
        {
            base.OnMouseEnter();
            isHovered = true;
        }

        public override void OnMouseLeave()
        {
            base.OnMouseLeave();
            isHovered = false;
        }

        public override void OnLeftClick(InputEventArgs inputEventArgs)
        {
            base.OnLeftClick(inputEventArgs);

            if (IsCategory)
            {
                IsExpanded = !IsExpanded;
                Toggled?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Selected?.Invoke(this, EventArgs.Empty);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            int indent = Depth * IndentPerDepth;
            int arrowX = indent;
            int textX = indent + ArrowWidth + 6;

            Rectangle rowRect = new Rectangle(0, 0, Width, RowHeight);

            if (SelectedFlag)
            {
                FillRectangle(rowRect, SelectedColor);
            }
            else if (isHovered)
            {
                FillRectangle(rowRect, HoverColor);
            }
            else
            {
                FillRectangle(rowRect, IdleColor);
            }

            if (IsCategory)
            {
                DrawString(IsExpanded ? ExpandedArrow : CollapsedArrow, FontIndex,
                    new Vector2(arrowX, 4), TextColor);
            }

            string displayText = NodeText ?? string.Empty;
            DrawStringWithShadow(displayText, FontIndex,
                new Vector2(textX, 4), TextColor);
        }
    }
}
