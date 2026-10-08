using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using ClientCore;
using ClientCore.Enums;
using ClientCore.Extensions;

using Rampastring.Tools;

namespace DTAClient.Domain
{
    /// <summary>
    /// A Tiberian Sun mission listed in Battle(E).ini.
    /// </summary>
    public class Mission
    {
        public Mission(IniSection missionSection, string missionCodeName)
        {
            if (missionSection == null)
                throw new ArgumentNullException(nameof(missionSection));

            CD = missionSection.GetIntValue(nameof(CD), 0);
            Side = missionSection.GetIntValue(nameof(Side), 0);
            Scenario = missionSection.GetStringValue(nameof(Scenario), string.Empty);
            UntranslatedGUIName = missionSection.GetStringValue("Description", "Undefined mission");
            GUIName = UntranslatedGUIName
                .L10N($"INI:Missions:{missionCodeName}:Description");

            IconPath = missionSection.GetStringValue("SideName", string.Empty);
            GUIDescription = missionSection.GetStringValue("LongDescription", string.Empty)
                .FromIniString()
                .L10N($"INI:Missions:{missionCodeName}:LongDescription");
            FinalMovie = missionSection.GetStringValue(nameof(FinalMovie), "none");
            RequiredAddon = missionSection.GetBooleanValue(nameof(RequiredAddon),
               ClientConfiguration.Instance.ClientGameType == ClientType.YR ||
               ClientConfiguration.Instance.ClientGameType == ClientType.Ares ?
                true :  // In case of YR this toggles Ra2Mode instead which should not be default
                false
            );
            Enabled = missionSection.GetBooleanValue(nameof(Enabled), true);
            NeedUnlockGlobals = ParseNeedUnlock(missionSection.GetStringValue("NeedUnlock", string.Empty));
            BuildOffAlly = missionSection.GetBooleanValue(nameof(BuildOffAlly), false);
            PlayerAlwaysOnNormalDifficulty = missionSection.GetBooleanValue(nameof(PlayerAlwaysOnNormalDifficulty), false);
            NoDifficulty = missionSection.GetBooleanValue(nameof(NoDifficulty), false);
            Tags = missionSection.GetStringValue(nameof(Tags), string.Empty).SplitWithCleanup();
            Group = missionSection.GetStringValue(nameof(Group), string.Empty);
            string[] rawDifficultyLabels = ParseDifficultyLabels(missionSection.GetStringValue("DifficultyLabel", string.Empty));
            DifficultyLabels = new string[rawDifficultyLabels.Length];
            for (int i = 0; i < rawDifficultyLabels.Length; i++)
                DifficultyLabels[i] = rawDifficultyLabels[i].L10N($"INI:Missions:DifficultyLabel:{rawDifficultyLabels[i]}");
            AdditionalDifficulty = missionSection.GetBooleanValue(nameof(AdditionalDifficulty), false);
            AdditionalMap = missionSection.GetStringValue(nameof(AdditionalMap), string.Empty);

            CodeName = missionCodeName;
            CustomMissionID = ComputeCustomMissionID(missionCodeName);
            PreviewImage = missionSection.GetStringValue("PreviewImage", string.Empty);
            MissionIcon = missionSection.GetStringValue("MissionIcon", string.Empty);
            ParseLocation(missionSection.GetStringValue("Location", string.Empty));
            ParseGlobeImages(missionSection);
        }

        public static Mission NewCustomMission(IniSection clientMissionConfigSection, string missionCodeName, string scenario, IniSection? gameMissionConfigSection)
        {
            var mission = new Mission(clientMissionConfigSection, missionCodeName)
            {
                IsCustomMission = true,
                Scenario = scenario,
                GameMissionConfigSection = gameMissionConfigSection,
                Tags = ["CUSTOM"],
            };
            return mission;
        }

        private static int ComputeCustomMissionID(string missionCodeName)
        {
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA1850 // Prefer static 'HashData' method over 'ComputeHash'
            using var sha1 = SHA1.Create();
            byte[] digest = sha1.ComputeHash(Encoding.UTF8.GetBytes(missionCodeName));
            return BinaryPrimitives.ReadInt32LittleEndian(digest);
#pragma warning restore CA1850 // Prefer static 'HashData' method over 'ComputeHash'
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        }

