using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using ClientCore;
using ClientCore.Enums;
using ClientCore.Extensions;

using ClientGUI;
using ClientGUI.Settings;

using ClientUpdater;

using DTAClient.Domain;
using DTAClient.DXGUI.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    public class CampaignSelector : XNAWindow
    {
        private const int DEFAULT_WIDTH = 1280;
        private const int DEFAULT_HEIGHT = 800;

        private const int CHAPTER_BUTTON_COUNT = 4;

        // Concept layout constants (client render resolution 1280x800).
        // Matches concept.png:
        //  - Database & Achievements at the very top (top-right corner)
        //  - Four large chapter buttons tiled across the full width
        //  - Mission list on the left
        //  - Description panel (scrollable) on the right
        //  - Difficulty + Start/Back at the bottom-middle
        private const int DB_BTN_X = 915;
        private const int DB_BTN_Y = 605;
        private const int DB_BTN_WIDTH = 170;
        private const int DB_BTN_HEIGHT = 55;
        private const int ACH_BTN_X = 1095;
        private const int ACH_BTN_Y = 605;
        private const int ACH_BTN_WIDTH = 170;
        private const int ACH_BTN_HEIGHT = 55;
        private const int GAME_TIME_X = 1090;
        private const int GAME_TIME_Y = 700;
        private const int CHAPTER_BTN_Y = 55;
        private const int CHAPTER_BTN_HEIGHT = 90;
        private const int CHAPTER_BTN_X0 = 30;
        private const int CHAPTER_BTN_WIDTH = 300;
        private const int CHAPTER_BTN_SPACING = 15;
        private const int LIST_X = 20;
        private const int LIST_Y = 175;
        private const int LIST_WIDTH = 360;
        private const int LIST_HEIGHT = 570;
        private const int MISSION_ROW_HEIGHT = 120;
        private const int BACK_LABEL_HEIGHT = 24;
        private const int BACK_BUTTON_WIDTH = 100;
        private const int RIGHT_COL_X = 905;
        private const int RIGHT_COL_WIDTH = 370;
        private const int DESC_Y = 200;
        private const int DESC_HEIGHT = 340;
        private const int DIFF_X = 470;
        private const int DIFF_Y = 594;
        private const int DIFF_WIDTH = 340;
        private const int DIFF_HEIGHT = 66;
        private const int ADDITIONAL_DIFFICULTY_PANEL_WIDTH = 425;
        private const int START_BTN_X = 490;
        private const int START_BTN_WIDTH = 140;
        private const int CONTINUE_BTN_X = 650;
        private const int CONTINUE_BTN_WIDTH = 140;
        private const int LOAD_BTN_X = 810;
        private const int LOAD_BTN_WIDTH = 140;
        private const int CANCEL_BTN_X = 970;
        private const int CANCEL_BTN_WIDTH = 140;
        private const int BOTTOM_BTN_Y = 662;

        private static string[] DifficultyNames = new string[] { "Easy", "Medium", "Hard" };

        private static string[] DifficultyIniPaths = new string[]
        {
            "INI/Map Code/Difficulty Easy.ini",
            "INI/Map Code/Difficulty Medium.ini",
            "INI/Map Code/Difficulty Hard.ini"
        };

        public CampaignSelector(WindowManager windowManager, DiscordHandler discordHandler, string iniName, string battleFile, GameLoadingWindow gameLoadingWindow) : base(windowManager)
        {
            this.discordHandler = discordHandler;
            this.iniName = iniName;
            this.battleFile = battleFile;
            this.gameLoadingWindow = gameLoadingWindow;
            settingsPath = "Client/" + iniName + "Settings.ini";
        }

        /// <summary>
        /// Raised when the player requests opening the Database window.
        /// </summary>
        public event EventHandler OpenDatabaseRequested;

        /// <summary>
        /// Raised when the player requests opening the Achievements window.
        /// </summary>
        public event EventHandler OpenAchievementsRequested;

        private readonly DiscordHandler discordHandler;
        private readonly string iniName;
        private readonly string battleFile;
        private readonly string settingsPath;
        private readonly GameLoadingWindow gameLoadingWindow;

        private List<Mission> selectedMissions = [];

        private const string MISSION_ICONS_FOLDER = "Mission Icons";
        private const string DEFAULT_MISSION_ICON_NAME = "Default.png";

        private XNALabel lblSelectCampaign;
        private CampaignMissionListBox lbCampaignList;
        private XNAButton btnBack;
        private Rectangle listBaseRect;
        private XNAClientButton btnLaunch;
        private XNAClientButton btnContinue;
        private XNAClientButton btnLoadGame;
        private XNAClientButton btnCancel;
        private XNAClientButton btnDatabase;
        private XNAClientButton btnAchievements;
        private XNAScrollableTextBlock tbMissionDescription;
        private XNAPanel pnlDifficulty;
        private XNALabel lblMissionDescriptionHeader;
        private XNALabel lblDifficultyLevel;
        private Rectangle difficultyPanelBaseRect;
        private XNALabel lblEasy;
        private XNALabel lblNormal;
        private XNALabel lblHard;
        private XNALabel lblAdditional;
        private string[] defaultDifficultyLabelTexts;
        private XNATrackbar trbDifficultySelector;
        private List<IUserSetting> userSettings = new List<IUserSetting>();

        private const string CAMPAIGN_GAME_TIME_FILE = "Client/CampaignGameTime.txt";

        private XNALabel lblGameTime;
        private XNAPanel pnlGameTime;
        private TimeSpan campaignGameTime;
        private DateTime missionLaunchTime;

        private CheaterWindow cheaterWindow;

        public List<CampaignCheckBox> CheckBoxes { get; } = new();
        public List<CampaignDropDown> DropDowns { get; } = new();

        private IniFile gameOptionsIni;

        private string[] filesToCheck = new string[]
        {
            "INI/AI.ini",
            "INI/AIE.ini",
            "INI/Art.ini",
            "INI/ArtE.ini",
            "INI/Enhance.ini",
            "INI/Rules.ini",
            "INI/Map Code/Difficulty Hard.ini",
            "INI/Map Code/Difficulty Medium.ini",
            "INI/Map Code/Difficulty Easy.ini"
        };

        private Mission missionToLaunch;

        private const string MISSION_PROGRESS_FILE = "Client/MissionProgress.ini";
        private const string UNLOCKED_ACHIEVEMENTS_FILE = "Client/UnlockedAchievements.ini";

        // Highest difficulty won per mission code name (0 = Easy, 1 = Medium,
        // 2 = Hard, 3 = additional difficulty). Used to draw victory badges.
        private readonly Dictionary<string, int> missionBestDifficulty = new();

        // Global variables (e.g. "RUS1_WIN") that the client has detected as set
        // after a won mission. Used to unlock achievements with a NeedUnlock key.
        private readonly HashSet<string> unlockedGlobals = new(StringComparer.OrdinalIgnoreCase);

        // Global variable name -> the moment it was first satisfied (persisted under
        // [Globals] in the unlocked achievements file).
        private readonly Dictionary<string, DateTime> satisfiedGlobalTimes = new(StringComparer.OrdinalIgnoreCase);

        // Achievement section name -> the NeedUnlock global variable it requires.
        private readonly Dictionary<string, string> achievementNeedUnlock = new(StringComparer.OrdinalIgnoreCase);

        // Achievement section name -> the moment it was first unlocked.
        private readonly Dictionary<string, DateTime> unlockedAchievements = new(StringComparer.OrdinalIgnoreCase);

        // The mission currently launched and the difficulty it was launched with,
        // used to record a victory badge when the mission is won.
        private Mission launchedMission;
        private int launchedDifficulty = -1;

        private List<Mission> _allMissions = [];
        public IReadOnlyCollection<Mission> AllMissions { get => _allMissions; }

        private Dictionary<int, Mission> _uniqueIDToMissions = new();
        public IReadOnlyDictionary<int, Mission> UniqueIDToMissions => _uniqueIDToMissions;

        private class ChapterButtonSource
        {
            public string Label;
            public string BattleFile;
            public string WorldMapName = "worldmap.png";
            public string OverlayImage;
            public float OverlayScale = 1.0f;
            public List<Mission> Missions = new();
            public XNAClientButton Button;
            public Texture2D DefaultIdleTexture;
            public bool HasMissions => Missions.Count > 0;
        }

        /// <summary>
        /// A sub-chapter group shown in the mission list's group navigation
        /// (e.g. "Allied Campaign" / "Soviet Campaign"). Missions whose <see cref="Mission.Group"/>
        /// equals <see cref="Key"/> are shown when the group is entered.
        /// </summary>
        private class CampaignGroup
        {
            public string Key;
            public string DisplayName;
            public string IconName;
        }

        private readonly List<ChapterButtonSource> chapterSources = new();
        private ChapterButtonSource activeChapterSource;
        private readonly XNAClientButton[] chapterButtons = new XNAClientButton[CHAPTER_BUTTON_COUNT];
        private RotatingEarthBackground rotatingEarth;

        // Sub-chapter group navigation state.
        private readonly List<CampaignGroup> topLevelGroups = new();
        private readonly List<CampaignGroup> groupPath = new();

        private void AddMission(Mission mission)
        {
            // no matter whether the key is duplicated, the mission is always added to AllMissions
            _allMissions.Add(mission);

            // but only the first mission is recorded in UniqueIDToMissions
            if (_uniqueIDToMissions.ContainsKey(mission.CustomMissionID))
            {
                Logger.Log($"CampaignSelector: duplicated mission. CodeName: {mission.CodeName}. ID: {mission.CustomMissionID}. Description: {mission.UntranslatedGUIName}.");
            }
            else
            {
                _uniqueIDToMissions.Add(mission.CustomMissionID, mission);
            }
        }

        public override void Initialize()
        {
            Name = iniName;
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), DEFAULT_WIDTH, DEFAULT_HEIGHT);
            ClientRectangle = new Rectangle(0, 0, DEFAULT_WIDTH, DEFAULT_HEIGHT);
            BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            gameOptionsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(),
                ClientConfiguration.GAME_OPTIONS));

            lblSelectCampaign = new XNALabel(WindowManager);
            lblSelectCampaign.Name = nameof(lblSelectCampaign);
            lblSelectCampaign.FontIndex = 1;
            lblSelectCampaign.ClientRectangle = new Rectangle(LIST_X, LIST_Y - 20, 0, 0);
            lblSelectCampaign.Text = "MISSIONS:".L10N("Client:Main:Missions");

            pnlGameTime = new XNAPanel(WindowManager);
            pnlGameTime.Name = nameof(pnlGameTime);
            pnlGameTime.ClientRectangle = new Rectangle(RIGHT_COL_X, GAME_TIME_Y - 30, RIGHT_COL_WIDTH, 60);
            pnlGameTime.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            pnlGameTime.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 150), 2, 2);

            lblGameTime = new XNALabel(WindowManager);
            lblGameTime.Name = nameof(lblGameTime);
            lblGameTime.FontIndex = 5;
            lblGameTime.AnchorPoint = new Vector2(GAME_TIME_X, GAME_TIME_Y);
            lblGameTime.TextAnchor = LabelTextAnchorInfo.CENTER;
            lblGameTime.Text = string.Empty;

            lbCampaignList = new CampaignMissionListBox(WindowManager);
            lbCampaignList.Name = nameof(lbCampaignList);
            lbCampaignList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 2, 2);
            lbCampaignList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbCampaignList.ClientRectangle = new Rectangle(LIST_X, LIST_Y, LIST_WIDTH, LIST_HEIGHT);
            lbCampaignList.LineHeight = MISSION_ROW_HEIGHT;
            lbCampaignList.SelectedIndexChanged += LbCampaignList_SelectedIndexChanged;

            btnBack = new XNAButton(WindowManager);
            btnBack.Name = nameof(btnBack);
            btnBack.FontIndex = 1;
            btnBack.Text = "< Back".L10N("Client:Campaign:Back");
            btnBack.AdaptiveText = false;
            btnBack.TextXPosition = 0;
            btnBack.TextYPosition = 2;
            btnBack.ClientRectangle = new Rectangle(LIST_X, LIST_Y - BACK_LABEL_HEIGHT,
                BACK_BUTTON_WIDTH, BACK_LABEL_HEIGHT);
            btnBack.DrawOrder = 10;
            btnBack.UpdateOrder = 10;
            btnBack.LeftClick += Back_Click;
            btnBack.Visible = false;
            AddChild(btnBack);

            lblMissionDescriptionHeader = new XNALabel(WindowManager);
            lblMissionDescriptionHeader.Name = nameof(lblMissionDescriptionHeader);
            lblMissionDescriptionHeader.FontIndex = 1;
            lblMissionDescriptionHeader.ClientRectangle = new Rectangle(RIGHT_COL_X, LIST_Y - 20, 0, 0);
            lblMissionDescriptionHeader.Text = "MISSION DESCRIPTION:".L10N("Client:Main:MissionDescription");

            tbMissionDescription = new XNAScrollableTextBlock(WindowManager);
            tbMissionDescription.Name = nameof(tbMissionDescription);
            tbMissionDescription.ClientRectangle = new Rectangle(RIGHT_COL_X, DESC_Y, RIGHT_COL_WIDTH, DESC_HEIGHT);
            tbMissionDescription.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            tbMissionDescription.Alpha = 1.0f;
            tbMissionDescription.FontIndex = 1;
            tbMissionDescription.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 150),
                tbMissionDescription.Width, tbMissionDescription.Height);

            pnlDifficulty = new XNAPanel(WindowManager);
            pnlDifficulty.Name = nameof(pnlDifficulty);
            pnlDifficulty.ClientRectangle = new Rectangle(
                DIFF_X,
                DIFF_Y,
                DIFF_WIDTH, DIFF_HEIGHT);
            pnlDifficulty.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            pnlDifficulty.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 140),
                pnlDifficulty.Width, pnlDifficulty.Height);
            pnlDifficulty.DrawBorders = true;
            pnlDifficulty.BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            lblDifficultyLevel = new XNALabel(WindowManager);
            lblDifficultyLevel.Name = nameof(lblDifficultyLevel);
            lblDifficultyLevel.Text = "DIFFICULTY LEVEL".L10N("Client:Main:DifficultyLevel");
            lblDifficultyLevel.FontIndex = 1;
            lblDifficultyLevel.ClientRectangle = new Rectangle(
                pnlDifficulty.X + (pnlDifficulty.Width - (int)Renderer.GetTextDimensions(lblDifficultyLevel.Text, lblDifficultyLevel.FontIndex).X) / 2,
                pnlDifficulty.Y + 4, 1, 1);

            trbDifficultySelector = new XNATrackbar(WindowManager);
            trbDifficultySelector.Name = nameof(trbDifficultySelector);
            trbDifficultySelector.ClientRectangle = new Rectangle(
                pnlDifficulty.X + 10, pnlDifficulty.Y + 4 + GetDifficultyHeaderHeight() + 4,
                pnlDifficulty.Width - 20, 20);
            trbDifficultySelector.MinValue = 0;
            trbDifficultySelector.MaxValue = 2;
            trbDifficultySelector.BackgroundTexture = AssetLoader.CreateTexture(
                new Color(0, 0, 0, 128), 2, 2);
            trbDifficultySelector.ButtonTexture = AssetLoader.LoadTextureUncached(
                "trackbarButton_difficulty.png");

            lblEasy = new XNALabel(WindowManager);
            lblEasy.Name = nameof(lblEasy);
            lblEasy.FontIndex = 1;
            lblEasy.Text = "EASY".L10N("Client:Main:DifficultyEasy");
            lblEasy.ClientRectangle = new Rectangle(trbDifficultySelector.X,
                trbDifficultySelector.Bottom + 2, 1, 1);

            lblNormal = new XNALabel(WindowManager);
            lblNormal.Name = nameof(lblNormal);
            lblNormal.FontIndex = 1;
            lblNormal.Text = "NORMAL".L10N("Client:Main:DifficultyNormal");
            lblNormal.ClientRectangle = new Rectangle(
                trbDifficultySelector.X + (trbDifficultySelector.Width - (int)Renderer.GetTextDimensions(lblNormal.Text, lblNormal.FontIndex).X) / 2,
                lblEasy.Y, 1, 1);

            lblHard = new XNALabel(WindowManager);
            lblHard.Name = nameof(lblHard);
            lblHard.FontIndex = 1;
            lblHard.Text = "HARD".L10N("Client:Main:DifficultyHard");
            lblHard.ClientRectangle = new Rectangle(
                trbDifficultySelector.Right - (int)Renderer.GetTextDimensions(lblHard.Text, lblHard.FontIndex).X,
                lblEasy.Y, 1, 1);

            lblAdditional = new XNALabel(WindowManager);
            lblAdditional.Name = nameof(lblAdditional);
            lblAdditional.FontIndex = 1;
            lblAdditional.Text = lblHard.Text;
            lblAdditional.Visible = false;

            defaultDifficultyLabelTexts = new[] { lblEasy.Text, lblNormal.Text, lblHard.Text };

            for (int i = 0; i < CHAPTER_BUTTON_COUNT; i++)
            {
                var btn = new XNAClientButton(WindowManager);
                btn.Name = "btnChapter" + i;
                btn.ClientRectangle = new Rectangle(
                    CHAPTER_BTN_X0 + i * (CHAPTER_BTN_WIDTH + CHAPTER_BTN_SPACING),
                    CHAPTER_BTN_Y, CHAPTER_BTN_WIDTH, CHAPTER_BTN_HEIGHT);
                btn.IdleTexture = AssetLoader.CreateTexture(new Color(32, 32, 32, 235), 1, 1);
                btn.HoverTexture = AssetLoader.CreateTexture(new Color(64, 64, 64, 235), 1, 1);
                int capturedIndex = i;
                btn.LeftClick += (sender, e) => SelectChapter(capturedIndex);
                chapterButtons[i] = btn;
                AddChild(btn);
            }

            btnDatabase = new XNAClientButton(WindowManager);
            btnDatabase.Name = nameof(btnDatabase);
            btnDatabase.ClientRectangle = new Rectangle(DB_BTN_X, DB_BTN_Y, DB_BTN_WIDTH, DB_BTN_HEIGHT);
            ApplyDefaultButtonTexture(btnDatabase);
            btnDatabase.FontIndex = 2;
            btnDatabase.Text = "Database".L10N("Client:Main:Database");
            btnDatabase.LeftClick += (sender, e) => OpenDatabaseRequested?.Invoke(this, EventArgs.Empty);

            btnAchievements = new XNAClientButton(WindowManager);
            btnAchievements.Name = nameof(btnAchievements);
            btnAchievements.ClientRectangle = new Rectangle(ACH_BTN_X, ACH_BTN_Y, ACH_BTN_WIDTH, ACH_BTN_HEIGHT);
            ApplyDefaultButtonTexture(btnAchievements);
            btnAchievements.FontIndex = 2;
            btnAchievements.Text = "Achievements".L10N("Client:Main:Achievements");
            btnAchievements.LeftClick += (sender, e) => OpenAchievementsRequested?.Invoke(this, EventArgs.Empty);

            btnLaunch = new XNAClientButton(WindowManager);
            btnLaunch.Name = nameof(btnLaunch);
            btnLaunch.ClientRectangle = new Rectangle(START_BTN_X, BOTTOM_BTN_Y, START_BTN_WIDTH, UIDesignConstants.BUTTON_HEIGHT);
            ApplyDefaultButtonTexture(btnLaunch);
            btnLaunch.Text = "Start".L10N("Client:Main:ButtonLaunch");
            btnLaunch.AllowClick = false;
            btnLaunch.LeftClick += BtnLaunch_LeftClick;

            btnContinue = new XNAClientButton(WindowManager);
            btnContinue.Name = nameof(btnContinue);
            btnContinue.ClientRectangle = new Rectangle(CONTINUE_BTN_X, BOTTOM_BTN_Y, CONTINUE_BTN_WIDTH, UIDesignConstants.BUTTON_HEIGHT);
            ApplyDefaultButtonTexture(btnContinue);
            btnContinue.Text = "Continue Game".L10N("Client:Main:ButtonContinue");
            btnContinue.AllowClick = false;
            btnContinue.LeftClick += BtnContinue_LeftClick;

            btnLoadGame = new XNAClientButton(WindowManager);
            btnLoadGame.Name = nameof(btnLoadGame);
            btnLoadGame.ClientRectangle = new Rectangle(LOAD_BTN_X, BOTTOM_BTN_Y, LOAD_BTN_WIDTH, UIDesignConstants.BUTTON_HEIGHT);
            ApplyDefaultButtonTexture(btnLoadGame);
            btnLoadGame.Text = "Load Game".L10N("Client:Main:ButtonLoadGame");
            btnLoadGame.LeftClick += BtnLoadGame_LeftClick;

            btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = nameof(btnCancel);
            btnCancel.ClientRectangle = new Rectangle(CANCEL_BTN_X, BOTTOM_BTN_Y, CANCEL_BTN_WIDTH, UIDesignConstants.BUTTON_HEIGHT);
            ApplyDefaultButtonTexture(btnCancel);
            btnCancel.Text = "Back".L10N("Client:Main:ButtonCancel");
            btnCancel.LeftClick += BtnCancel_LeftClick;

            AddChild(lblSelectCampaign);
            AddChild(pnlGameTime);
            AddChild(lblGameTime);
            AddChild(lbCampaignList);
            AddChild(lblMissionDescriptionHeader);
            AddChild(tbMissionDescription);
            AddChild(pnlDifficulty);
            AddChild(lblDifficultyLevel);
            AddChild(trbDifficultySelector);
            AddChild(lblEasy);
            AddChild(lblNormal);
            AddChild(lblHard);
            AddChild(lblAdditional);
            AddChild(btnDatabase);
            AddChild(btnAchievements);
            AddChild(btnLaunch);
            AddChild(btnContinue);
            AddChild(btnLoadGame);
            AddChild(btnCancel);

            // Set control attributes from INI file
            base.Initialize();

            ApplyTopButtonImages();

            // Align the game time panel with the mission description panel (same column).
            // This is only a default: if the user defines [pnlGameTime] / [lblGameTime]
            // sections in the window INI, those values take precedence.
            Rectangle descRect = tbMissionDescription.ClientRectangle;

            if (ThemeIni == null || !ThemeIni.SectionExists(nameof(pnlGameTime)))
                pnlGameTime.ClientRectangle = new Rectangle(descRect.X, GAME_TIME_Y - 30, descRect.Width, 60);

            if (ThemeIni == null || !ThemeIni.SectionExists(nameof(lblGameTime)))
                lblGameTime.AnchorPoint = new Vector2(descRect.X + descRect.Width / 2f, GAME_TIME_Y);

            // Remember the list's actual (INI-adjusted) rectangle so group navigation
            // can place the back button inside the list's top area.
            listBaseRect = lbCampaignList.ClientRectangle;

            // Remember the difficulty panel's base rectangle so it can be widened
            // when a mission enables the additional difficulty.
            difficultyPanelBaseRect = pnlDifficulty.ClientRectangle;

            // Place the back button inside the list's top-left corner, flush with the
            // list's left border.
            btnBack.ClientRectangle = new Rectangle(
                listBaseRect.X, listBaseRect.Y + 2, BACK_BUTTON_WIDTH, BACK_LABEL_HEIGHT);

            // Center on screen
            CenterOnParent();

            // Real 3D rotating earth globe in the center of the screen.
            rotatingEarth = new RotatingEarthBackground(WindowManager)
            {
                ClientRectangle = new Rectangle(0, 0, Width, Height),
                InputEnabled = false,
                DrawOrder = -200,
                GlobeDiameter = Math.Min(Width, Height) - 220
            };
            AddChild(rotatingEarth);

            trbDifficultySelector.Value = UserINISettings.Instance.Difficulty;

            // Apply the font-aware difficulty layout immediately so the initial
            // (nothing selected) state matches the layout once a mission is selected.
            UpdateDifficultyLabels(null);

            userSettings.AddRange(Children.OfType<IUserSetting>());

            LoadChapterButtons();

            LoadGroups();

            LoadCustomMissions();

            LoadMissionProgress();
            lbCampaignList.MissionBestDifficulty = missionBestDifficulty;
            lbCampaignList.IsMissionLocked = mission => !IsMissionUnlocked(mission);

            LoadUnlockedGlobals();

            LoadAchievementNeedUnlockMappings();
            LoadUnlockedAchievements();

            LoadCampaignGameTime();
            UpdateGameTimeLabel();

            RefreshMissionList();

            cheaterWindow = new CheaterWindow(WindowManager);
            var dp = new DarkeningPanel(WindowManager);
            dp.AddChild(cheaterWindow);
            AddChild(dp);
            dp.CenterOnParent();
            cheaterWindow.CenterOnParent();
            cheaterWindow.YesClicked += CheaterWindow_YesClicked;
            cheaterWindow.Disable();

            LoadSettings();
        }

        private void ApplyTopButtonImages()
        {
            ApplyTopButtonImage(btnDatabase, nameof(btnDatabase));
            ApplyTopButtonImage(btnAchievements, nameof(btnAchievements));
        }

        private void ApplyDefaultButtonTexture(XNAClientButton button)
        {
            CampaignButtonDefaults.ApplyDefaultButtonTexture(button);
        }

        private void ApplyTopButtonImage(XNAClientButton button, string iniSection)
        {
            if (ThemeIni == null)
                return;

            string idle = ThemeIni.GetStringValue(iniSection, "IdleTexture", string.Empty);
            if (string.IsNullOrEmpty(idle) || !AssetLoader.AssetExists(idle))
                return;

            Texture2D idleTexture = AssetLoader.LoadTexture(idle);
            string hover = ThemeIni.GetStringValue(iniSection, "HoverTexture", string.Empty);
            Texture2D hoverTexture = !string.IsNullOrEmpty(hover) && AssetLoader.AssetExists(hover)
                ? AssetLoader.LoadTexture(hover)
                : idleTexture;

            button.IdleTexture = idleTexture;
            button.HoverTexture = hoverTexture;
        }

        private void LoadChapterButtons()
        {
            chapterSources.Clear();

            // Read the ordered chapter button list from the window's INI file ([ChapterButtons] section).
            // Each entry is "label,battle file path[,worldmap file name[,overlay image[,overlay scale]]]"
            // where the battle file is parsed to populate the mission list for that button. Buttons whose
            // battle file has no missions are disabled. The optional third field is the world map texture
            // to display on the globe when that chapter is selected (defaults to "worldmap.png"). The
            // optional fourth field is an image layered on top of the globe, and the optional fifth field
            // is its scale as a percentage relative to the globe's disc (defaults to 100).
            bool hasIniButtons = false;
            if (ThemeIni != null && ThemeIni.SectionExists("ChapterButtons"))
            {
                for (int i = 0; i < CHAPTER_BUTTON_COUNT; i++)
                {
                    string value = ThemeIni.GetStringValue("ChapterButtons", i.ToString(), string.Empty);
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    string[] parts = value.Split(',');
                    string label = parts[0].Trim();
                    string sourceBattleFile = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                    string worldMapName = parts.Length > 2 ? parts[2].Trim() : "worldmap.png";
                    if (string.IsNullOrWhiteSpace(worldMapName))
                        worldMapName = "worldmap.png";
                    string overlayImage = parts.Length > 3 ? parts[3].Trim() : string.Empty;
                    float overlayScale = 1.0f;
                    if (parts.Length > 4 && float.TryParse(parts[4].Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedScale)
                        && parsedScale > 0)
                        overlayScale = parsedScale / 100.0f;

                    var source = new ChapterButtonSource
                    {
                        Label = label,
                        BattleFile = sourceBattleFile,
                        WorldMapName = worldMapName,
                        OverlayImage = overlayImage,
                        OverlayScale = overlayScale
                    };
                    if (!string.IsNullOrEmpty(sourceBattleFile))
                        ParseBattleIni(sourceBattleFile, source.Missions);

                    chapterSources.Add(source);
                    hasIniButtons = true;
                }
            }

            // Fall back to a single "Tutorial" button with the default battle file.
            if (!hasIniButtons)
            {
                var source = new ChapterButtonSource { Label = "Tutorial", BattleFile = battleFile };
                ParseBattleIni(battleFile, source.Missions);
                chapterSources.Add(source);
            }

            for (int i = 0; i < CHAPTER_BUTTON_COUNT; i++)
            {
                XNAClientButton btn = chapterButtons[i];

                if (i < chapterSources.Count)
                {
                    ChapterButtonSource source = chapterSources[i];
                    source.Button = btn;
                    source.DefaultIdleTexture = btn.IdleTexture;
                    btn.Text = source.Label;
                    btn.AllowClick = source.HasMissions;
                }
                else
                {
                    btn.Text = string.Empty;
                    btn.AllowClick = false;
                }
            }

            activeChapterSource = chapterSources.FirstOrDefault(s => s.HasMissions) ?? chapterSources.FirstOrDefault();
            UpdateChapterButtonStates();

            // Preload every chapter's world map and overlay so switching chapters doesn't
            // stall on the first visit to each chapter (large textures are loaded
            // synchronously and must be decoded ahead of time).
            foreach (ChapterButtonSource source in chapterSources)
            {
                rotatingEarth?.PreloadWorldMap(source.WorldMapName);
                rotatingEarth?.PreloadWorldMapOverlay(source.OverlayImage);
            }

            if (activeChapterSource != null)
            {
                rotatingEarth?.SetWorldMap(activeChapterSource.WorldMapName);
                rotatingEarth?.SetWorldMapOverlay(activeChapterSource.OverlayImage, activeChapterSource.OverlayScale);
            }
        }

        /// <summary>
        /// Loads the sub-chapter groups from the window INI's [CampaignGroups] section.
        /// Each entry is "index=key,displayname[,iconname]".
        /// </summary>
        private void LoadGroups()
        {
            topLevelGroups.Clear();

            if (ThemeIni == null || !ThemeIni.SectionExists("CampaignGroups"))
                return;

            for (int i = 0; i < 32; i++)
            {
                string value = ThemeIni.GetStringValue("CampaignGroups", i.ToString(), string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string[] parts = value.Split(',');
                if (parts.Length < 2)
                    continue;

                string key = parts[0].Trim();
                string displayName = parts[1].Trim().L10N("Client:CampaignGroups:" + key);
                string iconName = parts.Length > 2 ? parts[2].Trim() : string.Empty;

                topLevelGroups.Add(new CampaignGroup
                {
                    Key = key,
                    DisplayName = displayName,
                    IconName = iconName
                });
            }
        }

        private void SelectChapter(int index)
        {
            if (index < 0 || index >= chapterSources.Count)
                return;

            ChapterButtonSource source = chapterSources[index];
            if (!source.HasMissions)
                return;

            activeChapterSource = source;
            UpdateChapterButtonStates();
            rotatingEarth?.SetWorldMap(source.WorldMapName);
            rotatingEarth?.SetWorldMapOverlay(source.OverlayImage, source.OverlayScale);
            groupPath.Clear();
            RefreshMissionList();
        }

        private void UpdateChapterButtonStates()
        {
            foreach (ChapterButtonSource source in chapterSources)
            {
                if (source.Button == null)
                    continue;

                // The active chapter button uses its hover texture to indicate selection.
                if (source == activeChapterSource && source.HasMissions)
                    source.Button.IdleTexture = source.Button.HoverTexture;
                else
                    source.Button.IdleTexture = source.DefaultIdleTexture;
            }
        }

        private void LbCampaignList_SelectedIndexChanged(object sender, EventArgs e)
        {
            XNAListBoxItem selectedItem = lbCampaignList.SelectedItem;

            if (selectedItem == null)
            {
                tbMissionDescription.Text = string.Empty;

                UpdateDifficultyLabels(null);

                rotatingEarth?.ClearImages();
                rotatingEarth?.ResetLocation();

                btnLaunch.AllowClick = false;
                btnContinue.AllowClick = false;
                return;
            }

            // Sub-chapter group navigation.
            if (selectedItem.Tag is CampaignGroup group)
            {
                groupPath.Add(group);
                RefreshMissionList();
                return;
            }

            Mission mission = selectedItem.Tag as Mission;
            if (mission == null)
            {
                tbMissionDescription.Text = string.Empty;

                UpdateDifficultyLabels(null);

                rotatingEarth?.ClearImages();
                rotatingEarth?.ResetLocation();

                btnLaunch.AllowClick = false;
                btnContinue.AllowClick = false;
                return;
            }

            UpdateDifficultyLabels(mission);

            rotatingEarth?.SetImages(mission.GlobeImages);

            if (mission.LocationLongitude.HasValue && mission.LocationLatitude.HasValue)
                rotatingEarth?.SetLocation(mission.LocationLongitude.Value, mission.LocationLatitude.Value);
            else
                rotatingEarth?.ResetLocation();

            if (string.IsNullOrEmpty(mission.Scenario))
            {
                tbMissionDescription.Text = string.Empty;
                btnLaunch.AllowClick = false;
                btnContinue.AllowClick = false;
                return;
            }

            tbMissionDescription.Text = mission.GUIDescription;

            if (!mission.Enabled || !IsMissionUnlocked(mission))
            {
                btnLaunch.AllowClick = false;
                btnContinue.AllowClick = false;
                return;
            }

            btnLaunch.AllowClick = true;
            btnContinue.AllowClick = CanContinueSelectedMission();
        }

        private void Back_Click(object sender, EventArgs e)
        {
            groupPath.Clear();
            RefreshMissionList();
        }

        /// <summary>
        /// Updates the difficulty level labels and the trackbar range to the selected
        /// mission's configuration, or restores the defaults when the mission defines
        /// none. Missions with <see cref="Mission.AdditionalDifficulty"/> enabled get
        /// a fourth difficulty level. Missions with <see cref="Mission.NoDifficulty"/>
        /// hide the whole difficulty selector, since they are always launched on Easy.
        /// </summary>
        private void UpdateDifficultyLabels(Mission mission)
        {
            bool noDifficulty = mission == null || mission.NoDifficulty == true;
            SetDifficultyControlsVisible(!noDifficulty);
            if (noDifficulty)
                return;

            int levelCount = mission != null && mission.AdditionalDifficulty ? 4 : 3;

            UpdateDifficultyPanel(levelCount);

            trbDifficultySelector.MaxValue = levelCount - 1;
            if (trbDifficultySelector.Value > trbDifficultySelector.MaxValue)
                trbDifficultySelector.Value = trbDifficultySelector.MaxValue;

            lblAdditional.Visible = levelCount == 4;

            int labelY = trbDifficultySelector.Bottom + 2;

            for (int i = 0; i < levelCount; i++)
            {
                string fallback = defaultDifficultyLabelTexts[Math.Min(i, defaultDifficultyLabelTexts.Length - 1)];
                SetDifficultyLabelText(i, levelCount, GetDisplayedDifficultyLabel(mission, i, fallback), labelY);
            }
        }

        /// <summary>
        /// Shows or hides the whole difficulty selector (panel, header, trackbar and
        /// level labels). Used to hide it for missions with <see cref="Mission.NoDifficulty"/>.
        /// </summary>
        private void SetDifficultyControlsVisible(bool visible)
        {
            pnlDifficulty.Visible = visible;
            lblDifficultyLevel.Visible = visible;
            trbDifficultySelector.Visible = visible;
            lblEasy.Visible = visible;
            lblNormal.Visible = visible;
            lblHard.Visible = visible;
            lblAdditional.Visible = visible;
        }

        /// <summary>
        /// Widens the difficulty panel (and thus the trackbar) when a mission enables
        /// the additional difficulty, so the extra difficulty slot has enough room for
        /// its label. Restores the base size otherwise. The panel is kept horizontally
        /// centered on its base center.
        /// </summary>
        private void UpdateDifficultyPanel(int levelCount)
        {
            if (levelCount == 4)
            {
                int centerX = difficultyPanelBaseRect.X + difficultyPanelBaseRect.Width / 2;
                pnlDifficulty.ClientRectangle = new Rectangle(
                    centerX - ADDITIONAL_DIFFICULTY_PANEL_WIDTH / 2,
                    difficultyPanelBaseRect.Y,
                    ADDITIONAL_DIFFICULTY_PANEL_WIDTH,
                    difficultyPanelBaseRect.Height);
            }
            else
            {
                pnlDifficulty.ClientRectangle = difficultyPanelBaseRect;
            }

            // Re-center the header and resize the trackbar to the panel's new width.
            lblDifficultyLevel.ClientRectangle = new Rectangle(
                pnlDifficulty.X + (pnlDifficulty.Width - (int)Renderer.GetTextDimensions(lblDifficultyLevel.Text, lblDifficultyLevel.FontIndex).X) / 2,
                pnlDifficulty.Y + 4, 1, 1);

            trbDifficultySelector.ClientRectangle = new Rectangle(
                pnlDifficulty.X + 10, pnlDifficulty.Y + 4 + GetDifficultyHeaderHeight() + 4,
                pnlDifficulty.Width - 20, 20);
        }

        /// <summary>
        /// Returns the actual height of the "DIFFICULTY LEVEL" header text so the
        /// trackbar can be placed below it instead of overlapping it.
        /// </summary>
        private int GetDifficultyHeaderHeight() =>
            (int)Renderer.GetTextDimensions(lblDifficultyLevel.Text, lblDifficultyLevel.FontIndex).Y;

        /// <summary>
        /// Returns the custom difficulty label for the given level if the mission
        /// defines one, otherwise the provided fallback text.
        /// </summary>
        private string GetDisplayedDifficultyLabel(Mission mission, int level, string fallback)
        {
            if (mission != null && level < mission.DifficultyLabels.Length &&
                !string.IsNullOrWhiteSpace(mission.DifficultyLabels[level]))
                return mission.DifficultyLabels[level];

            return fallback;
        }

        /// <summary>
        /// Sets the text of one of the difficulty level labels and repositions it at
        /// its trackbar slot. The first slot is left-aligned, the second is centered,
        /// and the remaining slots are right-aligned at their tick positions so that
        /// adjacent labels can't overlap.
        /// </summary>
        private void SetDifficultyLabelText(int index, int levelCount, string text, int labelY)
        {
            XNALabel label = GetDifficultyLabelControl(index);

            label.Text = text;

            // XNALabel only repositions the label when TextAnchor is used; these
            // labels position themselves from their text width, so compute the X
            // coordinate manually.
            int textWidth = (int)Renderer.GetTextDimensions(text, label.FontIndex).X;
            int slotX = trbDifficultySelector.X + (int)(index * trbDifficultySelector.Width / (double)(levelCount - 1));
            int x;

            if (index == 0)
                x = trbDifficultySelector.X;
            else if (index == 1)
                x = slotX - textWidth / 2;
            else
                x = slotX - textWidth;

            label.ClientRectangle = new Rectangle(x, labelY, 1, 1);
        }

        private XNALabel GetDifficultyLabelControl(int index) => index switch
        {
            0 => lblEasy,
            1 => lblNormal,
            2 => lblHard,
            _ => lblAdditional
        };

        private void BtnCancel_LeftClick(object sender, EventArgs e)
        {
            SaveSettings();
            Disable();
        }

        private void BtnLoadGame_LeftClick(object sender, EventArgs e)
        {
            gameLoadingWindow.ListSaves();
            gameLoadingWindow.Enable();
        }

        private void BtnContinue_LeftClick(object sender, EventArgs e)
        {
            Mission mission = lbCampaignList.SelectedItem?.Tag as Mission;
            if (mission == null)
                return;

            string fileName = gameLoadingWindow.GetContinueSaveFileName(mission);
            if (fileName == null)
            {
                var msgBox = new XNAMessageBox(WindowManager,
                    "Continue Game".L10N("Client:Main:ButtonContinue"),
                    string.Format("No saved game found for \"{0}\".\n\n" +
                        "Start the mission and quit it once to create a continue save."
                        .L10N("Client:Main:NoContinueSave"), mission.GUIName),
                    XNAMessageBoxButtons.OK);
                msgBox.Show();
                return;
            }

            SavedGame sg = new SavedGame(fileName);
            if (!sg.ParseInfo())
                Logger.Log($"CampaignSelector: failed to parse continue save {fileName}; launching anyway.");

            gameLoadingWindow.LaunchSaveGame(sg, mission);
        }

        /// <summary>
        /// Whether the currently selected mission has a continue save that can be
        /// resumed with the "Continue Game" button.
        /// </summary>
        private bool CanContinueSelectedMission()
        {
            if (lbCampaignList.SelectedItem?.Tag is not Mission mission)
                return false;

            return gameLoadingWindow.GetContinueSaveFileName(mission) != null;
        }

        private void BtnLaunch_LeftClick(object sender, EventArgs e)
        {
            SaveSettings();

            Mission mission = lbCampaignList.SelectedItem?.Tag as Mission;
            if (mission == null)
                return;

            if (!ClientConfiguration.Instance.ModMode &&
                (!Updater.IsFileNonexistantOrOriginal(mission.GetEffectiveScenario(trbDifficultySelector.Value)) || AreFilesModified()))
            {
                // Confront the user by showing the cheater screen
                missionToLaunch = mission;
                cheaterWindow.Enable();
                return;
            }

            TryStartMission(mission);
        }

        private bool AreFilesModified()
        {
            foreach (string filePath in filesToCheck)
            {
                if (!Updater.IsFileNonexistantOrOriginal(filePath))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Called when the user wants to proceed to the mission despite having
        /// being called a cheater.
        /// </summary>
        private void CheaterWindow_YesClicked(object sender, EventArgs e)
        {
            TryStartMission(missionToLaunch);
        }

        /// <summary>
        /// The third difficulty level (index 2, i.e. "Hard" / "APOC"), which is
        /// warned about the first time the player plays it.
        /// </summary>
        private const int APOC_DIFFICULTY_INDEX = 2;

        /// <summary>
        /// Section in the campaign window INI that holds the difficulty warning
        /// captions/text so they can be customized without recompiling.
        /// </summary>
        private const string DIFFICULTY_WARNINGS_SECTION = "DifficultyWarnings";

        /// <summary>
        /// Whether the player has ever played a mission on the APOC (third)
        /// difficulty level. Persisted so the first-time warning only shows once.
        /// </summary>
        private bool hasPlayedApocDifficulty;

        /// <summary>
        /// Launches the given mission. When the selected mission still has an
        /// unfinished game (a continue save), a reminder is shown first; confirming
        /// launches directly (skipping the difficulty warnings), declining aborts.
        /// Otherwise the normal difficulty warning flow runs before launching.
        /// </summary>
        private void TryStartMission(Mission mission)
        {
            if (gameLoadingWindow.GetContinueSaveFileName(mission) != null)
            {
                ShowWarning(
                    GetCampaignWarningText("ContinueGameWarning", "WarningTitle", "Unfinished Game"),
                    GetCampaignWarningText("ContinueGameWarning", "WarningText",
                        "You have an unfinished game for this mission.@@Starting a new game will abandon your previous progress. Are you sure you want to continue?"),
                    () => LaunchMission(mission));
                return;
            }

            LaunchWithDifficultyChecks(mission);
        }

        /// <summary>
        /// Launches the given mission, but first shows a difficulty warning when the
        /// currently selected difficulty warrants one. The APOC (third) level warns
        /// only the first time it is played, while the additional APOC++ (fourth)
        /// level warns on every launch. The launch is aborted when the player
        /// declines the warning.
        /// </summary>
        private void LaunchWithDifficultyChecks(Mission mission)
        {
            int difficulty = mission.NoDifficulty ? 0 : trbDifficultySelector.Value;

            if (difficulty == Mission.ADDITIONAL_DIFFICULTY_INDEX)
            {
                ShowWarning(
                    GetCampaignWarningText(DIFFICULTY_WARNINGS_SECTION, "WarningTitle", "Difficulty Warning"),
                    GetCampaignWarningText(DIFFICULTY_WARNINGS_SECTION, "ApocPlusPlusWarningText", "Ready to step into the hell?"),
                    () => LaunchMission(mission));
            }
            else if (difficulty == APOC_DIFFICULTY_INDEX && !hasPlayedApocDifficulty)
            {
                ShowWarning(
                    GetCampaignWarningText(DIFFICULTY_WARNINGS_SECTION, "WarningTitle", "Difficulty Warning"),
                    GetCampaignWarningText(DIFFICULTY_WARNINGS_SECTION, "ApocFirstTimeWarningText", "This difficulty is not meant for first-time players."),
                    () =>
                    {
                        hasPlayedApocDifficulty = true;
                        SaveMissionProgress();
                        LaunchMission(mission);
                    });
            }
            else
            {
                LaunchMission(mission);
            }
        }

        /// <summary>
        /// Shows a Yes/No warning dialog for the given text. Proceeding with the
        /// dialog runs <paramref name="onConfirm"/>; declining only closes the dialog.
        /// </summary>
        private void ShowWarning(string title, string text, Action onConfirm)
        {
            XNAMessageBox msgBox = XNAMessageBox.ShowYesNoDialog(WindowManager, title, text);
            msgBox.YesClickedAction = box => { CloseWarning(box); onConfirm(); };
            msgBox.NoClickedAction = CloseWarning;
        }

        /// <summary>
        /// Closes the darkening panel hosting a warning dialog.
        /// </summary>
        private static void CloseWarning(XNAMessageBox messageBox)
        {
            if (messageBox.Parent is DarkeningPanel panel)
            {
                panel.Hide();
                panel.Hidden += (_, _) => panel.WindowManager.RemoveControl(panel);
            }
        }

        /// <summary>
        /// Reads a campaign warning string from the campaign window INI, falling
        /// back to <paramref name="defaultValue"/> when the key is missing or blank.
        /// The value is decoded as an INI string so the <c>@@</c> token becomes a
        /// line break (matching how mission descriptions are written).
        /// </summary>
        private string GetCampaignWarningText(string section, string key, string defaultValue)
        {
            string value = ThemeIni?.GetStringValue(section, key, defaultValue) ?? defaultValue;
            if (string.IsNullOrWhiteSpace(value))
                value = defaultValue;
            return value.FromIniString().L10N($"INI:Campaign:{section}:{key}");
        }

        /// <summary>
        /// Starts a singleplayer mission.
        /// </summary>
        private void LaunchMission(Mission mission)
        {
            missionLaunchTime = DateTime.Now;

            CustomMissionHelper.CopySupplementalMissionFiles(mission);

            FileInfo spawnerSettingsFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.SPAWNER_SETTINGS);

            spawnerSettingsFile.Delete();

            bool copyMapsToSpawnmapINI = ClientConfiguration.Instance.CopyMissionsToSpawnmapINI;

            int difficulty = mission.NoDifficulty ? 0 : trbDifficultySelector.Value;
            int effectiveDifficulty = difficulty == Mission.ADDITIONAL_DIFFICULTY_INDEX ? 2 : difficulty;

            launchedMission = mission;
            launchedDifficulty = difficulty;

            // Clear any mission result left over from a previous game so a stale
            // victory can't be credited to this launch.
            SafePath.DeleteFileIfExists(ProgramConstants.GamePath, "Client/MissionResult.ini");

            string scenario = mission.GetEffectiveScenario(difficulty);
            bool scenarioPathFound = mission.TryGetScenarioFilePath(scenario, out string scenarioPath);

            if (!scenarioPathFound)
            {
                Logger.Log($"CampaignSelector: mission scenario contains invalid path characters. Mission code name: {mission.CodeName}. Scenario: {scenario}. This mission will be launched without applying {nameof(ClientConfiguration.Instance.CopyMissionsToSpawnmapINI)}.");
                copyMapsToSpawnmapINI = false;
            }

            Logger.Log("About to write spawn.ini.");
            IniFile spawnIni = new(spawnerSettingsFile.FullName)
            {
                Comment = "Generated by CnCNet Client"
            };
            IniSection spawnIniSettings = new("Settings");

            if (copyMapsToSpawnmapINI)
                spawnIniSettings.AddKey("Scenario", "spawnmap.ini");
            else
                spawnIniSettings.AddKey("Scenario", scenario);

            // No one wants to play missions on Fastest, so we'll change it to Faster
            if (UserINISettings.Instance.GameSpeed == 0)
                UserINISettings.Instance.GameSpeed.Value = 1;

            spawnIniSettings.AddKey("CampaignID", mission.CampaignID.ToString(CultureInfo.InvariantCulture));
            spawnIniSettings.AddKey("GameSpeed", UserINISettings.Instance.GameSpeed.ToString());

            switch (ClientConfiguration.Instance.ClientGameType)
            {
                case ClientType.YR or ClientType.Ares:
                    spawnIniSettings.AddKey("Ra2Mode", (!mission.RequiredAddon).ToString(CultureInfo.InvariantCulture));
                    break;
                case ClientType.TS:
                    spawnIniSettings.AddKey("Firestorm", mission.RequiredAddon.ToString(CultureInfo.InvariantCulture));
                    break;
                // TODO figure out the RA one
            }

            spawnIniSettings.AddKey("CustomLoadScreen", LoadingScreenController.GetLoadScreenName(mission.Side.ToString()));

            spawnIniSettings.AddKey("IsSinglePlayer", "Yes");
            spawnIniSettings.AddKey("SidebarHack", ClientConfiguration.Instance.SidebarHack.ToString(CultureInfo.InvariantCulture));
            spawnIniSettings.AddKey("Side", mission.Side.ToString(CultureInfo.InvariantCulture));
            spawnIniSettings.AddKey("BuildOffAlly", mission.BuildOffAlly.ToString(CultureInfo.InvariantCulture));

            if (!mission.NoDifficulty)
                UserINISettings.Instance.Difficulty.Value = difficulty;

            spawnIniSettings.AddKey("DifficultyModeHuman", mission.PlayerAlwaysOnNormalDifficulty ? "1" : effectiveDifficulty.ToString(CultureInfo.InvariantCulture));
            spawnIniSettings.AddKey("DifficultyModeComputer", GetComputerDifficulty(effectiveDifficulty).ToString(CultureInfo.InvariantCulture));

            if (mission.IsCustomMission)
            {
                spawnIniSettings.AddKey("CustomMissionID", mission.CustomMissionID.ToString(CultureInfo.InvariantCulture));
            }

            spawnIni.AddSection(spawnIniSettings);
            WriteMissionSectionToSpawnIni(spawnIni, mission, scenario);

            foreach (CampaignCheckBox chkBox in CheckBoxes)
                chkBox.ApplySpawnIniCode(spawnIni);

            foreach (CampaignDropDown dd in DropDowns)
                dd.ApplySpawnIniCode(spawnIni);

            // Apply forced options from GameOptions.ini

            List<string> forcedKeys = gameOptionsIni.GetSectionKeys("CampaignForcedSpawnIniOptions");

            if (forcedKeys != null)
            {
                foreach (string key in forcedKeys)
                {
                    spawnIni.SetStringValue("Settings", key,
                        gameOptionsIni.GetStringValue("CampaignForcedSpawnIniOptions", key, String.Empty));
                }
            }

            spawnIni.WriteIniFile();

            var difficultyIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, DifficultyIniPaths[effectiveDifficulty]));
            string difficultyName = GetDisplayedDifficultyLabel(mission, difficulty, DifficultyNames[effectiveDifficulty]);

            if (copyMapsToSpawnmapINI)
            {
                var mapIni = new IniFile(scenarioPath);

                IniFile.ConsolidateIniFiles(mapIni, difficultyIni);

                foreach (CampaignCheckBox chkBox in CheckBoxes)
                    chkBox.ApplyMapCode(mapIni, gameMode: null);

                foreach (CampaignDropDown dd in DropDowns)
                    dd.ApplyMapCode(mapIni, gameMode: null);

                mapIni.WriteIniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, "spawnmap.ini"));
            }

            if (!mission.NoDifficulty)
                UserINISettings.Instance.Difficulty.Value = difficulty;
            UserINISettings.Instance.SaveSettings();

            if (ClientConfiguration.Instance.ReturnToMainMenuOnMissionLaunch)
                Disable();
            else
                ToggleControls(false);

            discordHandler.UpdatePresence(mission.UntranslatedGUIName, difficultyName, mission.IconPath, true);
            GameProcessLogic.GameProcessExited += GameProcessExited_Callback;

            GameProcessLogic.StartGameProcess(WindowManager);
        }

        public static void WriteMissionSectionToSpawnIni(IniFile spawnIni, Mission mission) =>
            WriteMissionSectionToSpawnIni(spawnIni, mission, mission.Scenario);

        public static void WriteMissionSectionToSpawnIni(IniFile spawnIni, Mission mission, string scenario)
        {
            bool scenarioPathFound = mission.TryGetScenarioFilePath(scenario, out string scenarioPath);
            if (!scenarioPathFound)
            {
                Logger.Log($"CampaignSelector: mission scenario contains invalid path characters. Mission code name: {mission.CodeName}. Scenario: {scenario}. This mission will be launched without mission section data.");
                return;
            }

            IniSection gameMissionConfigSection = mission.IsCustomMission ? mission.GameMissionConfigSection : null;

            if (!mission.IsCustomMission && File.Exists(scenarioPath))
            {
                var mapIni = new IniFile(scenarioPath);
                gameMissionConfigSection = mapIni.GetSection("GameMissionConfig");
            }

            if (gameMissionConfigSection is not null)
            {
                // copy an IniSection
                IniSection spawnIniMissionIniSection = new(scenario);
                string loadingScreenName = string.Empty;
                string loadingScreenPalName = string.Empty;
                foreach (var kvp in gameMissionConfigSection.Keys)
                {
                    if (string.IsNullOrEmpty(kvp.Value))
                    {
                        if (kvp.Key.Equals("LS640BkgdName", StringComparison.InvariantCulture) || kvp.Key.Equals("LS800BkgdName", StringComparison.InvariantCulture))
                            loadingScreenName = kvp.Value;
                        else if (kvp.Key.Equals("LS800BkgdPal", StringComparison.InvariantCulture))
                            loadingScreenPalName = kvp.Value;
                    }

                    spawnIniMissionIniSection.AddKey(kvp.Key, kvp.Value);
                }

                if (string.IsNullOrEmpty(loadingScreenName))
                {
                    string lsFilename = CustomMissionHelper.CustomMissionSupplementDefinition.FirstOrDefault(x => x.extension.Equals("shp", StringComparison.InvariantCultureIgnoreCase)).filename;

                    if (!string.IsNullOrEmpty(lsFilename))
                    {
                        spawnIniMissionIniSection.AddOrReplaceKey("LS640BkgdName", lsFilename);
                        spawnIniMissionIniSection.AddOrReplaceKey("LS800BkgdName", lsFilename);
                    }
                }
                if (string.IsNullOrEmpty(loadingScreenPalName))
                {
                    string palFilename = CustomMissionHelper.CustomMissionSupplementDefinition.FirstOrDefault(x => x.extension.Equals("pal", StringComparison.InvariantCultureIgnoreCase)).filename;

                    if (!string.IsNullOrEmpty(palFilename))
                        spawnIniMissionIniSection.AddOrReplaceKey("LS800BkgdPal", palFilename);
                }

                // append the new IniSection
                spawnIni.AddSection(spawnIniMissionIniSection);
                spawnIni.SetStringValue("Settings", "ReadMissionSection", "Yes");
            }
        }

        private void ToggleControls(bool enabled)
        {
            btnLaunch.AllowClick = enabled;
            btnCancel.AllowClick = enabled;
            btnLoadGame.AllowClick = enabled;
            btnContinue.AllowClick = enabled && CanContinueSelectedMission();
            lbCampaignList.Enabled = enabled;
            trbDifficultySelector.Enabled = enabled;

            foreach (IUserSetting setting in userSettings)
            {
                if (setting is SettingCheckBoxBase cb)
                    cb.AllowChecking = enabled;
                else if (setting is SettingDropDownBase dd)
                    dd.AllowDropDown = enabled;
            }
        }

        private int GetComputerDifficulty(int difficulty) =>
            Math.Abs(difficulty - 2);

        private void GameProcessExited_Callback()
        {
            WindowManager.AddCallback(new Action(GameProcessExited), null);
        }

        protected virtual void GameProcessExited()
        {
            GameProcessLogic.GameProcessExited -= GameProcessExited_Callback;

            CustomMissionHelper.DeleteSupplementalMissionFiles();

            // Logger.Log("GameProcessExited: Updating Discord Presence.");
            discordHandler.UpdatePresence();

            RecordMissionVictoryIfWon();

            UpdateCampaignGameTime();

            if (!ClientConfiguration.Instance.ReturnToMainMenuOnMissionLaunch)
                ToggleControls(true);

            // Handle ResetToDefaultOnGameExit
            {
                // Reset campaign checkboxes
                foreach (CampaignCheckBox cb in CheckBoxes)
                {
                    if (cb.ResetToDefaultOnGameExit)
                        cb.ResetToDefault();
                }

                // Reset user settings
                foreach (IUserSetting setting in userSettings)
                {
                    if (!setting.ResetToDefaultOnGameExit)
                        continue;

                    if (setting is SettingCheckBoxBase cb)
                        cb.Checked = cb.DefaultValue;
                    else if (setting is SettingDropDownBase dd)
                        dd.SelectedIndex = dd.DefaultValue;
                }

                SaveSettings();
            }

            // The mission's continue save may have been created (quit) or deleted
            // (win) while the game was running; refresh the button to match.
            btnContinue.AllowClick = CanContinueSelectedMission();
        }

        /// <summary>
        /// Checks the game's statistics log for a mission victory and, if won, records
        /// the highest difficulty cleared for the launched mission.
        /// </summary>
        private void RecordMissionVictoryIfWon()
        {
            if (launchedMission == null || launchedDifficulty < 0)
                return;

            Mission mission = launchedMission;
            int difficulty = launchedDifficulty;
            launchedMission = null;
            launchedDifficulty = -1;

            if (!DidPlayerWinMission())
                return;

            MergeMissionGlobals();

            if (missionBestDifficulty.TryGetValue(mission.CodeName, out int best) && best >= difficulty)
            {
                Logger.Log($"CampaignSelector: Mission {mission.CodeName} already cleared at difficulty {best}; skipping completion popup.");
                return;
            }

            missionBestDifficulty[mission.CodeName] = difficulty;
            SaveMissionProgress();
            ShowCampaignCompletedNotification(mission, difficulty);
            RefreshMissionList();

            Logger.Log($"CampaignSelector: Mission victory recorded for {mission.CodeName} at difficulty {difficulty}.");
        }

        /// <summary>
        /// Returns whether the current player won the last game, read from the mission
        /// result file written by Phobos when the mission outcome was determined.
        /// </summary>
        private bool DidPlayerWinMission()
        {
            string resultPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "Client/MissionResult.ini");
            if (!File.Exists(resultPath))
                return false;

            try
            {
                var resultIni = new IniFile(resultPath);
                return resultIni.GetBooleanValue("Result", "Won", false);
            }
            catch (Exception ex)
            {
                Logger.Log($"CampaignSelector: Failed to read the mission result file. Reason: {ex}");
            }

            return false;
        }

        /// <summary>
        /// Loads the set of global variables the client has detected as satisfied from
        /// the [Globals] section of the unlocked achievements file.
        /// </summary>
        private void LoadUnlockedGlobals()
        {
            unlockedGlobals.Clear();
            satisfiedGlobalTimes.Clear();

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
                {
                    string value = globalsIni.GetStringValue("Globals", key, string.Empty);
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                    {
                        satisfiedGlobalTimes[key] = time;
                        unlockedGlobals.Add(key);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"CampaignSelector: Failed to read the unlocked globals file. Reason: {ex}");
            }
        }

        /// <summary>
        /// Merges the global variables recorded by Phobos in the mission result file
        /// into the persisted set of satisfied global variables.
        /// </summary>
        private void MergeMissionGlobals()
        {
            string resultPath = SafePath.CombineFilePath(ProgramConstants.GamePath, "Client/MissionResult.ini");
            if (!File.Exists(resultPath))
                return;

            try
            {
                var resultIni = new IniFile(resultPath);
                List<string> keys = resultIni.GetSectionKeys("Globals");
                if (keys == null)
                    return;

                bool changed = false;
                List<string> newlySatisfied = new();
                DateTime now = DateTime.Now;

                foreach (string key in keys)
                {
                    if (resultIni.GetBooleanValue("Globals", key, false) && unlockedGlobals.Add(key))
                    {
                        satisfiedGlobalTimes[key] = now;
                        changed = true;
                        newlySatisfied.Add(key);
                    }
                }

                if (changed)
                {
                    SaveUnlockedAchievements();
                    RecordAchievementUnlocks(newlySatisfied);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"CampaignSelector: Failed to merge mission globals. Reason: {ex}");
            }
        }

        /// <summary>
        /// Records the unlock time for achievements whose NeedUnlock key matches one of
        /// the freshly satisfied global variables.
        /// </summary>
        private void RecordAchievementUnlocks(IEnumerable<string> satisfiedGlobals)
        {
            bool recorded = false;
            DateTime now = DateTime.Now;
            List<string> newlyUnlocked = new();

            foreach (KeyValuePair<string, string> mapping in achievementNeedUnlock)
            {
                bool matches = false;
                foreach (string global in satisfiedGlobals)
                {
                    if (string.Equals(mapping.Value, global, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                    continue;

                if (!unlockedAchievements.ContainsKey(mapping.Key))
                {
                    unlockedAchievements[mapping.Key] = now;
                    newlyUnlocked.Add(mapping.Key);
                    recorded = true;
                }
            }

            if (recorded)
            {
                SaveUnlockedAchievements();
                ShowAchievementUnlockNotification(newlyUnlocked);
            }
        }

        /// <summary>
        /// Shows a notification on the right side of the window listing the achievements
        /// that were just unlocked.
        /// </summary>
        private void ShowAchievementUnlockNotification(IEnumerable<string> achievementSections)
        {
            var ini = new IniFile(SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "Achievements.ini"));
            var unlocked = new List<(Microsoft.Xna.Framework.Graphics.Texture2D Icon, string Name, string Description)>();

            foreach (string section in achievementSections)
            {
                string name = ini.GetStringValue(section, "Name", section).L10N($"INI:Achievements:{section}:Name");
                string description = ini.GetStringValue(section, "Description", string.Empty).L10N($"INI:Achievements:{section}:Description");
                string iconName = ini.GetStringValue(section, "Icon", string.Empty);
                unlocked.Add((LoadAchievementIcon(iconName), name, description));
            }

            int maxToasts = Math.Max(1, NotificationSettings.MaxToasts);
            int achievementSlots = unlocked.Count > maxToasts ? maxToasts - 1 : unlocked.Count;

            for (int i = 0; i < achievementSlots; i++)
            {
                var notification = new AchievementUnlockNotification(WindowManager);
                notification.SetAchievement(unlocked[i].Icon, unlocked[i].Name, unlocked[i].Description);
                WindowManager.AddAndInitializeControl(notification);
                notification.Start();
            }

            if (unlocked.Count > achievementSlots)
            {
                var moreNotification = new AchievementUnlockNotification(WindowManager);
                moreNotification.SetMore(unlocked.Count - achievementSlots);
                WindowManager.AddAndInitializeControl(moreNotification);
                moreNotification.Start();
            }
        }

        /// <summary>
        /// Loads an achievement icon from the "Achievement Icons" resources folder
        /// (falling back to the resources root). Returns null when missing.
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D LoadAchievementIcon(string iconName)
            => LoadLocalizedIcon("Achievement Icons", iconName, fallbackToResourceRoot: true);

        /// <summary>
        /// Cache of icons loaded through <see cref="LoadLocalizedIcon"/>, keyed by the
        /// relative asset path. Icons are otherwise re-decoded from disk on every mission
        /// list refresh (i.e. on every chapter switch), which causes a visible stutter.
        /// A null value means the icon was not found.
        /// </summary>
        private readonly Dictionary<string, Microsoft.Xna.Framework.Graphics.Texture2D> iconCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Loads an icon by name through the asset search paths, so that translations
        /// (and themes) can override it by placing a file with the same relative path
        /// in their folder. Returns null when the icon cannot be found. Results are
        /// cached and owned by this instance (see <see cref="Kill"/>).
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D LoadLocalizedIcon(string folder, string iconName, bool fallbackToResourceRoot = false)
        {
            if (string.IsNullOrEmpty(iconName))
                return null;

            string relativePath = SafePath.CombineFilePath(folder, iconName);

            if (iconCache.TryGetValue(relativePath, out Microsoft.Xna.Framework.Graphics.Texture2D cached))
                return cached;

            Microsoft.Xna.Framework.Graphics.Texture2D result = null;
            if (AssetLoader.AssetExists(relativePath))
                result = AssetLoader.LoadTextureUncached(relativePath);
            else if (fallbackToResourceRoot && AssetLoader.AssetExists(iconName))
                result = AssetLoader.LoadTextureUncached(iconName);

            iconCache[relativePath] = result;
            return result;
        }

        /// <inheritdoc/>
        public override void Kill()
        {
            foreach (Microsoft.Xna.Framework.Graphics.Texture2D texture in iconCache.Values)
                texture?.Dispose();
            iconCache.Clear();

            base.Kill();
        }

        /// <summary>
        /// Shows a notification on the left side of the window reporting the completed
        /// campaign mission and the medal earned for the cleared difficulty.
        /// </summary>
        private void ShowCampaignCompletedNotification(Mission mission, int difficulty)
        {
            var notification = new CampaignCompletedNotification(WindowManager);
            notification.SetCampaign(mission.GUIName, LoadMissionIcon(mission.MissionIcon), LoadMedalTexture(difficulty));
            WindowManager.AddAndInitializeControl(notification);
            notification.Start();
        }

        /// <summary>
        /// Loads the victory badge texture for the given difficulty (0 = bronze star,
        /// 1 = silver star, 2 = gold star, 3 = medal). Returns null when missing.
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D LoadMedalTexture(int difficulty)
        {
            string fileName = difficulty switch
            {
                0 => "bronzestar.png",
                1 => "silverstar.png",
                2 => "goldstar.png",
                _ => "medal.png"
            };

            return LoadLocalizedIcon("CampaignBadges", fileName);
        }

        /// <summary>
        /// Loads a mission's icon from the "Mission Icons" resources folder.
        /// Returns null when missing.
        /// </summary>
        private Microsoft.Xna.Framework.Graphics.Texture2D LoadMissionIcon(string iconName)
            => LoadLocalizedIcon(MISSION_ICONS_FOLDER, iconName);

        /// <summary>
        /// Builds the map of achievement section names to their NeedUnlock global
        /// variable from the achievements INI.
        /// </summary>
        private void LoadAchievementNeedUnlockMappings()
        {
            achievementNeedUnlock.Clear();

            string path = SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "Achievements.ini");
            if (!File.Exists(path))
                return;

            var ini = new IniFile(path);
            foreach (string sectionName in ini.GetSections())
            {
                if (sectionName.Equals("Achievements", StringComparison.OrdinalIgnoreCase))
                    continue;

                string needUnlock = ini.GetStringValue(sectionName, "NeedUnlock", string.Empty);
                if (!string.IsNullOrEmpty(needUnlock))
                    achievementNeedUnlock[sectionName] = needUnlock;
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
                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                        unlockedAchievements[key] = time;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"CampaignSelector: Failed to read the unlocked achievements file. Reason: {ex}");
            }
        }

        /// <summary>
        /// Persists the recorded achievement unlock times and satisfied global variables
        /// to the unlocked achievements file.
        /// </summary>
        private void SaveUnlockedAchievements()
        {
            var ini = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, UNLOCKED_ACHIEVEMENTS_FILE));

            foreach (KeyValuePair<string, DateTime> entry in unlockedAchievements)
                ini.SetStringValue("Achievements", entry.Key,
                    entry.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            foreach (KeyValuePair<string, DateTime> entry in satisfiedGlobalTimes)
                ini.SetStringValue("Globals", entry.Key,
                    entry.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            ini.WriteIniFile();
        }

        /// <summary>
        /// Loads the total campaign game time (in whole seconds) from the campaign game
        /// time file.
        /// </summary>
        private void LoadCampaignGameTime()
        {
            campaignGameTime = TimeSpan.Zero;

            string path = SafePath.CombineFilePath(ProgramConstants.GamePath, CAMPAIGN_GAME_TIME_FILE);
            if (!File.Exists(path))
                return;

            try
            {
                string content = File.ReadAllText(path).Trim();
                if (long.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out long totalSeconds))
                    campaignGameTime = TimeSpan.FromSeconds(totalSeconds);
            }
            catch (Exception ex)
            {
                Logger.Log($"CampaignSelector: Failed to read the campaign game time file. Reason: {ex}");
            }
        }

        /// <summary>
        /// Persists the total campaign game time (in whole seconds) to the campaign game
        /// time file.
        /// </summary>
        private void SaveCampaignGameTime()
        {
            try
            {
                long totalSeconds = (long)campaignGameTime.TotalSeconds;
                File.WriteAllText(
                    SafePath.CombineFilePath(ProgramConstants.GamePath, CAMPAIGN_GAME_TIME_FILE),
                    totalSeconds.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Logger.Log($"CampaignSelector: Failed to save the campaign game time file. Reason: {ex}");
            }
        }

        /// <summary>
        /// Refreshes the Game Time label shown at the top-left of the campaign selector.
        /// </summary>
        private void UpdateGameTimeLabel()
        {
            if (lblGameTime == null)
                return;

            string text = "Game Time: ".L10N("Client:Main:GameTime")
                + string.Format(CultureInfo.InvariantCulture, "{0}:{1:mm\\:ss}",
                    (long)campaignGameTime.TotalHours, campaignGameTime);
            lblGameTime.Text = text;
        }

        /// <summary>
        /// Adds the duration of the just-finished campaign mission to the accumulated
        /// campaign game time, persists it and refreshes the Game Time label.
        /// </summary>
        private void UpdateCampaignGameTime()
        {
            campaignGameTime += DateTime.Now - missionLaunchTime;
            SaveCampaignGameTime();
            UpdateGameTimeLabel();
        }

        /// <summary>
        /// Loads the highest difficulty won per mission from the mission progress file.
        /// </summary>
        private void LoadMissionProgress()
        {
            missionBestDifficulty.Clear();

            string progressPath = SafePath.CombineFilePath(ProgramConstants.GamePath, MISSION_PROGRESS_FILE);
            if (!File.Exists(progressPath))
                return;

            var progressIni = new IniFile(progressPath);

            hasPlayedApocDifficulty = progressIni.GetBooleanValue("Settings", "HasPlayedApocDifficulty", false);

            List<string> keys = progressIni.GetSectionKeys("Progress");
            if (keys == null)
                return;

            foreach (string key in keys)
            {
                int difficulty = progressIni.GetIntValue("Progress", key, -1);
                if (difficulty >= 0 && difficulty <= Mission.ADDITIONAL_DIFFICULTY_INDEX)
                    missionBestDifficulty[key] = difficulty;
            }
        }

        /// <summary>
        /// Saves the highest difficulty won per mission to the mission progress file.
        /// </summary>
        private void SaveMissionProgress()
        {
            var progressIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, MISSION_PROGRESS_FILE));

            foreach (KeyValuePair<string, int> entry in missionBestDifficulty)
                progressIni.SetIntValue("Progress", entry.Key, entry.Value);

            progressIni.SetBooleanValue("Settings", "HasPlayedApocDifficulty", hasPlayedApocDifficulty);

            progressIni.WriteIniFile();
        }

        private void LoadCustomMissions()
        {
            string customMissionsDirectory = SafePath.CombineDirectoryPath(ProgramConstants.GamePath, ClientConfiguration.Instance.CustomMissionPath);
            if (!Directory.Exists(customMissionsDirectory))
                return;

            string[] mapFiles = Directory.GetFiles(customMissionsDirectory, "*.map");
            if (mapFiles.Length == 0)
                return;

            foreach (string mapFilePath in mapFiles)
            {
                var mapFile = new IniFile(mapFilePath);

                IniSection clientMissionDataSection = mapFile.GetSection("ClientMissionConfig");

                if (clientMissionDataSection is null)
                    continue;

                IniSection? gameMissionDataSection = mapFile.GetSection("GameMissionConfig");

                string filename = new FileInfo(mapFilePath).Name;
                string scenario = SafePath.CombineFilePath(ClientConfiguration.Instance.CustomMissionPath, filename);
                Mission mission = Mission.NewCustomMission(clientMissionDataSection, missionCodeName: filename, scenario, gameMissionDataSection);
                AddMission(mission);
            }
        }

        /// <summary>
        /// Parses a Battle(E).ini file into the given mission list. Returns true if succesful (file found), otherwise false.
        /// </summary>
        /// <param name="path">The path of the file, relative to the game directory.</param>
        /// <param name="targetList">The list that parsed missions are added to.</param>
        /// <returns>True if succesful, otherwise false.</returns>
        private bool ParseBattleIni(string path, List<Mission> targetList)
        {
            Logger.Log("Attempting to parse " + path + " to populate mission list.");

            FileInfo battleIniFileInfo = SafePath.GetFile(ProgramConstants.GamePath, path);
            if (!battleIniFileInfo.Exists)
            {
                Logger.Log("File " + path + " not found. Ignoring.");
                return false;
            }

            var battleIni = new IniFile(battleIniFileInfo.FullName);

            List<string> battleKeys = battleIni.GetSectionKeys("Battles");

            if (battleKeys == null)
                return false; // File exists but [Battles] doesn't

            for (int i = 0; i < battleKeys.Count; i++)
            {
                string battleEntry = battleKeys[i];
                string battleSection = battleIni.GetStringValue("Battles", battleEntry, "NOT FOUND");

                if (!battleIni.SectionExists(battleSection))
                    continue;

                var mission = new Mission(battleIni.GetSection(battleSection), missionCodeName: battleEntry);
                targetList.Add(mission);
                AddMission(mission);
            }

            Logger.Log("Finished parsing " + path + ".");
            return true;
        }

        /// <summary>
        /// Load or re-load missions with selected tags.
        /// </summary>
        /// <param name="selectedTags">Missions with at lease one of which tags to be shown. As an exception, null means show all missions.</param>
        /// <param name="loadCustomMissions">True means show official missions. False means show custom missions.</param>
        public void LoadMissionsWithFilter(ISet<string> selectedTags, bool disableCustomMissions = true, bool disableOfficialMissions = false)
        {
            currentSelectedTags = selectedTags;
            currentDisableCustomMissions = disableCustomMissions;
            currentDisableOfficialMissions = disableOfficialMissions;

            groupPath.Clear();
            RefreshMissionList();
        }

        private ISet<string> currentSelectedTags;
        private bool currentDisableCustomMissions = true;
        private bool currentDisableOfficialMissions = false;

        /// <summary>
        /// Rebuilds the mission list according to the current group navigation path.
        /// At the root level the sub-chapter groups are shown; inside a group the
        /// group's missions are shown (with a back row at the top). If no groups are
        /// configured, missions are shown directly.
        /// </summary>
        private void RefreshMissionList()
        {
            selectedMissions.Clear();

            lbCampaignList.IsChangingSize = true;

            // Item textures are shared, cached icons owned by this instance, so they must
            // not be disposed here (see LoadLocalizedIcon / Kill).
            lbCampaignList.Clear();
            lbCampaignList.SelectedIndex = -1;

            List<Mission> filteredMissions = GetFilteredMissions().ToList();

            if (groupPath.Count == 0)
            {
                // Root level. Hide the back label and restore the list's top padding.
                btnBack.Visible = false;
                lbCampaignList.ContentPaddingTop = 0;

                // Only show groups that actually have missions assigned.
                List<CampaignGroup> visibleGroups = topLevelGroups
                    .Where(group => filteredMissions.Any(
                        mission => IsInGroup(mission, group.Key)))
                    .ToList();

                if (visibleGroups.Count > 0)
                {
                    // Show the sub-chapter group cards first, then ungrouped missions.
                    foreach (CampaignGroup group in visibleGroups)
                        AddGroupItem(group);

                    foreach (Mission mission in filteredMissions.Where(
                        mission => string.IsNullOrEmpty(mission.Group)))
                        AddMissionItem(mission);
                }
                else
                {
                    // No groups have missions assigned; show all missions directly.
                    foreach (Mission mission in filteredMissions)
                        AddMissionItem(mission);
                }
            }
            else
            {
                CampaignGroup activeGroup = groupPath[groupPath.Count - 1];

                // Show the back label inside the list's top area and push the items
                // down to leave room for it.
                btnBack.Visible = true;
                lbCampaignList.ContentPaddingTop = BACK_LABEL_HEIGHT;

                foreach (Mission mission in filteredMissions.Where(
                    mission => IsInGroup(mission, activeGroup.Key)))
                    AddMissionItem(mission);
            }

            lbCampaignList.IsChangingSize = false;
            lbCampaignList.TopIndex = 0;
        }

        /// <summary>
        /// Returns the current chapter's missions after applying the custom/official
        /// and tag filters.
        /// </summary>
        private IEnumerable<Mission> GetFilteredMissions()
        {
            IEnumerable<Mission> missions = activeChapterSource != null ? activeChapterSource.Missions : [];

            if (currentDisableCustomMissions && currentDisableOfficialMissions)
            {
                // do nothing
            }
            else if (currentDisableCustomMissions)
            {
                missions = missions.Where(mission => !mission.IsCustomMission);
            }
            else if (currentDisableOfficialMissions)
            {
                missions = missions.Where(mission => mission.IsCustomMission);
            }
            else
            {
                // do nothing
            }

            if (currentSelectedTags != null)
                missions = missions.Where(mission => mission.Tags.Intersect(currentSelectedTags).Any()).ToList();

            return missions;
        }

        private static bool IsInGroup(Mission mission, string groupKey) =>
            string.Equals(mission.Group, groupKey, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns whether the mission's <c>NeedUnlock</c> global variables are all
        /// satisfied. Missions without a <c>NeedUnlock</c> key are always unlocked.
        /// </summary>
        private bool IsMissionUnlocked(Mission mission)
        {
            if (mission.NeedUnlockGlobals.Count == 0)
                return true;

            foreach (string requiredGlobal in mission.NeedUnlockGlobals)
            {
                if (!unlockedGlobals.Contains(requiredGlobal))
                    return false;
            }

            return true;
        }

        private void AddGroupItem(CampaignGroup group)
        {
            var item = new XNAListBoxItem
            {
                Text = group.DisplayName,
                Tag = group,
                TextColor = lbCampaignList.DefaultItemColor
            };

            if (!string.IsNullOrEmpty(group.IconName))
            {
                // The group icon is resolved through the asset search paths (so that
                // translations/themes can override it by placing a same-named file in
                // their folder), with a ".png" extension appended if the name has none.
                Texture2D iconTexture = LoadLocalizedIcon(MISSION_ICONS_FOLDER, group.IconName);
                if (iconTexture == null && !Path.HasExtension(group.IconName))
                    iconTexture = LoadLocalizedIcon(MISSION_ICONS_FOLDER, group.IconName + ".png");

                if (iconTexture != null)
                    item.Texture = iconTexture;
            }

            if (item.Texture == null)
                item.Texture = LoadLocalizedIcon(MISSION_ICONS_FOLDER, DEFAULT_MISSION_ICON_NAME);

            lbCampaignList.AddItem(item);
        }

        private void AddMissionItem(Mission mission)
        {
            selectedMissions.Add(mission);

            var item = new XNAListBoxItem();
            item.Text = mission.GUIName;
            item.Tag = mission;
            if (!mission.Enabled || !IsMissionUnlocked(mission))
            {
                item.TextColor = UISettings.ActiveSettings.DisabledItemColor;
            }
            else if (string.IsNullOrEmpty(mission.Scenario))
            {
                item.TextColor = AssetLoader.GetColorFromString(
                    ClientConfiguration.Instance.ListBoxHeaderColor);
                item.IsHeader = true;
                item.Selectable = false;
            }
            else
            {
                item.TextColor = lbCampaignList.DefaultItemColor;
            }

            item.Texture = LoadLocalizedIcon(MISSION_ICONS_FOLDER, mission.MissionIcon);

            if (item.Texture == null)
                item.Texture = LoadLocalizedIcon(MISSION_ICONS_FOLDER, DEFAULT_MISSION_ICON_NAME);

            lbCampaignList.AddItem(item);
        }

        /// <summary>
        /// Saves settings to an INI file on the file system.
        /// </summary>
        private void SaveSettings()
        {
            SaveUserSettings();
            SaveCampaignSettings();
        }

        private void SaveUserSettings()
        {
            userSettings.ForEach(c => c.Save());
            UserINISettings.Instance.SaveSettings();
        }

        private void SaveCampaignSettings()
        {
            if (!ClientConfiguration.Instance.SaveCampaignGameOptions)
                return;

            try
            {
                FileInfo settingsFileInfo = SafePath.GetFile(ProgramConstants.GamePath, settingsPath);

                settingsFileInfo.Delete();

                var settingsIni = new IniFile(settingsFileInfo.FullName);

                foreach (CampaignDropDown dd in DropDowns)
                    settingsIni.SetStringValue("GameOptions", dd.Name, dd.SelectedIndex.ToString());

                foreach (CampaignCheckBox cb in CheckBoxes)
                    settingsIni.SetStringValue("GameOptions", cb.Name, cb.Checked.ToString());

                settingsIni.WriteIniFile();
            }
            catch (Exception ex)
            {
                Logger.Log($"Saving campaign settings failed! Reason: {ex}");
            }
        }

        /// <summary>
        /// Loads settings from an INI file on the file system.
        /// </summary>
        private void LoadSettings()
        {
            LoadUserSettings();
            LoadCampaignSettings();
        }

        private void LoadUserSettings() => userSettings.ForEach(c => c.Load());

        private void LoadCampaignSettings()
        {
            if (!ClientConfiguration.Instance.SaveCampaignGameOptions)
                return;

            var settingsIni = new IniFile(SafePath.CombineFilePath(ProgramConstants.GamePath, settingsPath));

            foreach (CampaignDropDown dd in DropDowns)
            {
                dd.SelectedIndex = settingsIni.GetIntValue("GameOptions", dd.Name, dd.SelectedIndex);

                if (dd.SelectedIndex > -1 && dd.SelectedIndex < dd.Items.Count)
                    dd.SelectedIndex = dd.SelectedIndex;
            }

            foreach (CampaignCheckBox cb in CheckBoxes)
                cb.Checked = settingsIni.GetBooleanValue("GameOptions", cb.Name, cb.Checked);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
