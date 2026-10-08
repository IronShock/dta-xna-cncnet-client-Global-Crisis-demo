using System;
using System.Collections.Generic;

using ClientCore;
using ClientGUI;

using DTAClient.Domain;
using DTAClient.DXGUI.Generic;

using Microsoft.Xna.Framework;

using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// The entry point of the campaign interface.
    /// Opens the mission selector directly; Database and Achievements are opened
    /// from the top-right corner buttons of the selector itself.
    /// </summary>
    public class CampaignHub : INItializableWindow
    {
        private readonly DiscordHandler discordHandler;
        private readonly IServiceProvider serviceProvider;

        private CampaignSelector storySelector;
        private DatabaseWindow databaseWindow;
        private AchievementsWindow achievementsWindow;

        public CampaignHub(WindowManager windowManager, DiscordHandler discordHandler, IServiceProvider serviceProvider)
            : base(windowManager)
        {
            this.discordHandler = discordHandler;
            this.serviceProvider = serviceProvider;
        }

        public IReadOnlyDictionary<int, Mission> UniqueIDToMissions { get; private set; }
        public IReadOnlyCollection<Mission> AllMissions { get; private set; }

        public override void Initialize()
        {
            // Resolve the singleton load-game window lazily (GameLoadingWindow
            // depends on CampaignHub, so constructor injection would be circular).
            GameLoadingWindow gameLoadingWindow = (GameLoadingWindow)serviceProvider.GetService(typeof(GameLoadingWindow));

            storySelector = new CampaignSelector(WindowManager, discordHandler, "CampaignStory", "INI/BattleStory.ini", gameLoadingWindow);
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, storySelector);
            storySelector.Disable();

            databaseWindow = new DatabaseWindow(WindowManager, "DatabaseWindow");
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, databaseWindow);
            databaseWindow.Disable();

            achievementsWindow = new AchievementsWindow(WindowManager, "AchievementsWindow");
            DarkeningPanel.AddAndInitializeWithControl(WindowManager, achievementsWindow);
            achievementsWindow.Disable();

            var allMissions = new List<Mission>();
            var uniqueIDToMissions = new Dictionary<int, Mission>();

            foreach (Mission mission in storySelector.AllMissions)
            {
                allMissions.Add(mission);
                if (!uniqueIDToMissions.ContainsKey(mission.CustomMissionID))
                    uniqueIDToMissions.Add(mission.CustomMissionID, mission);
            }

            AllMissions = allMissions;
            UniqueIDToMissions = uniqueIDToMissions;

            storySelector.OpenDatabaseRequested += (s, e) => databaseWindow.Enable();
            storySelector.OpenAchievementsRequested += (s, e) => achievementsWindow.Enable();

            Name = nameof(CampaignHub);
            ClientRectangle = new Rectangle(0, 0, WindowManager.RenderResolutionX, WindowManager.RenderResolutionY);
            BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 255), Width, Height);
            BorderColor = UISettings.ActiveSettings.PanelBorderColor;

            base.Initialize();

            WindowManager.CenterControlOnScreen(this);
        }

        public void Open()
        {
            // Only the mission selector is shown; the hub itself is a pass-through.
            // Enabling both windows here would keep the hub's darkening panel on
            // screen after the selector is closed, blocking the way back.
            storySelector.Enable();
        }
    }
}
