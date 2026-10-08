using System;

using ClientCore;
using ClientCore.Extensions;
using ClientGUI;

using Microsoft.Xna.Framework;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A placeholder window for campaign sub-sections that have not been
    /// implemented yet (e.g. Database and Achievements). The window's INI file
    /// name is derived from the window's name.
    /// </summary>
    public class PlaceholderWindow : XNAWindow
    {
        private readonly string windowTitle;

        public PlaceholderWindow(WindowManager windowManager, string windowTitle)
            : base(windowManager)
        {
            this.windowTitle = windowTitle;
        }

        public override void Initialize()
        {
            Name = windowTitle;
            BackgroundTexture = AssetLoader.LoadTexture("missionselectorbg.png");
            ClientRectangle = new Rectangle(0, 0, 600, 400);
            BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            var lblTitle = new XNALabel(WindowManager);
            lblTitle.Name = nameof(lblTitle);
            lblTitle.FontIndex = 1;
            lblTitle.ClientRectangle = new Rectangle(12, 12, 0, 0);
            lblTitle.Text = windowTitle;

            var lblComingSoon = new XNALabel(WindowManager);
            lblComingSoon.Name = nameof(lblComingSoon);
            lblComingSoon.ClientRectangle = new Rectangle(12, 60, 0, 0);
            lblComingSoon.Text = "This feature is coming in a future update.".L10N("Client:Main:ComingSoon");

            var btnClose = new XNAClientButton(WindowManager);
            btnClose.Name = nameof(btnClose);
            btnClose.ClientRectangle = new Rectangle(12, Height - 35, UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            btnClose.Text = "Back".L10N("Client:Main:ButtonBack");
            btnClose.LeftClick += BtnClose_LeftClick;

            AddChild(lblTitle);
            AddChild(lblComingSoon);
            AddChild(btnClose);

            base.Initialize();

            CenterOnParent();
        }

        private void BtnClose_LeftClick(object sender, EventArgs e)
        {
            Disable();
        }
    }
}
