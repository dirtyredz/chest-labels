using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// How a named chest's label is exposed to screen readers (e.g. MoonlightAccess) through
    /// the game's own CustomName field. See <see cref="ChestPatches"/> for the read-only patch
    /// that applies it - the save is never written.
    /// </summary>
    public enum ScreenReaderNameMode
    {
        /// <summary>Leave the game's name field untouched.</summary>
        Off,

        /// <summary>Return just the label, e.g. "Pantry".</summary>
        LabelOnly,

        /// <summary>Return the object's type and label, e.g. "Storage Crate named Pantry".</summary>
        TypeAndLabel,
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Moonlight Peaks.exe")]
    public sealed class ChestLabelsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dirtyredz.moonlightpeaks.chestlabels";
        public const string PluginName = "Chest Labels";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static LabelStore Store;

        internal static ConfigEntry<bool> ShowHeader;
        internal static ConfigEntry<bool> VerboseLogging;
        internal static ConfigEntry<KeyboardShortcut> ReloadKey;
        internal static ConfigEntry<float> HeaderFontSize;
        internal static ConfigEntry<float> OrnamentOffsetY;
        internal static ConfigEntry<float> TitleOffsetY;
        internal static ConfigEntry<float> PanelTopPadding;
        internal static ConfigEntry<float> HeaderDropY;
        internal static ConfigEntry<float> LineOffsetY;
        internal static ConfigEntry<bool> ShowEditButton;
        internal static ConfigEntry<bool> LogUiDiagnostics;
        internal static ConfigEntry<bool> ShowHoverLabel;
        internal static ConfigEntry<ScreenReaderNameMode> ScreenReaderName;
        internal static ConfigEntry<bool> UseGameInteractionLabel;
        internal static ConfigEntry<bool> UseGameNameplate;
        internal static ConfigEntry<string> NameplateTint;
        internal static ConfigEntry<bool> HideArrowWhenNamed;
        internal static ConfigEntry<float> HoverHeight;
        internal static ConfigEntry<float> HoverFontSize;
        internal static ConfigEntry<float> HoverBackgroundAlpha;

        /// <summary>
        /// Set when UI injection throws. The header is then left alone for the rest of the
        /// session rather than throwing once per chest open.
        /// </summary>
        internal static bool HeaderDisabledAfterError;

        private readonly Harmony harmony = new Harmony(PluginGuid);

        // Mod Menu reads these strings out of ConfigDescription.Tags and uses them to title its
        // sections. They are display names only - the .cfg section keys are untouched, because
        // renaming a section there would orphan every existing player's saved values.
        private const string ChestWindow = "ModMenu.Section=Chest window";
        private const string HoverLabelSection = "ModMenu.Section=Hover label";
        private const string Renaming = "ModMenu.Section=Renaming";
        private const string ScreenReaderSection = "ModMenu.Section=Screen reader";
        private const string Diagnostics = "ModMenu.Section=Diagnostics";

        private void Awake()
        {
            Log = Logger;

            ShowHeader = Config.Bind(
                "Display", "ShowHeader", true,
                new ConfigDescription(
                    "Show the chest's label at the top of the chest screen.",
                    null,
                    ChestWindow, "ModMenu.Label=Show the chest's name"));

            VerboseLogging = Config.Bind(
                "Diagnostics", "VerboseLogging", false,
                new ConfigDescription(
                    "Log each chest's GUID and contents when opened. Useful while labels are " +
                    "still edited by hand; turn off once you no longer need it.",
                    null,
                    Diagnostics, "ModMenu.Label=Log chest contents"));

            ReloadKey = Config.Bind(
                "Editing", "ReloadKey", new KeyboardShortcut(KeyCode.F9),
                new ConfigDescription(
                    "Reload labels from the sidecar file on disk. Lets you hand-edit the JSON " +
                    "and see the result without restarting the game.",
                    null,
                    Renaming, "ModMenu.Label=Reload labels from disk"));

            HeaderFontSize = Config.Bind(
                "Display", "HeaderFontSize", 33f,
                new ConfigDescription(
                    "Maximum font size of the header text. Long labels shrink to fit.",
                    new AcceptableValueRange<float>(12f, 64f),
                    ChestWindow, "ModMenu.Label=Name size"));

            OrnamentOffsetY = Config.Bind(
                "Display", "OrnamentOffsetY", 36f,
                new ConfigDescription(
                    "How far to raise the panel's decorative flourish to make room for the " +
                    "title beneath it. 0 leaves it where the game put it.",
                    new AcceptableValueRange<float>(-100f, 100f),
                    ChestWindow, "ModMenu.Label=Flourish height"));

            TitleOffsetY = Config.Bind(
                "Display", "TitleOffsetY", -3f,
                new ConfigDescription(
                    "Vertical position of the title inside the header band, between the raised " +
                    "flourish and the divider line. Negative is lower.",
                    new AcceptableValueRange<float>(-100f, 100f),
                    ChestWindow, "ModMenu.Label=Name position"));

            PanelTopPadding = Config.Bind(
                "Display", "PanelTopPadding", 56f,
                new ConfigDescription(
                    "Extra room added above the item grid for the title, in UI units. The window " +
                    "grows by the same amount so the grid keeps its size. 0 disables.",
                    new AcceptableValueRange<float>(0f, 200f),
                    ChestWindow, "ModMenu.Label=Room above the grid"));

            HeaderDropY = Config.Bind(
                "Display", "HeaderDropY", 34f,
                new ConfigDescription(
                    "How far to push the whole title band down from the top of the window. This " +
                    "is what creates visible room above the decoration.",
                    new AcceptableValueRange<float>(-100f, 100f),
                    ChestWindow, "ModMenu.Label=Title band position"));

            LineOffsetY = Config.Bind(
                "Display", "LineOffsetY", -14f,
                new ConfigDescription(
                    "How far to push the header band's divider line down, opening space between " +
                    "the title and the line. Negative is lower.",
                    new AcceptableValueRange<float>(-100f, 100f),
                    ChestWindow, "ModMenu.Label=Divider position"));

            ShowEditButton = Config.Bind(
                "Editing", "ShowEditButton", true,
                new ConfigDescription(
                    "Show a pencil button beside the chest title. Click it to rename the chest " +
                    "in place: Enter saves, Escape cancels.",
                    null,
                    Renaming, "ModMenu.Label=Show the pencil button"));

            ShowHoverLabel = Config.Bind(
                "Hover", "ShowHoverLabel", true,
                new ConfigDescription(
                    "Show a chest's label in the world when the mouse is over it.",
                    null,
                    HoverLabelSection, "ModMenu.Label=Show names in the world"));

            UseGameInteractionLabel = Config.Bind(
                "Hover", "UseGameInteractionLabel", false,
                new ConfigDescription(
                    "Show a named chest's name in the game's own interaction banner - the plate " +
                    "that names characters and objects you point at. Matches the game exactly and " +
                    "appears at the same moment as the interaction arrow.",
                    null,
                    HoverLabelSection, "ModMenu.Label=Use the interaction banner"));

            UseGameNameplate = Config.Bind(
                "Hover", "UseGameNameplate", true,
                new ConfigDescription(
                    "Show the hover label using the game's own nameplate banner - the same one " +
                    "it uses for character names - so it matches exactly. Set false to use the " +
                    "mod's own plate instead.",
                    null,
                    HoverLabelSection, "ModMenu.Label=Use the game's nameplate"));

            // Deliberately no ModMenu.Values: tagging values turns the field into a fixed cycle
            // and removes text entry, so any hex outside the list would become unreachable.
            NameplateTint = Config.Bind(
                "Hover", "NameplateTint", "#4A2E8F",
                new ConfigDescription(
                    "Colour for the speech bubble behind a chest name, as a hex value. Leave " +
                    "blank to keep the game's own colour. Only applies while the mod's label is " +
                    "showing; the game's own nameplates are restored afterwards.",
                    null,
                    HoverLabelSection, "ModMenu.Label=Nameplate colour (hex)"));

            HideArrowWhenNamed = Config.Bind(
                "Hover", "HideArrowWhenNamed", true,
                new ConfigDescription(
                    "Hide the game's interaction arrow while a named chest's label is showing. " +
                    "The label already marks the chest, and the arrow sits in the same place.",
                    null,
                    HoverLabelSection, "ModMenu.Label=Hide the interaction arrow"));

            HoverHeight = Config.Bind(
                "Hover", "HoverHeight", 0.8f,
                new ConfigDescription(
                    "How far above the chest the hover label floats, in world units. Kept low so " +
                    "it clears the game's own interaction chevron, which sits above the chest.",
                    new AcceptableValueRange<float>(0f, 3f),
                    HoverLabelSection, "ModMenu.Label=Label height"));

            HoverFontSize = Config.Bind(
                "Hover", "HoverFontSize", 22f,
                new ConfigDescription(
                    "Maximum font size of the hover label.",
                    new AcceptableValueRange<float>(10f, 48f),
                    HoverLabelSection, "ModMenu.Label=Label size"));

            HoverBackgroundAlpha = Config.Bind(
                "Hover", "HoverBackgroundAlpha", 0.3f,
                new ConfigDescription(
                    "Opacity of the plate behind the hover label, 0-1. 0 means no background at " +
                    "all, letting the game's interaction chevron show through.",
                    new AcceptableValueRange<float>(0f, 1f),
                    HoverLabelSection, "ModMenu.Label=Background opacity"));

            ScreenReaderName = Config.Bind(
                "Accessibility", "ScreenReaderName", ScreenReaderNameMode.TypeAndLabel,
                new ConfigDescription(
                    "How a named chest's label is read out by screen readers such as " +
                    "MoonlightAccess. It works by filling the game's own (normally empty) name " +
                    "field for the chest, which the screen reader already reads. " +
                    "\"Type and label\" says e.g. \"Storage Crate named Pantry\"; \"Label only\" " +
                    "says just \"Pantry\"; \"Off\" leaves the field untouched. The label is only " +
                    "supplied when read - it is never written into your save.",
                    null,
                    ScreenReaderSection, "ModMenu.Label=Screen reader name"));

            LogUiDiagnostics = Config.Bind(
                "Diagnostics", "LogUiDiagnostics", false,
                new ConfigDescription(
                    "Dump the chest screen's layout to the log each time the header is drawn: " +
                    "canvas, font, colour, position and child list. Use this to work out why a " +
                    "header is not showing, then turn it off.",
                    null,
                    Diagnostics, "ModMenu.Label=Log the chest screen layout"));

            var storeRoot = Path.Combine(Paths.ConfigPath, "ChestLabels");
            Store = new LabelStore(storeRoot);

            harmony.PatchAll(typeof(ChestPatches));

            gameObject.AddComponent<HoverLabel>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Labels: {storeRoot}");
            Log.LogInfo($"Press {ReloadKey.Value} to reload labels from disk.");
        }

        private void Update()
        {
            // Escape cancels a rename rather than falling through to the game, which would
            // close the whole chest screen mid-edit.
            if (TitleEditor.IsEditing && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                TitleEditor.Cancel();
                return;
            }

            if (ReloadKey.Value.IsDown())
            {
                ReloadFromDisk();
            }
        }

        /// <summary>
        /// Point the store at the active save, lazily.
        ///
        /// Lives here rather than in the chest patches because the hover label needs labels
        /// too, and it runs long before any chest is opened. Hanging this off the chest patch
        /// meant a chest had to be opened once before its hover label would ever appear.
        /// </summary>
        internal static bool EnsureStoreLoaded()
        {
            var persistence = GamePersistence.Instance;
            if (persistence == null || Store == null)
            {
                return false;
            }

            var saveGuid = persistence.Guid.ToString();
            if (string.IsNullOrWhiteSpace(saveGuid))
            {
                return false;
            }

            var isFirstLoad = Store.SaveGuid == null;

            // Short-circuits when the GUID is unchanged, so this is cheap to call often.
            Store.LoadForSave(saveGuid);

            if (isFirstLoad)
            {
                Log.LogInfo($"Save {saveGuid}: loaded {Store.Count} label(s) from {Store.CurrentPath}");

                if (Store.LastLoadRecoveredFromCorruption)
                {
                    Log.LogWarning(
                        "The labels file could not be parsed and was moved aside with a " +
                        "'.corrupt-*' suffix. Starting with an empty set.");
                }
            }

            return true;
        }

        private void OnDestroy()
        {
            try
            {
                Store?.Save();
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to save labels on shutdown: {e}");
            }
        }

        private void ReloadFromDisk()
        {
            var saveGuid = Store?.SaveGuid;
            if (saveGuid == null)
            {
                Log.LogWarning("No save loaded yet - open a chest first so the save is known.");
                return;
            }

            try
            {
                // LoadForSave short-circuits when the GUID is unchanged, so drop the current
                // save first to force a genuine re-read.
                Store.Save();
                var reloader = new LabelStore(Path.Combine(Paths.ConfigPath, "ChestLabels"));
                reloader.LoadForSave(saveGuid);
                Store = reloader;
                Log.LogInfo($"Reloaded {Store.Count} label(s) from {Store.CurrentPath}");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to reload labels: {e}");
            }
        }
    }
}






















