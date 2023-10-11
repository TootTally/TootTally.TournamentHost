using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
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
            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OnGameControllerStart(GameController __instance)
            {
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
                botRightCam.pixelRect = new Rect(_screenSize.x / screenRatio, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                botLeftCam.pixelRect = new Rect(0, 0, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                topLeftCam.pixelRect = new Rect(0, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
                topRightCam.pixelRect = new Rect(_screenSize.x / screenRatio, _screenSize.y / screenRatio, _screenSize.x / screenRatio, _screenSize.y / screenRatio);
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