        public string CodeName { get; private set; }
        public int CampaignID { get; } = -1;
        public int CustomMissionID { get; private set; }

        public int CD { get; private set; }
        public int Side { get; private set; }

        /// <summary>
        /// Refers to the map file. Must be a relative path to the game folder. If it contains invalid path characters like '>', the client treats it as a special scenario that does not have a map file, passing the string directly to the spawner.
        /// </summary>
        public string Scenario { get; private set; }
        public string GUIName { get; private set; }
        public string UntranslatedGUIName { get; private set; }
        public string IconPath { get; private set; }
        public string GUIDescription { get; private set; }
        public string FinalMovie { get; private set; }
        public bool RequiredAddon { get; private set; }
        public bool Enabled { get; set; }

        /// <summary>
        /// The global variables that must all be satisfied before this mission can be
        /// launched, parsed from the comma-separated <c>NeedUnlock</c> key (matching the
        /// achievements mechanism). Empty means the mission is always available.
        /// Matching is case-insensitive.
        /// </summary>
        public List<string> NeedUnlockGlobals { get; private set; } = new();

        public bool BuildOffAlly { get; private set; }
        public bool PlayerAlwaysOnNormalDifficulty { get; private set; }

        /// <summary>
        /// Whether the mission hides the difficulty selector and is always launched
        /// on Easy difficulty, as defined by the <c>NoDifficulty</c> key.
        /// </summary>
        public bool NoDifficulty { get; private set; }
        public IReadOnlyCollection<string> Tags { get; private set; }

        /// <summary>
        /// The faction/campaign group this mission belongs to, as defined by the
        /// <c>Group</c> key in the mission's INI section. Used by the campaign
        /// selector's sub-chapter navigation (e.g. "Allied" / "Soviet").
        /// </summary>
        public string Group { get; private set; }

        public const int ADDITIONAL_DIFFICULTY_INDEX = 3;

        /// <summary>
        /// Custom labels for the difficulty levels (Easy/Medium/Hard, plus the
        /// additional difficulty when <see cref="AdditionalDifficulty"/> is enabled),
        /// parsed from the comma-separated <c>DifficultyLabel</c> key (up to four
        /// values). They override the difficulty text shown in the campaign selector
        /// and in the Discord presence for the currently selected difficulty level.
        /// </summary>
        public string[] DifficultyLabels { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Parses the <c>DifficultyLabel</c> key into up to four custom difficulty
        /// labels. Returns an empty array if the key is missing or blank.
        /// </summary>
        private static string[] ParseDifficultyLabels(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return Array.Empty<string>();

            string[] parts = rawValue.Split(',');
            string[] labels = new string[Math.Min(4, parts.Length)];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = parts[i].Trim();

            return labels;
        }

        /// <summary>
        /// Whether the mission exposes an additional (fourth) difficulty level in the
        /// campaign selector, as defined by the <c>AdditionalDifficulty</c> key.
        /// </summary>
        public bool AdditionalDifficulty { get; private set; }

        /// <summary>
        /// The map used when the mission is launched with the additional difficulty
        /// level, as defined by the <c>AdditionalMap</c> key. Must be a relative path
        /// to the game folder. Falls back to <see cref="Scenario"/> when not set.
        /// </summary>
        public string AdditionalMap { get; private set; }

        /// <summary>
        /// Returns the scenario (map file) that should be launched for the given
        /// difficulty level. The additional difficulty uses <see cref="AdditionalMap"/>
        /// when it is set; every other level uses <see cref="Scenario"/>.
        /// </summary>
        public string GetEffectiveScenario(int difficultyLevel)
        {
            if (difficultyLevel == ADDITIONAL_DIFFICULTY_INDEX && !string.IsNullOrEmpty(AdditionalMap))
                return AdditionalMap;

            return Scenario;
        }

        /// <summary>
        /// This property is not set through the ini file.
        /// For a user custom mission, "scenario" will be assumed as the filename of a map file, with the suffix ".map" (case-insensitive).
        /// The map file is assumed to be placed at ClientConfiguration.CustomMissionPath.
        /// When launching a user custom mission, all supplemental files, i.e., files with the same filename (excepts for the suffix), will be temporarily copied into game folder.
        /// </summary>
        public bool IsCustomMission { get; private set; }

        public IniSection? GameMissionConfigSection { get; set; }

        public string PreviewImage { get; private set; }

        /// <summary>
        /// The filename of the icon image shown for this mission in the campaign
        /// selector's mission list (as opposed to <see cref="PreviewImage"/>, which
        /// is the larger image shown in the preview panel). The image is expected to
        /// be located in the "Mission Icons" resource folder.
        /// </summary>
        public string MissionIcon { get; private set; }

        /// <summary>
        /// The longitude of the mission's location on the world map (degrees, positive east),
        /// parsed from the <c>Location</c> key. Null if not set or invalid.
        /// </summary>
        public double? LocationLongitude { get; private set; }

        /// <summary>
        /// The latitude of the mission's location on the world map (degrees, positive north),
        /// parsed from the <c>Location</c> key. Null if not set or invalid.
        /// </summary>
        public double? LocationLatitude { get; private set; }

        /// <summary>
        /// Parses the <c>Location</c> key, which is expected to be "longitude,latitude"
        /// (e.g. "-77.0,38.9"). On failure both coordinates are left as null.
        /// </summary>
        private void ParseLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return;

            string[] parts = location.Split(',');
            if (parts.Length < 2)
                return;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
                return;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude))
                return;

