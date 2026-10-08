using ClientCore;
using ClientCore.Extensions;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A transient card shown when an achievement is unlocked, styled like the common
    /// Steam / PlayStation toast: the achievement icon next to a small header and the
    /// achievement name (and its description as a muted sub-line).
    /// </summary>
    public class AchievementUnlockNotification : SlideNotificationPanel
    {
        private Texture2D icon;
        private string name;
        private string description;
        private string headerText;
        private bool showIcon = true;

        public AchievementUnlockNotification(WindowManager windowManager)
            : base(windowManager, NotificationType.Achievement)
        {
        }

        /// <summary>
        /// Builds the notification content for a single unlocked achievement.
        /// Must be called before adding the control.
        /// </summary>
        public void SetAchievement(Texture2D icon, string name, string description)
        {
            this.icon = icon;
            this.name = name;
            this.description = description;
            showIcon = true;

            SetCommonContent();
        }

        /// <summary>
        /// Builds a compact "and N more" summary card shown when more achievements were
        /// unlocked than fit on screen.
        /// </summary>
        public void SetMore(int count)
        {
            icon = null;
            description = null;
            showIcon = false;
            name = string.Format("...and {0} more".L10N("Client:Achievements:MoreItems"), count);

            SetCommonContent();
        }

        private void SetCommonContent()
        {
            headerText = string.IsNullOrEmpty(NotificationSettings.AchievementHeader)
                ? "ACHIEVEMENT UNLOCKED".L10N("Client:Achievements:UnlockedPopup")
                : NotificationSettings.AchievementHeader;

            BackgroundTexture = AssetLoader.CreateTexture(NotificationSettings.BackgroundColor, 1, 1);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            DrawBorders = true;
            BorderColor = NotificationSettings.BorderColor;

            ClientRectangle = new Rectangle(0, 0, NotificationSettings.Width, NotificationSettings.ContentHeight);
        }

        protected override void DrawContent()
        {
            DrawIconTextContent(icon, headerText, name, description, showIcon);
        }
    }
}
