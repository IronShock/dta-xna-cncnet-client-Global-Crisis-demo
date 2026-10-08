using System;

using ClientCore;
using ClientCore.Extensions;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A transient card shown after a campaign mission is won. Displays the mission's
    /// banner across the top (aspect-fitted) and the earned medal next to the
    /// "campaign completed" header and the campaign name.
    /// </summary>
    public class CampaignCompletedNotification : SlideNotificationPanel
    {
        private Texture2D banner;
        private Texture2D medal;
        private string campaignName;
        private string headerText;
        private int bannerHeight;

        public CampaignCompletedNotification(WindowManager windowManager)
            : base(windowManager, NotificationType.Campaign)
        {
        }

        /// <summary>
        /// Builds the notification content. Must be called before adding the control.
        /// </summary>
        public void SetCampaign(string campaignName, Texture2D banner, Texture2D medal)
        {
            this.campaignName = campaignName;
            this.banner = banner;
            this.medal = medal;

            headerText = string.IsNullOrEmpty(NotificationSettings.CampaignHeader)
                ? "CAMPAIGN COMPLETED".L10N("Client:Achievements:CampaignCompleted")
                : NotificationSettings.CampaignHeader;

            bannerHeight = 0;
            if (NotificationSettings.ShowBanner && banner != null && banner.Width > 0)
            {
                int width = NotificationSettings.Width;
                float height = (float)width * banner.Height / banner.Width;
                bannerHeight = Math.Min((int)Math.Round(height), NotificationSettings.BannerMaxHeight);
            }

            BackgroundTexture = AssetLoader.CreateTexture(NotificationSettings.BackgroundColor, 1, 1);
            PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            DrawBorders = true;
            BorderColor = NotificationSettings.BorderColor;

            ClientRectangle = new Rectangle(0, 0, NotificationSettings.Width,
                bannerHeight + NotificationSettings.ContentHeight);
        }

        protected override void DrawContent()
        {
            if (bannerHeight > 0)
                DrawImageContained(banner, new Rectangle(0, 0, Width, bannerHeight));

            DrawIconTextContent(medal, headerText, campaignName, null);
        }
    }
}