            LocationLongitude = longitude;
            LocationLatitude = latitude;
        }

        /// <summary>
        /// Parses the comma-separated <c>NeedUnlock</c> key into the list of global
        /// variables that must all be satisfied to unlock the mission. Blank entries
        /// are ignored; an empty result means the mission is always available.
        /// </summary>
        private static List<string> ParseNeedUnlock(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (string part in value.Split(','))
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    result.Add(trimmed);
            }

            return result;
        }

        /// <summary>
        /// The images placed on the world map globe for this mission, parsed from the
        /// <c>ImageN</c> keys (format "image.png,longitude,latitude[,size%]"). Shown
        /// baked onto the globe while this mission is selected.
        /// </summary>
        public List<GlobeImage> GlobeImages { get; private set; } = new();

        /// <summary>
        /// Parses the <c>Image0</c>...<c>ImageN</c> keys into globe overlay images.
        /// Entries with a missing key end the list; malformed entries are skipped.
        /// </summary>
        private void ParseGlobeImages(IniSection missionSection)
        {
            GlobeImages.Clear();

            for (int i = 0; i < 32; i++)
            {
                string value = missionSection.GetStringValue("Image" + i, string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                    break;

                string[] parts = value.Split(',');
                if (parts.Length < 3)
                    continue;

                string imagePath = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(imagePath))
                    continue;

                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
                    continue;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude))
                    continue;

                float? heightPercent = null;
                if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]))
                {
                    string sizeText = parts[3].Trim().TrimEnd('%');
                    if (float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedPercent))
                        heightPercent = parsedPercent;
                }

                GlobeImages.Add(new GlobeImage(imagePath, longitude, latitude, heightPercent));
            }
        }

        public bool TryGetScenarioFilePath(out string scenarioFilePath) =>
            TryGetScenarioFilePath(Scenario, out scenarioFilePath);

        public bool TryGetScenarioFilePath(string scenario, out string scenarioFilePath)
        {
            scenarioFilePath = null;
            if (string.IsNullOrEmpty(scenario))
                return false;

            foreach (string segment in scenario.Split('/', '\\'))
            {
                if (string.IsNullOrEmpty(segment) || segment != segment.ToWin32FileName())
                    return false;
            }

            scenarioFilePath = SafePath.CombineFilePath(ProgramConstants.GamePath, scenario);
            return true;
        }
    }
}