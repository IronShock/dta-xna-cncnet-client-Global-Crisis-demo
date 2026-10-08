using System.Linq;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A scrollable panel that hosts the achievement cells of the achievements
    /// window. Content is added to the inner content panel; its size is derived
    /// from the bounds of the added controls automatically.
    /// </summary>
    public class AchievementsScrollPanel : XNAScrollPanel
    {
        public AchievementsScrollPanel(WindowManager windowManager) : base(windowManager)
        {
            AllowScroll = (false, true);
        }

        /// <summary>
        /// Adds a control to the scrollable content area.
        /// </summary>
        public void AddContent(XNAControl control)
        {
            ContentPanel.AddChild(control);
        }

        /// <summary>
        /// Removes all controls from the scrollable content area.
        /// </summary>
        public void ClearContent()
        {
            foreach (XNAControl control in ContentPanel.Children.ToList())
                ContentPanel.RemoveChild(control);
        }
    }
}
