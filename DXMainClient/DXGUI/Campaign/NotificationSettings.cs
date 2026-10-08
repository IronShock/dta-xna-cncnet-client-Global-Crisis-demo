using System;
using System.IO;

using ClientCore;

using Microsoft.Xna.Framework;

using Rampastring.Tools;
using Rampastring.XNAUI;

namespace DTAClient.DXGUI.Campaign
{
    /// <summary>
    /// Layout and styling for the campaign / achievement slide-in notifications.
    /// Values are read once from Notifications.ini in the base resource path; when the
    /// file (or a key) is missing the built-in defaults are used.
    /// </summary>
    internal static class NotificationSettings
    {
        private const string MAIN_SECTION = "Notifications";
        private const string CAMPAIGN_SECTION = "CampaignCompleted";
        private const string ACHIEVEMENT_SECTION = "AchievementUnlocked";

        public static int Width { get; private set; } = 380;
        public static int ContentHeight { get; private set; } = 88;
        public static int Padding { get; private set; } = 14;
        public static int IconSize { get; private set; } = 64;
        public static int IconGap { get; private set; } = 12;
        public static int RightMargin { get; private set; } = 20;
        public static int TopMargin { get; private set; } = 20;
        public static int SlotGap { get; private set; } = 8;
        public static double SlideDuration { get; private set; } = 0.5;
        public static double HoldDuration { get; private set; } = 5.0;
        public static Color BackgroundColor { get; private set; } = new Color(0, 0, 0, 205);
        public static Color BorderColor { get; private set; } = new Color(185, 200, 215, 255);
        public static Color AccentCampaignColor { get; private set; } = new Color(214, 175, 54, 255);
        public static Color AccentAchievementColor { get; private set; } = new Color(80, 200, 120, 255);
        public static int AccentBarWidth { get; private set; } = 3;
        public static int HeaderFontIndex { get; private set; } = 1;
        public static int NameFontIndex { get; private set; } = 6;
        public static int SubFontIndex { get; private set; } = 1;
        public static bool PointFilterIcons { get; private set; } = true;
        public static int MaxToasts { get; private set; } = 4;
        public static bool ShowBanner { get; private set; } = true;
        public static int BannerMaxHeight { get; private set; } = 110;
        public static string CampaignHeader { get; private set; } = string.Empty;
        public static string AchievementHeader { get; private set; } = string.Empty;

        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;

            string path = SafePath.CombineFilePath(ProgramConstants.GetBaseResourcePath(), "Notifications.ini");
            if (!File.Exists(path))
                return;

            try
            {
                var ini = new CCIniFile(path);

                Width = ini.GetIntValue(MAIN_SECTION, "Width", Width);
                ContentHeight = ini.GetIntValue(MAIN_SECTION, "ContentHeight", ContentHeight);
                Padding = ini.GetIntValue(MAIN_SECTION, "Padding", Padding);
                IconSize = ini.GetIntValue(MAIN_SECTION, "IconSize", IconSize);
                IconGap = ini.GetIntValue(MAIN_SECTION, "IconGap", IconGap);
                RightMargin = ini.GetIntValue(MAIN_SECTION, "RightMargin", RightMargin);
                TopMargin = ini.GetIntValue(MAIN_SECTION, "TopMargin", TopMargin);
                SlotGap = ini.GetIntValue(MAIN_SECTION, "SlotGap", SlotGap);
                SlideDuration = ini.GetSingleValue(MAIN_SECTION, "SlideDuration", (float)SlideDuration);
                HoldDuration = ini.GetSingleValue(MAIN_SECTION, "HoldDuration", (float)HoldDuration);
                BackgroundColor = ReadColor(ini, MAIN_SECTION, "BackgroundColor", BackgroundColor);
                BorderColor = ReadColor(ini, MAIN_SECTION, "BorderColor", BorderColor);
                AccentCampaignColor = ReadColor(ini, MAIN_SECTION, "AccentCampaignColor", AccentCampaignColor);
                AccentAchievementColor = ReadColor(ini, MAIN_SECTION, "AccentAchievementColor", AccentAchievementColor);
                AccentBarWidth = ini.GetIntValue(MAIN_SECTION, "AccentBarWidth", AccentBarWidth);
                HeaderFontIndex = ini.GetIntValue(MAIN_SECTION, "HeaderFontIndex", HeaderFontIndex);
                NameFontIndex = ini.GetIntValue(MAIN_SECTION, "NameFontIndex", NameFontIndex);
                SubFontIndex = ini.GetIntValue(MAIN_SECTION, "SubFontIndex", SubFontIndex);
                PointFilterIcons = ini.GetBooleanValue(MAIN_SECTION, "PointFilterIcons", PointFilterIcons);
                MaxToasts = ini.GetIntValue(MAIN_SECTION, "MaxToasts", MaxToasts);

                ShowBanner = ini.GetBooleanValue(CAMPAIGN_SECTION, "ShowBanner", ShowBanner);
                BannerMaxHeight = ini.GetIntValue(CAMPAIGN_SECTION, "BannerMaxHeight", BannerMaxHeight);
                CampaignHeader = ini.GetStringValue(CAMPAIGN_SECTION, "Header", CampaignHeader);

                AchievementHeader = ini.GetStringValue(ACHIEVEMENT_SECTION, "Header", AchievementHeader);
            }
            catch (Exception ex)
            {
                Logger.Log($"NotificationSettings: Failed to read Notifications.ini. Reason: {ex}");
            }
        }

        private static Color ReadColor(IniFile ini, string section, string key, Color fallback)
        {
            string value = ini.GetStringValue(section, key, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return AssetLoader.GetColorFromString(value, fallback);
        }
    }
}
