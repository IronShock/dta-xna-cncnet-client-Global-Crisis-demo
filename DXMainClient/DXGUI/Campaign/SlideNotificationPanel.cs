using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A transient notification card that slides in from the right edge of the window,
    /// stays for a few seconds, then slides out and removes itself. Campaign cards are
    /// pinned to the top; achievement cards stack below them. The visual style follows
    /// the common Steam / PlayStation toast: a slim dark card with an accent bar, an
    /// aspect-fitted icon and a header + name text block.
    /// </summary>
    public abstract class SlideNotificationPanel : XNAPanel
    {
        public enum NotificationType
        {
            Campaign,
            Achievement
        }

        private static readonly List<SlideNotificationPanel> activeNotifications = new();

        private double slideProgress;
        private double holdRemaining;
        private bool slidingIn;
        private bool slidingOut;
        private bool started;
        private int slotY;

        protected SlideNotificationPanel(WindowManager windowManager, NotificationType notificationType)
            : base(windowManager)
        {
            NotificationSettings.EnsureLoaded();
            SlotType = notificationType;
        }

        protected NotificationType SlotType { get; }

        protected Color AccentColor => SlotType == NotificationType.Campaign
            ? NotificationSettings.AccentCampaignColor
            : NotificationSettings.AccentAchievementColor;

        /// <summary>
        /// Begins the slide-in -> hold -> slide-out -> remove sequence.
        /// </summary>
        public void Start()
        {
            if (SlotType == NotificationType.Campaign)
                activeNotifications.Insert(0, this);
            else
                activeNotifications.Add(this);

            started = true;
            slidingIn = true;
            slidingOut = false;
            holdRemaining = NotificationSettings.HoldDuration;
            slideProgress = 0.0;

            RecalculateSlots();
            Enable();
        }

        public override void Update(GameTime gameTime)
        {
            if (!started)
            {
                base.Update(gameTime);
                return;
            }

            double dt = gameTime.ElapsedGameTime.TotalSeconds;

            if (slidingIn)
            {
                slideProgress += dt / NotificationSettings.SlideDuration;
                if (slideProgress >= 1.0)
                {
                    slideProgress = 1.0;
                    slidingIn = false;
                }
                PositionPanel();
            }
            else if (!slidingOut)
            {
                holdRemaining -= dt;
                if (holdRemaining <= 0.0)
                    slidingOut = true;
            }
            else
            {
                slideProgress -= dt / NotificationSettings.SlideDuration;
                if (slideProgress <= 0.0)
                {
                    slideProgress = 0.0;
                    activeNotifications.Remove(this);
                    RecalculateSlots();
                    WindowManager.RemoveControl(this);
                    base.Update(gameTime);
                    return;
                }
                PositionPanel();
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// Repositions every active notification from the top, using each card's own
        /// height so variable-height campaign cards (with a banner) stack correctly.
        /// </summary>
        private static void RecalculateSlots()
        {
            int y = NotificationSettings.TopMargin;
            foreach (SlideNotificationPanel notification in activeNotifications)
            {
                notification.slotY = y;
                y += notification.Height + NotificationSettings.SlotGap;
                notification.PositionPanel();
            }
        }

        private void PositionPanel()
        {
            int screenWidth = WindowManager.RenderResolutionX;
            int hiddenX = screenWidth;
            int shownX = screenWidth - Width - NotificationSettings.RightMargin;
            int x = hiddenX + (int)((shownX - hiddenX) * slideProgress);
            ClientRectangle = new Rectangle(x, slotY, Width, Height);
        }

        public override void Draw(GameTime gameTime)
        {
            DrawPanel();
            DrawContent();
            DrawAccentBar();

            if (DrawBorders)
                DrawPanelBorders();
        }

        /// <summary>
        /// Draws the card-specific content. Implemented by the concrete notifications.
        /// </summary>
        protected abstract void DrawContent();

        protected void DrawAccentBar()
        {
            if (NotificationSettings.AccentBarWidth <= 0)
                return;

            FillRectangle(new Rectangle(0, 0, NotificationSettings.AccentBarWidth, Height), AccentColor);
        }

        /// <summary>
        /// Draws an icon aspect-fitted and centered inside <paramref name="box"/>.
        /// Small textures are drawn with point sampling so pixel art stays crisp.
        /// </summary>
        protected void DrawIconFitted(Texture2D texture, Rectangle box, Color tint)
        {
            if (texture == null || texture.Width <= 0 || texture.Height <= 0)
                return;

            float scale = Math.Min((float)box.Width / texture.Width, (float)box.Height / texture.Height);
            int width = Math.Max(1, (int)(texture.Width * scale));
            int height = Math.Max(1, (int)(texture.Height * scale));
            var destination = new Rectangle(
                box.X + (box.Width - width) / 2,
                box.Y + (box.Height - height) / 2,
                width, height);

            bool pointFilter = NotificationSettings.PointFilterIcons && (texture.Width <= 64 || texture.Height <= 64);
            if (pointFilter)
                Renderer.PushSettings(new SpriteBatchSettings(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null));

            DrawTexture(texture, destination, tint);

            if (pointFilter)
                Renderer.PopSettings();
        }

        /// <summary>
        /// Draws an image aspect-fitted (contained) and centered inside <paramref name="area"/>.
        /// </summary>
        protected void DrawImageContained(Texture2D texture, Rectangle area)
        {
            if (texture == null || texture.Width <= 0 || texture.Height <= 0)
                return;

            float scale = Math.Min((float)area.Width / texture.Width, (float)area.Height / texture.Height);
            int width = Math.Max(1, (int)(texture.Width * scale));
            int height = Math.Max(1, (int)(texture.Height * scale));
            var destination = new Rectangle(
                area.X + (area.Width - width) / 2,
                area.Y + (area.Height - height) / 2,
                width, height);

            DrawTexture(texture, destination, Color.White);
        }

        /// <summary>
        /// Draws the standard "icon + header + name (+ sub)" content row, vertically
        /// centered inside the card's content area (below any banner).
        /// </summary>
        protected void DrawIconTextContent(Texture2D icon, string header, string name, string sub, bool showIcon = true)
        {
            int contentTop = Height - NotificationSettings.ContentHeight;
            int textX = NotificationSettings.Padding;

            if (showIcon)
            {
                int iconY = contentTop + (NotificationSettings.ContentHeight - NotificationSettings.IconSize) / 2;
                DrawIconFitted(icon, new Rectangle(NotificationSettings.Padding, iconY,
                    NotificationSettings.IconSize, NotificationSettings.IconSize), Color.White);

                textX = NotificationSettings.Padding + NotificationSettings.IconSize + NotificationSettings.IconGap;
            }

            int maxWidth = Width - textX - NotificationSettings.Padding;
            if (maxWidth <= 0)
                return;

            int stackHeight = MeasureTextStackHeight(header, name, sub);
            int stackTop = contentTop + (NotificationSettings.ContentHeight - stackHeight) / 2;

            DrawTextStack(header, name, sub, textX, maxWidth, stackTop);
        }

        private int MeasureTextStackHeight(string header, string name, string sub)
        {
            int height = 0;

            if (!string.IsNullOrEmpty(header))
                height += (int)Renderer.GetTextDimensions(header, NotificationSettings.HeaderFontIndex).Y;
            if (!string.IsNullOrEmpty(name))
                height += (int)Renderer.GetTextDimensions(name, NotificationSettings.NameFontIndex).Y;
            if (!string.IsNullOrEmpty(sub))
                height += (int)Renderer.GetTextDimensions(sub, NotificationSettings.SubFontIndex).Y;

            return height;
        }

        private void DrawTextStack(string header, string name, string sub, int x, int maxWidth, int top)
        {
            int y = top;

            if (!string.IsNullOrEmpty(header))
            {
                string text = Renderer.GetStringWithLimitedWidth(header, NotificationSettings.HeaderFontIndex, maxWidth);
                DrawStringWithShadow(text, NotificationSettings.HeaderFontIndex, new Vector2(x, y), AccentColor);
                y += (int)Renderer.GetTextDimensions(text, NotificationSettings.HeaderFontIndex).Y;
            }

            if (!string.IsNullOrEmpty(name))
            {
                string text = Renderer.GetStringWithLimitedWidth(name, NotificationSettings.NameFontIndex, maxWidth);
                DrawStringWithShadow(text, NotificationSettings.NameFontIndex, new Vector2(x, y), Color.White);
                y += (int)Renderer.GetTextDimensions(text, NotificationSettings.NameFontIndex).Y;
            }

            if (!string.IsNullOrEmpty(sub))
            {
                string text = Renderer.GetStringWithLimitedWidth(sub, NotificationSettings.SubFontIndex, maxWidth);
                DrawStringWithShadow(text, NotificationSettings.SubFontIndex, new Vector2(x, y), UISettings.ActiveSettings.SubtleTextColor);
            }
        }
    }
}
