using ClientGUI;

using Microsoft.Xna.Framework;

using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// Shared visual defaults for the campaign-related user interface.
    /// </summary>
    internal static class CampaignButtonDefaults
    {
        /// <summary>
        /// The default flat button texture used by the campaign interface: a dark
        /// semi-transparent idle color and a lighter hover color. Applied when the
        /// button does not specify custom IdleTexture/HoverTexture through its INI
        /// configuration.
        /// </summary>
        internal static void ApplyDefaultButtonTexture(XNAClientButton button)
        {
            button.IdleTexture = AssetLoader.CreateTexture(new Color(32, 32, 32, 235), 1, 1);
            button.HoverTexture = AssetLoader.CreateTexture(new Color(64, 64, 64, 235), 1, 1);
        }
    }
}
