using System;
using System.Collections.Generic;
using System.IO;

using ClientCore;
using ClientCore.Extensions;
using ClientGUI;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// A full-screen achievements window showing a scrollable two-column grid of
    /// achievement cells (icon on the left, name and description on the right).
    /// Achievements are defined in an INI file, one section per achievement. The
    /// window layout is editable through AchievementsWindow.ini (window attributes,
    /// [lblTitle] / [scrollPanel] / [lblNoAchievements] / [btnClose] control sections
    /// and an [AchievementsGrid] section for the cell metrics).
    /// </summary>
    public class AchievementsWindow : XNAWindow
    {
        private const int TITLE_X = 40;
        private const int TITLE_Y = 26;
        private const int GRID_X = 40;
        private const int GRID_Y = 78;
        private const int GRID_SIDE_MARGIN = 40;
        private const int GRID_BOTTOM_MARGIN = 70;
        private const int DEFAULT_CELL_HEIGHT = 120;
        private const int DEFAULT_CELL_COLUMN_GAP = 20;
        private const int DEFAULT_CELL_ROW_GAP = 16;

        private const string ACHIEVEMENTS_GRID_SECTION = "AchievementsGrid";

        private readonly string windowTitle;

        private XNALabel lblTitle;
        private AchievementsScrollPanel scrollPanel;
        private XNAClientButton btnClose;
        private XNALabel lblNoAchievements;

        private string achievementsIniPath;
        private readonly List<Texture2D> loadedTextures = new();

        // Cell metrics, editable via the [AchievementsGrid] section of the window INI.
        private int cellHeight = DEFAULT_CELL_HEIGHT;
        private int cellColumnGap = DEFAULT_CELL_COLUMN_GAP;
        private int cellRowGap = DEFAULT_CELL_ROW_GAP;

        private const string UNLOCKED_ACHIEVEMENTS_FILE = "Client/UnlockedAchievements.ini";

        // Global variables the client has detected as set after won missions. Used to
        // resolve achievements whose NeedUnlock key matches one of these names.
        private readonly HashSet<string> unlockedGlobals = new(StringComparer.OrdinalIgnoreCase);

        // Achievement section name -> the moment it was first unlocked.
        private readonly Dictionary<string, DateTime> unlockedAchievements = new(StringComparer.OrdinalIgnoreCase);

        public AchievementsWindow(WindowManager windowManager, string windowTitle)
            : base(windowManager)
        {
            this.windowTitle = windowTitle;
            Name = windowTitle;
        }

        public override void Initialize()
        {
            // Default to a full-screen window. AchievementsWindow.ini Size/BackgroundTexture
            // override these once base.Initialize() reads the file.
            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), Width, Height);
            BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            achievementsIniPath = SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "Achievements.ini");

            CreateControls();

            // Reads AchievementsWindow.ini and applies the [AchievementsWindow]
            // attributes to this window and every [<ControlName>] section to the
            // matching control.
            base.Initialize();

            LoadGridSettings();

            LoadUnlockedGlobals();
            LoadUnlockedAchievements();
            LoadAchievements();

            // Reload the unlocked globals and rebuild the grid each time the window is
            // shown, so achievements unlocked during this session are reflected.
            VisibleChanged += AchievementsWindow_VisibleChanged;

            CenterOnParent();
        }

        /// <summary>
        /// Reads the achievement grid cell metrics from the [AchievementsGrid] section
        /// of the window INI (mirroring the campaign selector's custom INI sections).
        /// </summary>
        private void LoadGridSettings()
        {
            if (ThemeIni == null || !ThemeIni.SectionExists(ACHIEVEMENTS_GRID_SECTION))
                return;

            cellHeight = ThemeIni.GetIntValue(ACHIEVEMENTS_GRID_SECTION, "CellHeight", cellHeight);
            cellColumnGap = ThemeIni.GetIntValue(ACHIEVEMENTS_GRID_SECTION, "ColumnGap", cellColumnGap);
            cellRowGap = ThemeIni.GetIntValue(ACHIEVEMENTS_GRID_SECTION, "RowGap", cellRowGap);
        }

        private void AchievementsWindow_VisibleChanged(object sender, EventArgs e)
        {
            if (!Visible)
                return;

            LoadUnlockedGlobals();
            LoadUnlockedAchievements();
            LoadAchievements();
        }

        /// <summary>
        /// Loads the set of global variables the client has detected as satisfied from
        /// the [Globals] section of the unlocked achievements file.
        /// </summary>
        private void LoadUnlockedGlobals()
        {
            unlockedGlobals.Clear();

            string path = SafePath.CombineFilePath(ProgramConstants.GamePath, UNLOCKED_ACHIEVEMENTS_FILE);
            if (!File.Exists(path))
                return;

            try
            {
                var globalsIni = new IniFile(path);
                List<string> keys = globalsIni.GetSectionKeys("Globals");
                if (keys == null)
                    return;

                foreach (string key in keys)
                    unlockedGlobals.Add(key);
            }
            catch (Exception ex)
            {
                Logger.Log($"AchievementsWindow: Failed to read the unlocked globals file. Reason: {ex}");
            }
        }

        /// <summary>
        /// Loads the recorded achievement unlock times from the unlocked achievements file.
        /// </summary>
        private void LoadUnlockedAchievements()
        {
            unlockedAchievements.Clear();

            string path = SafePath.CombineFilePath(ProgramConstants.GamePath, UNLOCKED_ACHIEVEMENTS_FILE);
            if (!File.Exists(path))
                return;

            try
            {
                var ini = new IniFile(path);
                List<string> keys = ini.GetSectionKeys("Achievements");
                if (keys == null)
                    return;

                foreach (string key in keys)
                {
                    string value = ini.GetStringValue("Achievements", key, string.Empty);
                    if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime time))
                        unlockedAchievements[key] = time;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"AchievementsWindow: Failed to read the unlocked achievements file. Reason: {ex}");
            }
        }

        private void CreateControls()
        {
            lblTitle = new XNALabel(WindowManager)
            {
                Name = "lblTitle",
                FontIndex = 2,
                ClientRectangle = new Rectangle(TITLE_X, TITLE_Y, 0, 0),
                Text = windowTitle.L10N("Client:Main:Achievements")
            };

            scrollPanel = new AchievementsScrollPanel(WindowManager)
            {
                Name = "scrollPanel",
                ClientRectangle = new Rectangle(GRID_X, GRID_Y,
                    Width - GRID_X - GRID_SIDE_MARGIN,
                    Height - GRID_Y - GRID_BOTTOM_MARGIN),
                BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 110), 2, 2),
                PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED,
                DrawBorders = true,
                BorderColor = UISettings.ActiveSettings.PanelBorderColor,
                ScrollStep = 40
            };

            lblNoAchievements = new XNALabel(WindowManager)
            {
                Name = "lblNoAchievements",
                FontIndex = 1,
                ClientRectangle = new Rectangle(GRID_X + 20, GRID_Y + 20, 0, 0),
                Text = "No achievements defined.".L10N("Client:Achievements:None")
            };
            lblNoAchievements.Visible = false;

            btnClose = new XNAClientButton(WindowManager)
            {
                Name = "btnClose",
                Text = "Back".L10N("Client:Main:ButtonBack")
            };
            btnClose.ClientRectangle = new Rectangle(GRID_X, Height - 45,
                UIDesignConstants.BUTTON_WIDTH_133, UIDesignConstants.BUTTON_HEIGHT);
            CampaignButtonDefaults.ApplyDefaultButtonTexture(btnClose);
            btnClose.LeftClick += (s, e) => Disable();

            AddChild(lblTitle);
            AddChild(scrollPanel);
            AddChild(lblNoAchievements);
            AddChild(btnClose);
        }

        private void LoadAchievements()
        {
            scrollPanel.ClearContent();

            if (!SafePath.GetFile(achievementsIniPath).Exists)
            {
                lblNoAchievements.Text = "Achievements.ini not found.".L10N("Client:Achievements:IniMissing");
                lblNoAchievements.Visible = true;
                return;
            }

            IniFile ini = new CCIniFile(achievementsIniPath);

            int cellWidth = (scrollPanel.Width - cellColumnGap) / 2;
            int cellIndex = 0;
            int loadedCount = 0;

            foreach (string sectionName in ini.GetSections())
            {
                if (sectionName.Equals("Achievements", StringComparison.OrdinalIgnoreCase))
                    continue;

                IniSection section = ini.GetSection(sectionName);

                string name = section.GetStringValue("Name", string.Empty).L10N($"INI:Achievements:{sectionName}:Name");
                if (string.IsNullOrEmpty(name))
                    continue;

                string description = section.GetStringValue("Description", string.Empty).L10N($"INI:Achievements:{sectionName}:Description");
                bool isLocked = section.GetBooleanValue("Locked", false);
                bool isHidden = section.GetBooleanValue("Hidden", false);

                // An achievement with a NeedUnlock key is locked until the client has
                // detected the matching global variable as set; once satisfied it is
                // unlocked regardless of the static Locked key.
                string needUnlock = section.GetStringValue("NeedUnlock", string.Empty);
                if (!string.IsNullOrEmpty(needUnlock))
                    isLocked = !unlockedGlobals.Contains(needUnlock);

                Texture2D icon = LoadIcon(section.GetStringValue("Icon", string.Empty));

                DateTime? unlockTime = unlockedAchievements.TryGetValue(sectionName, out DateTime recordedTime)
                    ? recordedTime
                    : (DateTime?)null;

                var cell = new AchievementCell(WindowManager, icon, name, description, isLocked, isHidden, unlockTime)
                {
                    Name = "cell_" + sectionName
                };

                int column = cellIndex % 2;
                int row = cellIndex / 2;
                cell.ClientRectangle = new Rectangle(
                    column * (cellWidth + cellColumnGap),
                    row * (cellHeight + cellRowGap),
                    cellWidth, cellHeight);

                scrollPanel.AddContent(cell);
                cellIndex++;
                loadedCount++;
            }

            lblNoAchievements.Visible = loadedCount == 0;
            if (loadedCount == 0)
            {
                lblNoAchievements.Text = "No achievements defined.".L10N("Client:Achievements:None");
            }
        }

        /// <summary>
        /// Loads an achievement icon from the "Achievement Icons" resources folder
        /// (falling back to the resources root). Returns null when missing.
        /// </summary>
        private Texture2D LoadIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
                return null;

            string relativePath = SafePath.CombineFilePath("Achievement Icons", iconName);

            string path = AssetLoader.AssetExists(relativePath) ? relativePath
                : AssetLoader.AssetExists(iconName) ? iconName
                : null;

            if (path == null)
                return null;

            Texture2D texture = AssetLoader.LoadTextureUncached(path);
            loadedTextures.Add(texture);

            return texture;
        }

        public override void Kill()
        {
            foreach (Texture2D texture in loadedTextures)
                texture.Dispose();
            loadedTextures.Clear();

            base.Kill();
        }
    }
}
