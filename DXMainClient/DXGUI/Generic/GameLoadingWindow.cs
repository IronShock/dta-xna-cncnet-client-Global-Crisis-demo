using ClientCore;
using ClientGUI;
using DTAClient.Domain;
using ClientCore.Extensions;
using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using DTAClient.DXGUI.Campaign;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// A window for loading saved singleplayer games.
    /// </summary>
    public class GameLoadingWindow : XNAWindow
    {
        private const string SAVED_GAMES_DIRECTORY = "Saved Games";

        /// <summary>
        /// File name prefix of the dedicated per-mission continue saves written by
        /// Phobos when the player leaves a campaign mission. These are hidden from
        /// the regular load list and only used by the campaign screen's
        /// "Continue Game" button.
        /// </summary>
        public const string CONTINUE_GAME_PREFIX = "ContinueGame_";

        public GameLoadingWindow(WindowManager windowManager, DiscordHandler discordHandler, CampaignHub campaignHub) : base(windowManager)
        {
            this.discordHandler = discordHandler;
            this.campaignHub = campaignHub;
        }

        private DiscordHandler discordHandler;
        private CampaignHub campaignHub;

        private XNAMultiColumnListBox lbSaveGameList;
        private XNAClientButton btnLaunch;
        private XNAClientButton btnDelete;
        private XNAClientButton btnCancel;

        private List<SavedGame> savedGames = new List<SavedGame>();

        public override void Initialize()
        {
            Name = "GameLoadingWindow";
            BackgroundTexture = AssetLoader.LoadTexture("loadmissionbg.png");

            ClientRectangle = new Rectangle(0, 0, 600, 380);
            CenterOnParent();

            lbSaveGameList = new XNAMultiColumnListBox(WindowManager);
            lbSaveGameList.Name = nameof(lbSaveGameList);
            lbSaveGameList.ClientRectangle = new Rectangle(13, 13, 574, 317);
            lbSaveGameList.AddColumn("SAVED GAME NAME".L10N("Client:Main:SavedGameNameColumnHeader"), 400);
            lbSaveGameList.AddColumn("DATE / TIME".L10N("Client:Main:SavedGameDateTimeColumnHeader"), 174);
            lbSaveGameList.BackgroundTexture = AssetLoader.CreateTexture(new Color(0, 0, 0, 128), 1, 1);
            lbSaveGameList.PanelBackgroundDrawMode = PanelBackgroundImageDrawMode.STRETCHED;
            lbSaveGameList.SelectedIndexChanged += ListBox_SelectedIndexChanged;
            lbSaveGameList.AllowKeyboardInput = true;

            btnLaunch = new XNAClientButton(WindowManager);
            btnLaunch.Name = nameof(btnLaunch);
            btnLaunch.ClientRectangle = new Rectangle(125, 345, 110, 23);
            btnLaunch.Text = "Load".L10N("Client:Main:ButtonLoad");
            btnLaunch.AllowClick = false;
            btnLaunch.LeftClick += BtnLaunch_LeftClick;

            btnDelete = new XNAClientButton(WindowManager);
            btnDelete.Name = nameof(btnDelete);
            btnDelete.ClientRectangle = new Rectangle(btnLaunch.Right + 10, btnLaunch.Y, 110, 23);
            btnDelete.Text = "Delete".L10N("Client:Main:ButtonDelete");
            btnDelete.AllowClick = false;
            btnDelete.LeftClick += BtnDelete_LeftClick;

            btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = nameof(btnCancel);
            btnCancel.ClientRectangle = new Rectangle(btnDelete.Right + 10, btnLaunch.Y, 110, 23);
            btnCancel.Text = "Cancel".L10N("Client:Main:ButtonCancel");
            btnCancel.LeftClick += BtnCancel_LeftClick;

            AddChild(lbSaveGameList);
            AddChild(btnLaunch);
            AddChild(btnDelete);
            AddChild(btnCancel);

            base.Initialize();

            ListSaves();
        }

        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbSaveGameList.SelectedIndex == -1)
            {
                btnLaunch.AllowClick = false;
                btnDelete.AllowClick = false;
            }
            else
            {
                btnLaunch.AllowClick = true;
                btnDelete.AllowClick = true;
            }
        }

        public void Open()
        {
            Enable();
        }

        private void BtnCancel_LeftClick(object sender, EventArgs e)
        {
            Disable();
        }

        private void BtnLaunch_LeftClick(object sender, EventArgs e)
        {
            SavedGame sg = savedGames[lbSaveGameList.SelectedIndex];
            LaunchSaveGame(sg);
        }

        /// <summary>
        /// Launches the game to load the given saved game, resolving the mission
        /// from the saved game's custom mission ID when possible.
        /// </summary>
        public void LaunchSaveGame(SavedGame sg)
        {
            Mission mission = campaignHub.UniqueIDToMissions.GetValueOrDefault(sg.CustomMissionID, null);
            LaunchSaveGame(sg, mission);
        }

        /// <summary>
        /// Launches the game to load the given saved game. When <paramref name="mission"/>
        /// is provided it is used for supplemental files and the spawn.ini mission
        /// section, which is required for continue saves whose custom mission ID
        /// could not be parsed from the save file.
        /// </summary>
        public void LaunchSaveGame(SavedGame sg, Mission mission)
        {
            Logger.Log("Loading saved game " + sg.FileName);

            CustomMissionHelper.DeleteSupplementalMissionFiles();

            if (mission != null)
                CustomMissionHelper.CopySupplementalMissionFiles(mission);

            FileInfo spawnerSettingsFile = SafePath.GetFile(ProgramConstants.GamePath, ProgramConstants.SPAWNER_SETTINGS);

            if (spawnerSettingsFile.Exists)
                spawnerSettingsFile.Delete();

            IniFile spawnIni = new()
            {
                Comment = "Generated by CnCNet Client"
            };

            IniSection spawnIniSettings = new("Settings");
            spawnIniSettings.AddKey("Scenario", "spawnmap.ini");
            spawnIniSettings.AddKey("SaveGameName", sg.FileName);
            spawnIniSettings.AddKey("LoadSaveGame", "Yes");
            spawnIniSettings.AddKey("SidebarHack", ClientConfiguration.Instance.SidebarHack.ToString());
            spawnIniSettings.AddKey("CustomLoadScreen", LoadingScreenController.GetLoadScreenName("g"));
            spawnIniSettings.AddKey("Firestorm", "No");
            spawnIniSettings.AddKey("GameSpeed", UserINISettings.Instance.GameSpeed.ToString());

            spawnIni.AddSection(spawnIniSettings);

            if (mission != null)
            {
                spawnIniSettings.AddKey("CustomMissionID", mission.CustomMissionID.ToString());
                CampaignSelector.WriteMissionSectionToSpawnIni(spawnIni, mission);
            }

            spawnIni.WriteIniFile(spawnerSettingsFile.FullName);

            FileInfo spawnMapIniFile = SafePath.GetFile(ProgramConstants.GamePath, "spawnmap.ini");

            if (spawnMapIniFile.Exists)
                spawnMapIniFile.Delete();

            using (var spawnMapStreamWriter = new StreamWriter(spawnMapIniFile.FullName))
            {
                spawnMapStreamWriter.WriteLine("[Map]");
                spawnMapStreamWriter.WriteLine("Size=0,0,50,50");
                spawnMapStreamWriter.WriteLine("LocalSize=0,0,50,50");
                spawnMapStreamWriter.WriteLine();
            }

            discordHandler.UpdatePresence(sg.GUIName, true);

            Disable();
            GameProcessLogic.GameProcessExited += GameProcessExited_Callback;

            GameProcessLogic.StartGameProcess(WindowManager);
        }

        private void BtnDelete_LeftClick(object sender, EventArgs e)
        {
            SavedGame sg = savedGames[lbSaveGameList.SelectedIndex];
            var msgBox = new XNAMessageBox(WindowManager, "Delete Confirmation".L10N("Client:Main:DeleteConfirmationTitle"),
                string.Format(("The following saved game will be deleted permanently:\n\n" +
                    "Filename: {0}\n" +
                    "Saved game name: {1}\n" +
                    "Date and time: {2}\n\n" +
                    "Are you sure you want to proceed?").L10N("Client:Main:DeleteConfirmationText"),
                    sg.FileName, Renderer.GetSafeString(sg.GUIName, lbSaveGameList.FontIndex), sg.LastModified.ToString()),
                XNAMessageBoxButtons.YesNo);
            msgBox.Show();
            msgBox.YesClickedAction = DeleteMsgBox_YesClicked;
        }

        private void DeleteMsgBox_YesClicked(XNAMessageBox obj)
        {
            SavedGame sg = savedGames[lbSaveGameList.SelectedIndex];

            Logger.Log("Deleting saved game " + sg.FileName);
            SafePath.DeleteFileIfExists(ProgramConstants.GamePath, SAVED_GAMES_DIRECTORY, sg.FileName);
            ListSaves();
        }

        private void GameProcessExited_Callback()
        {
            WindowManager.AddCallback(new Action(GameProcessExited), null);
        }

        protected virtual void GameProcessExited()
        {
            GameProcessLogic.GameProcessExited -= GameProcessExited_Callback;

            CustomMissionHelper.DeleteSupplementalMissionFiles();

            discordHandler.UpdatePresence();
        }

        public void ListSaves()
        {
            savedGames.Clear();
            lbSaveGameList.ClearItems();
            lbSaveGameList.SelectedIndex = -1;

            DirectoryInfo savedGamesDirectoryInfo = SafePath.GetDirectory(ProgramConstants.GamePath, SAVED_GAMES_DIRECTORY);

            if (!savedGamesDirectoryInfo.Exists)
            {
                Logger.Log("Saved Games directory not found!");
                return;
            }

            IEnumerable<FileInfo> files = savedGamesDirectoryInfo.EnumerateFiles("*.SAV", SearchOption.TopDirectoryOnly);

            foreach (FileInfo file in files)
            {
                // Continue saves are owned by the campaign screen's "Continue Game"
                // button and must not appear in the regular load list.
                if (file.Name.StartsWith(CONTINUE_GAME_PREFIX, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Note: ParseSaveGame modifies savedGames
                ParseSaveGame(file.FullName);
            }

            savedGames = savedGames.OrderBy(sg => sg.LastModified.Ticks).ToList();
            savedGames.Reverse();

            foreach (SavedGame sg in savedGames)
            {
                string[] item = new string[] {
                    Renderer.GetSafeString(sg.GUIName, lbSaveGameList.FontIndex),
                    sg.LastModified.ToString() };
                lbSaveGameList.AddItem(item, true);
            }
        }

        private void ParseSaveGame(string fileName)
        {
            string shortName = Path.GetFileName(fileName);

            SavedGame sg = new SavedGame(shortName);
            if (sg.ParseInfo())
                savedGames.Add(sg);
        }

        /// <summary>
        /// Returns the file name of the existing continue save for the given mission
        /// (matching the map Phobos wrote it under), or null if the mission has no
        /// continue save yet. The additional-difficulty map is checked too.
        /// </summary>
        public string GetContinueSaveFileName(Mission mission)
        {
            if (mission == null)
                return null;

            List<string> scenarios = new();
            if (!string.IsNullOrEmpty(mission.Scenario))
                scenarios.Add(mission.Scenario);
            if (!string.IsNullOrEmpty(mission.AdditionalMap))
                scenarios.Add(mission.AdditionalMap);

            foreach (string scenario in scenarios)
            {
                string baseName = Path.GetFileNameWithoutExtension(scenario);
                if (string.IsNullOrEmpty(baseName))
                    continue;

                string fileName = CONTINUE_GAME_PREFIX + baseName + ".cgs";
                if (SafePath.GetFile(ProgramConstants.GamePath, SAVED_GAMES_DIRECTORY, fileName).Exists)
                    return fileName;
            }

            return null;
        }
    }
}
