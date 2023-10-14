using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TootTally.Spectating;
using TootTally.Utils;
using TootTally.Utils.TootTallySettings;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace TootTally.TournamentHost
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTally", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "TournamentHost.cfg";
        public Options option;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }
        public string Name { get => PluginInfo.PLUGIN_NAME; set => Name = value; }

        public ManualLogSource GetLogger => Logger;
        public static TootTallySettingPage settingPage;

        public void LogInfo(string msg) => Logger.LogInfo(msg);
        public void LogError(string msg) => Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTally.Plugin.Instance.Config.Bind("Modules", "TournamentHost", true, "Tournament Host Client for TootTally");
            TootTally.Plugin.AddModule(this);
        }

        public void LoadModule()
        {
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true);
            option = new Options()
            {
                HorizontalScreenCount = config.Bind("Global", "HorizontalScreenCount", 4f, "Amount of screen displayed horizontally"),
                VerticalScreenCount = config.Bind("Global", "VerticalScreenCount", 2f, "Amount of screen displayed vertically"),
                UserIDs = config.Bind("Global", "UserIDs", "0,0,0;0,0,0", "List of user IDs to spectate")
            };

            settingPage = TootTallySettingsManager.AddNewPage("TournamentHost", "Tournament Host", 40f, new Color(0, 0, 0, 0));
            settingPage?.AddSlider("Hori Screens", 1, 10, option.HorizontalScreenCount, true);
            settingPage?.AddSlider("Vert Screens", 1, 10, option.VerticalScreenCount, true);
            settingPage?.AddLabel("UserIDs");
            settingPage?.AddTextField("UserIDs", option.UserIDs.Value, false, text => option.UserIDs.Value = text);

            Harmony.CreateAndPatchAll(typeof(TournamentHostPatches), PluginInfo.PLUGIN_GUID);
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            Harmony.UnpatchID(PluginInfo.PLUGIN_GUID);
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public static class TournamentHostPatches
        {

            private static Vector2 _screenSize;
            private static List<TournamentGameplayController> _tournamentControllerList = new List<TournamentGameplayController>();
            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OnGameControllerStart(GameController __instance)
            {
                _tournamentControllerList?.Clear();
                _screenSize = new Vector2(Screen.width, Screen.height);
                float horizontalScreenCount = (int)Instance.option.HorizontalScreenCount.Value;
                float horizontalRatio = _screenSize.x / horizontalScreenCount;
                float verticalScreenCount = (int)Instance.option.VerticalScreenCount.Value;
                float verticalRatio = _screenSize.y / verticalScreenCount;
                var gameplayCanvas = GameObject.Find("GameplayCanvas");
                gameplayCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var sizeDelta = gameplayCanvas.GetComponent<RectTransform>().sizeDelta;
                gameplayCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(sizeDelta.x / horizontalScreenCount, sizeDelta.y);
                gameplayCanvas.GetComponent<RectTransform>().pivot = new Vector2(.5f * (horizontalScreenCount - (horizontalScreenCount - verticalScreenCount)), .5f);
                var botLeftCam = GameObject.Find("GameplayCam").GetComponent<Camera>();

                var canvasObject = new GameObject($"TournamentGameplayCanvas");
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.scaleFactor = 2.4f / verticalScreenCount;
                canvas.scaleFactor = 2.4f / verticalScreenCount;

                var gridLayout = canvasObject.AddComponent<GridLayoutGroup>();
                gridLayout.cellSize = new Vector2(horizontalRatio / canvas.scaleFactor, verticalRatio / canvas.scaleFactor);

                //var botRightCam = GameObject.Instantiate(botLeftCam);
                //var topLeftCam = GameObject.Instantiate(botLeftCam);
                //var topRightCam = GameObject.Instantiate(botLeftCam);
                //botRightCam.pixelRect = new Rect(_screenSize.x / screenRatio, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                //botLeftCam.pixelRect = new Rect(0, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                //topLeftCam.pixelRect = new Rect(0, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                //topRightCam.pixelRect = new Rect(_screenSize.x / screenRatio, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio);

                var IDs = Instance.option.UserIDs.Value.Split(';');
                string[][] idList = new string[IDs.Length][];
                for (int i = 0; i < IDs.Length; i++)
                    idList[i] = IDs[i].Split(',');
                /*
                / grist:1
                / dom: 62
                / Samuran: 7
                / Danew: 106
                / Silver : 98
                / Beta : 11
                / Guardie : 114
                / Dew : 374
                / Static : 242
                / PX : 372
                */
                Plugin.Instance.LogInfo("X: " + horizontalScreenCount + " Y: " + verticalScreenCount);
                for (int y = 0; y < verticalScreenCount; y++)
                {
                    for (int x = 0; x < horizontalScreenCount; x++)
                    {
                        if (int.TryParse(idList[y][x], out int id))
                        {
                            var tc = gameplayCanvas.AddComponent<TournamentGameplayController>();
                            tc.Initialize(__instance, GameObject.Instantiate(botLeftCam), new Rect(x * horizontalRatio, y * verticalRatio, horizontalRatio, verticalRatio), canvasObject.transform, new SpectatingSystem(id, idList[y][x].ToString()));
                            _tournamentControllerList.Add(tc);
                        }
                    }
                }

                botLeftCam.enabled = false;
                __instance.pointer.SetActive(false);
                __instance.ui_score_shadow.gameObject.SetActive(false);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.getScoreAverage))]
            [HarmonyPrefix]
            public static bool OnGetScoreAveragePrefix()
            {
                _tournamentControllerList.ForEach(tc => tc.OnGetScoreAverage());
                return false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.tallyScore))]
            [HarmonyPrefix]
            public static bool OnTallyScorePrefix()
            {
                _tournamentControllerList.ForEach(tc => tc.OnTallyScore());
                return false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.fixAudioMixerStuff))]
            [HarmonyPrefix]
            public static void CopyAllAudioClips()
            {
                _tournamentControllerList.ForEach(tc => tc.CopyAllAudioClips());
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Start))]
            [HarmonyPostfix]
            public static void DisconnectAllClients()
            {
                _tournamentControllerList.ForEach(tc => tc.Disconnect());
            }

            private static bool _waitingToSync;

            [HarmonyPatch(typeof(GameController), nameof(GameController.playsong))]
            [HarmonyPrefix]
            public static bool OverwriteStartSongIfSyncRequired(GameController __instance)
            {
                if (ShouldWaitForSync(out _waitingToSync))
                    PopUpNotifManager.DisplayNotif("Waiting to sync with host...");

                return !_waitingToSync;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPostfix]
            public static void OnUpdatePlaybackSpectatingData(GameController __instance)
            {
                if (_waitingToSync && __instance.curtainc.doneanimating && !ShouldWaitForSync(out _waitingToSync))
                    __instance.startSong(false);
            }

            private static bool ShouldWaitForSync(out bool waitForSync)
            {
                waitForSync = true;
                if (!_tournamentControllerList.Any(x => !x.IsReady))
                    waitForSync = false;
                if (Input.GetKey(KeyCode.Space))
                    waitForSync = false;
                return waitForSync;
            }
        }

        public class Options
        {
            // Fill this class up with ConfigEntry objects that define your configs
            // Example:
            // public ConfigEntry<bool> Unlimited { get; set; }
            public ConfigEntry<float> HorizontalScreenCount { get; set; }
            public ConfigEntry<float> VerticalScreenCount { get; set; }
            public ConfigEntry<string> UserIDs { get; set; }
        }
    }
}