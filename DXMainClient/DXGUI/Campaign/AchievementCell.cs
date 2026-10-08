using System;
using System.Collections.Generic;

using ClientCore;
using ClientCore.Extensions;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A single rectangular achievement entry shown in the achievements window:
    /// the achievement icon on the left and its name + description on the right.
    /// Locked achievements are dimmed (darkened icon and grayed text).
    /// </summary>
    public class AchievementCell : XNAPanel
    {
        private const int CELL_PADDING = 12;
        private const int ICON_SIZE = 92;
        private const int TEXT_SPACING = 12;
        private const int NAME_DESC_SPACING = 4;

        private static readonly Color UnlockTimeColor = new Color(80, 200, 120);

        private readonly Texture2D iconTexture;
        private readonly string name;
        private readonly string description;
        private readonly bool isLocked;
        private readonly bool isHidden;
        private readonly DateTime? unlockTime;

        private readonly int nameFontIndex;
        private readonly int descriptionFontIndex;

        private readonly Color nameColor;
        private readonly Color descriptionColor;

        public AchievementCell(WindowManager windowManager, Texture2D icon, string name, string description, bool isLocked, bool isHidden, DateTime? unlockTime)
            : base(windowManager)
        {
            iconTexture = icon;
            this.name = name ?? string.Empty;
            this.description = description ?? string.Empty;
            this.isLocked = isLocked;
            this.isHidden = isHidden;
            this.unlockTime = unlockTime;

            nameFontIndex = 2;
            descriptionFontIndex = 1;

            if (isLocked)
            {
                nameColor = UISettings.ActiveSettings.DisabledItemColor;
                descriptionColor = UISettings.ActiveSettings.DisabledItemColor;
            }
            else
            {
                nameColor = Color.White;
                descriptionColor = new Color(200, 200, 200);
            }
        }

        /// <summary>
        /// Whether this achievement's content is hidden behind a mystery placeholder
        /// (a hidden achievement that has not been unlocked yet).
        /// </summary>
        private bool IsHiddenContent => isHidden && isLocked;

        private string DisplayName => IsHiddenContent ? "???" : name;

        private string DisplayDescription => IsHiddenContent
            ? "Unlock to reveal this achievement.".L10N("Client:Achievements:HiddenHint")
            : description;

        public override void Draw(GameTime gameTime)
        {
            DrawPanel();

            int iconX = CELL_PADDING;
            int iconY = (Height - ICON_SIZE) / 2;

            DrawIcon(iconX, iconY);

            int textX = iconX + ICON_SIZE + TEXT_SPACING;
            int textWidth = Width - textX - CELL_PADDING;

            DrawName(textX, CELL_PADDING, textWidth);
            DrawDescription(textX, CELL_PADDING + GetTextHeight(name, nameFontIndex) + NAME_DESC_SPACING, textWidth);

            DrawUnlockTime(textX, textWidth);

            if (DrawBorders)
                DrawPanelBorders();

            DrawChildren(gameTime);
        }

        private void DrawIcon(int x, int y)
        {
            var iconRect = new Rectangle(x, y, ICON_SIZE, ICON_SIZE);

            if (IsHiddenContent)
            {
                FillRectangle(iconRect, new Color(0, 0, 0, 120));
                DrawRectangleBorders(iconRect);

                string questionMark = "?";
                Vector2 textSize = Renderer.GetTextDimensions(questionMark, nameFontIndex);
                DrawStringWithShadow(questionMark, nameFontIndex,
                    new Vector2(x + (ICON_SIZE - textSize.X) / 2, y + (ICON_SIZE - textSize.Y) / 2),
                    UISettings.ActiveSettings.DisabledItemColor);
                return;
            }

            if (iconTexture != null)
            {
                int availableWidth = ICON_SIZE;
                int availableHeight = ICON_SIZE;
                float scale = Math.Min(1.0f,
                    Math.Min((float)availableWidth / iconTexture.Width,
                             (float)availableHeight / iconTexture.Height));
                int drawWidth = (int)(iconTexture.Width * scale);
                int drawHeight = (int)(iconTexture.Height * scale);
                int drawX = x + (ICON_SIZE - drawWidth) / 2;
                int drawY = y + (ICON_SIZE - drawHeight) / 2;

                DrawTexture(iconTexture, new Rectangle(drawX, drawY, drawWidth, drawHeight), Color.White);

                if (isLocked)
                    FillRectangle(iconRect, new Color(0, 0, 0, 150));
            }
            else
            {
                FillRectangle(iconRect, new Color(0, 0, 0, 120));
            }

            DrawRectangleBorders(iconRect);
        }

        private void DrawName(int x, int y, int maxWidth)
        {
            string limited = Renderer.GetStringWithLimitedWidth(DisplayName, nameFontIndex, maxWidth);
            DrawStringWithShadow(limited, nameFontIndex, new Vector2(x, y), nameColor);
        }

        private void DrawDescription(int x, int y, int maxWidth)
        {
            if (string.IsNullOrEmpty(DisplayDescription))
                return;

            List<string> lines = Renderer.GetFixedTextLines(DisplayDescription, descriptionFontIndex, maxWidth);

            int lineHeight = GetTextHeight("A", descriptionFontIndex);
            int availableHeight = Height - y - CELL_PADDING;

            // Leave room for the unlock time line at the bottom-right of the cell.
            if (!isLocked && unlockTime.HasValue)
                availableHeight -= lineHeight;

            int maxLines = Math.Max(1, availableHeight / lineHeight);

            for (int i = 0; i < lines.Count && i < maxLines; i++)
                DrawStringWithShadow(lines[i], descriptionFontIndex,
                    new Vector2(x, y + i * lineHeight), descriptionColor);
        }

        /// <summary>
        /// Draws the unlock date/time in green at the bottom-right of the cell for
        /// unlocked achievements that have a recorded unlock moment.
        /// </summary>
        private void DrawUnlockTime(int x, int maxWidth)
        {
            if (isLocked || !unlockTime.HasValue)
                return;

            string text = "Unlocked: ".L10N("Client:Achievements:Unlocked")
                + unlockTime.Value.ToString("yyyy-MM-dd HH:mm");
            Vector2 textSize = Renderer.GetTextDimensions(text, descriptionFontIndex);
            int lineHeight = GetTextHeight("A", descriptionFontIndex);

            int textX = x + maxWidth - (int)textSize.X;
            int textY = Height - CELL_PADDING - lineHeight;

            DrawStringWithShadow(text, descriptionFontIndex, new Vector2(textX, textY), UnlockTimeColor);
        }

        private int GetTextHeight(string text, int fontIndex) =>
            (int)Renderer.GetTextDimensions(text, fontIndex).Y;

        private void DrawRectangleBorders(Rectangle rect)
        {
            Color borderColor = UISettings.ActiveSettings.PanelBorderColor;
            FillRectangle(new Rectangle(rect.X, rect.Y, rect.Width, 1), borderColor);
            FillRectangle(new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), borderColor);
            FillRectangle(new Rectangle(rect.X, rect.Y, 1, rect.Height), borderColor);
            FillRectangle(new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), borderColor);
        }

        public override void Kill()
        {
            // The icon texture is owned by the achievements window; dispose it there.
            base.Kill();
        }
    }
}
