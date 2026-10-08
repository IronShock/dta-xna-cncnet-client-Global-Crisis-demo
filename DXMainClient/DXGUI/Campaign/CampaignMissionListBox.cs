using System;
using System.Collections.Generic;
using System.IO;

using ClientCore;

using DTAClient.Domain;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using SixLabors.ImageSharp.Processing;

using Image = SixLabors.ImageSharp.Image;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A list box that renders each mission as a large image card with the
    /// mission name overlaid on a semi-transparent bar at the bottom of the card.
    /// Also renders sub-chapter group cards used by the campaign selector's group
    /// navigation.
    /// </summary>
    public class CampaignMissionListBox : XNAListBox
    {
        /// <summary>
        /// The height reserved at the bottom of each card for the mission name bar.
        /// </summary>
        private const int TEXT_BAR_HEIGHT = 26;

        private const int CARD_MARGIN = 2;

        private const int BADGE_SIZE = 34;

        /// <summary>
        /// Gap kept between the victory badge / lock icon and both the card edge and
        /// the mission icon, so the badge never touches the image.
        /// </summary>
        private const int BADGE_OFFSET = 4;

        /// <summary>
        /// Width of the vertical strip reserved on the left of each card so the
        /// victory badge / lock icon can sit there without overlapping the image.
        /// </summary>
        private const int BADGE_GUTTER_WIDTH = BADGE_SIZE + BADGE_OFFSET * 2;

        /// <summary>
        /// Vertical padding kept free above and below the SideName prefix icon.
        /// </summary>
        private const int SIDE_ICON_VERTICAL_PADDING = 4;

        /// <summary>
        /// Maximum width of the SideName prefix icon so it cannot crowd out the name.
        /// </summary>
        private const int MAX_SIDE_ICON_WIDTH = 48;

        private Color hoverOnItemColor;

        /// <summary>
        /// The highest difficulty won per mission code name, used to draw a victory
        /// badge on the top-left corner of a mission's card (0 = bronze, 1 = silver,
        /// 2 = gold, 3 = medal). Set by the campaign selector.
        /// </summary>
        public Dictionary<string, int> MissionBestDifficulty { get; set; }

        /// <summary>
        /// Returns whether the given mission is currently locked (its <c>NeedUnlock</c>
        /// globals are not all satisfied). Locked missions show a lock icon on their
        /// card. Set by the campaign selector.
        /// </summary>
        public Func<Mission, bool> IsMissionLocked { get; set; }

        private Texture2D bronzeStarTexture;
        private Texture2D silverStarTexture;
        private Texture2D goldStarTexture;
        private Texture2D medalTexture;
        private Texture2D lockTexture;

        /// <summary>
        /// Cache of SideName prefix icons keyed by the raw SideName string
        /// (<see cref="Mission.IconPath"/>). A null value means the icon was not found.
        /// </summary>
        private readonly Dictionary<string, Texture2D> sideIconCache = new();

        public CampaignMissionListBox(WindowManager windowManager) : base(windowManager)
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            hoverOnItemColor = AssetLoader.GetColorFromString(
                ClientConfiguration.Instance.HoverOnGameColor);

            LoadBadgeTextures();

            if (AssetLoader.AssetExists("lockedgame.png"))
                lockTexture = AssetLoader.LoadTexture("lockedgame.png");
        }

        private void LoadBadgeTextures()
        {
            bronzeStarTexture = LoadBadgeTexture("bronzestar.png");
            silverStarTexture = LoadBadgeTexture("silverstar.png");
            goldStarTexture = LoadBadgeTexture("goldstar.png");
            medalTexture = LoadBadgeTexture("medal.png");
        }

        private static Texture2D LoadBadgeTexture(string fileName)
        {
            string relativePath = SafePath.CombineFilePath("CampaignBadges", fileName);
            if (!AssetLoader.AssetExists(relativePath))
                return null;

            // The badge sources are large (256x256), so downscale them once here with
            // a high-quality resampler and draw them 1:1 afterwards. Runtime scaling of
            // such a large image to a tiny badge would look blurry or aliased.
            foreach (string searchPath in AssetLoader.AssetSearchPaths)
            {
                FileInfo fileInfo = SafePath.GetFile(searchPath, relativePath);
                if (!fileInfo.Exists)
                    continue;

                using FileStream fs = fileInfo.OpenRead();
                using Image image = Image.Load(fs);
                image.Mutate(x => x.Resize(BADGE_SIZE, BADGE_SIZE, KnownResamplers.Lanczos3));
                return AssetLoader.TextureFromImage(image);
            }

            return null;
        }

        /// <summary>
        /// Returns the small icon drawn to the left of a mission's name in its card's
        /// bottom bar, loaded from "&lt;SideName&gt;icon.png". Returns null when the mission
        /// has no SideName or the icon file does not exist. Results are cached by SideName.
        /// </summary>
        private Texture2D GetSideIcon(Mission mission)
        {
            if (string.IsNullOrEmpty(mission.IconPath))
                return null;

            if (sideIconCache.TryGetValue(mission.IconPath, out Texture2D cached))
                return cached;

            string iconName = mission.IconPath + "icon.png";
            Texture2D texture = AssetLoader.AssetExists(iconName)
                ? AssetLoader.LoadTexture(iconName)
                : null;

            sideIconCache[mission.IconPath] = texture;
            return texture;
        }

        private int GetScrollbarWidth() => EnableScrollbar && ScrollBar.IsDrawn() ? ScrollBar.Width : 0;

        /// <summary>
        /// Each mission is a single fixed-height card. Returning a constant line
        /// count keeps every row exactly one <see cref="XNAListBox.LineHeight"/> tall,
        /// regardless of how the mission name wraps, so cards never inflate in height.
        /// </summary>
        protected override int GetListBoxItemLineCount(XNAListBoxItem listBoxItem) => 1;

        protected override void DrawListBoxItem(int index, int y)
        {
            XNAListBoxItem lbItem = Items[index];

            int rowHeight = GetListBoxItemLineCount(lbItem) * LineHeight;
            int drawnWidth = Width - 2 - GetScrollbarWidth();

            // Missions and sub-chapter groups share the same card style.
            DrawMissionCard(lbItem, index, y, rowHeight, drawnWidth);
        }

        private void DrawMissionCard(XNAListBoxItem lbItem, int index, int y, int rowHeight, int drawnWidth)
        {
            bool isLocked = lbItem.Tag is Mission mission && IsMissionLocked != null && IsMissionLocked(mission);

            if (index == SelectedIndex)
            {
                FillRectangle(new Rectangle(1, y, drawnWidth, rowHeight), FocusColor);
            }
            else if (index == HoveredIndex)
            {
                FillRectangle(new Rectangle(1, y, drawnWidth, rowHeight), hoverOnItemColor);
            }

            int imageAreaHeight = rowHeight - TEXT_BAR_HEIGHT;

            if (lbItem.Texture != null)
            {
                int textureWidth = lbItem.Texture.Width;
                int textureHeight = lbItem.Texture.Height;

                int contentLeft = BADGE_GUTTER_WIDTH;
                int contentWidth = drawnWidth - contentLeft - CARD_MARGIN;

                float scaleX = (float)contentWidth / textureWidth;
                float scaleY = (float)(imageAreaHeight - CARD_MARGIN * 2) / textureHeight;
                float scale = Math.Min(scaleX, scaleY);

                int drawWidth = (int)(textureWidth * scale);
                int drawHeight = (int)(textureHeight * scale);

                int drawX = contentLeft;
                int drawY = y + CARD_MARGIN + (imageAreaHeight - CARD_MARGIN * 2 - drawHeight) / 2;

                DrawTexture(lbItem.Texture,
                    new Rectangle(drawX, drawY, drawWidth, drawHeight), Color.White);

                // Darken the card of a locked mission so it clearly reads as unavailable.
                if (isLocked)
                {
                    FillRectangle(new Rectangle(drawX, drawY, drawWidth, drawHeight),
                        new Color(0, 0, 0, 180));
                }
            }

            DrawVictoryBadge(lbItem, y);
            DrawLockIcon(lbItem, y);

            int barY = y + rowHeight - TEXT_BAR_HEIGHT;
            FillRectangle(new Rectangle(1, barY, drawnWidth, TEXT_BAR_HEIGHT),
                new Color(0, 0, 0, 170));

            Texture2D sideIcon = lbItem.Tag is Mission sideMission ? GetSideIcon(sideMission) : null;

            // Draw the SideName icon aligned to the far left of the name bar. The text
            // keeps its original centered position and is unaffected by the icon.
            if (sideIcon != null)
            {
                float iconScale = Math.Min(1.0f, Math.Min(
                    (float)(TEXT_BAR_HEIGHT - SIDE_ICON_VERTICAL_PADDING * 2) / sideIcon.Height,
                    (float)MAX_SIDE_ICON_WIDTH / sideIcon.Width));
                int iconWidth = (int)(sideIcon.Width * iconScale);
                int iconHeight = (int)(sideIcon.Height * iconScale);

                DrawTexture(sideIcon,
                    new Rectangle(CARD_MARGIN, barY + (TEXT_BAR_HEIGHT - iconHeight) / 2, iconWidth, iconHeight),
                    Color.White);
            }

            string text = lbItem.Text;
            string limitedText = Renderer.GetStringWithLimitedWidth(text, FontIndex, drawnWidth - CARD_MARGIN * 2);
            Vector2 textSize = Renderer.GetTextDimensions(limitedText, FontIndex);
            int textX = CARD_MARGIN + (drawnWidth - CARD_MARGIN * 2 - (int)textSize.X) / 2;
            int textY = barY + (TEXT_BAR_HEIGHT - (int)textSize.Y) / 2;

            DrawStringWithShadow(limitedText, FontIndex,
                new Vector2(textX, textY), Color.White);
        }

        /// <summary>
        /// Draws the victory badge (bronze/silver/gold star or medal) on the top-left
        /// corner of a mission card when the mission has been cleared. The badge for
        /// the highest difficulty cleared is shown. Group cards and missions without
        /// progress are left untouched.
        /// </summary>
        private void DrawVictoryBadge(XNAListBoxItem lbItem, int y)
        {
            if (!(lbItem.Tag is Mission mission) || MissionBestDifficulty == null)
                return;

            if (!MissionBestDifficulty.TryGetValue(mission.CodeName, out int difficulty))
                return;

            Texture2D badge = difficulty switch
            {
                0 => bronzeStarTexture,
                1 => silverStarTexture,
                2 => goldStarTexture,
                3 => medalTexture,
                _ => null
            };

            if (badge == null)
                return;

            // The badge texture was pre-scaled to BADGE_SIZE at load time, so draw it
            // 1:1 without any runtime scaling (which would blur it).
            DrawTexture(badge,
                new Rectangle(BADGE_OFFSET, y + BADGE_OFFSET, BADGE_SIZE, BADGE_SIZE), Color.White);
        }

        /// <summary>
        /// Draws a lock icon on the top-left corner of a mission card when the mission
        /// is currently locked (its <c>NeedUnlock</c> globals are not all satisfied).
        /// </summary>
        private void DrawLockIcon(XNAListBoxItem lbItem, int y)
        {
            if (lockTexture == null || IsMissionLocked == null)
                return;

            if (!(lbItem.Tag is Mission mission) || !IsMissionLocked(mission))
                return;

            float scale = Math.Min(1.0f,
                Math.Min((float)BADGE_SIZE / lockTexture.Width, (float)BADGE_SIZE / lockTexture.Height));
            int drawWidth = (int)(lockTexture.Width * scale);
            int drawHeight = (int)(lockTexture.Height * scale);

            Renderer.PushSettings(new SpriteBatchSettings(
                SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null));

            DrawTexture(lockTexture,
                new Rectangle(BADGE_OFFSET, y + BADGE_OFFSET, drawWidth, drawHeight), Color.White);

            Renderer.PopSettings();
        }
    }
}
