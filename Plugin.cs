using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using TootTally.Spectating;
using TootTally.Utils;
using TootTally.Utils.TootTallySettings;
using UnityEngine;

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
            private static int _numberOfScreens;
            private static List<TournamentGameplayController> _tournamentControllerList = new List<TournamentGameplayController>();
            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OnGameControllerStart(GameController __instance)
            {
                _tournamentControllerList?.Clear();
                _screenSize = new Vector2(Screen.width, Screen.height);
                _numberOfScreens = 4;
                var screenRatio = _numberOfScreens / 2f;
                var gameplayCanvas = GameObject.Find("GameplayCanvas");
                gameplayCanvas.GetComponent<Canvas>().scaleFactor = screenRatio;
                gameplayCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

                var botLeftCam = GameObject.Find("GameplayCam").GetComponent<Camera>();
                var botRightCam = GameObject.Instantiate(botLeftCam);
                var topLeftCam = GameObject.Instantiate(botLeftCam);
                var topRightCam = GameObject.Instantiate(botLeftCam);
                //botRightCam.pixelRect = new Rect(_screenSize.x / screenRatio, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                botLeftCam.pixelRect = new Rect(0, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                //topLeftCam.pixelRect = new Rect(0, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                //topRightCam.pixelRect = new Rect(_screenSize.x / screenRatio, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio);

                var tc1 = gameplayCanvas.AddComponent<TournamentGameplayController>();
                tc1.Initialize(__instance, botRightCam, new Rect(_screenSize.x / screenRatio, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio), new SpectatingSystem(11, "Megalovania"));
                _tournamentControllerList.Add(tc1);

                var tc2 = gameplayCanvas.AddComponent<TournamentGameplayController>();
                tc2.Initialize(__instance, topLeftCam, new Rect(0, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio), new SpectatingSystem(1385, "Benny"));
                _tournamentControllerList.Add(tc2);

                var tc3 = gameplayCanvas.AddComponent<TournamentGameplayController>();
                tc3.Initialize(__instance, topRightCam, new Rect(_screenSize.x / screenRatio, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio), new SpectatingSystem(2369, "koozie"));
                _tournamentControllerList.Add(tc3);

                __instance.pointer.SetActive(false);
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.getScoreAverage))]
            [HarmonyPrefix]
            public static bool OnGetScoreAveragePostfix()
            {
                _tournamentControllerList.ForEach(tc => tc.OnGetScoreAverage());
                return false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.fixAudioMixerStuff))]
            [HarmonyPrefix]
            public static void CopyAllAudioClips()
            {
                _tournamentControllerList.ForEach(tc => tc.CopyAllAudioClips());
            }
        }

        public class Options
        {
            // Fill this class up with ConfigEntry objects that define your configs
            // Example:
            // public ConfigEntry<bool> Unlimited { get; set; }
        }
    }
}