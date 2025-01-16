using System.IO;
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using WaterWraithMod.Patches;
using BepInEx.Bootstrap;
using System.Collections.Generic;
using System.Linq;

namespace WaterWraithMod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("evaisa.lethallib")]
    [BepInDependency("NoteBoxz.LethalMin", BepInDependency.DependencyFlags.SoftDependency)]
    public class WaterWraithMod : BaseUnityPlugin
    {
        public static WaterWraithMod Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        internal static Harmony? Harmony { get; set; }
        internal static AssetBundle assetBundle { get; private set; } = null!;

        public static ConfigEntry<float> SpawnChanceConfig = null!;
        public static ConfigEntry<float> SpawnTimerConfig = null!;
        public static ConfigEntry<bool> ChaseEnemyConfig = null!;
        public static ConfigEntry<int> DamageConfig = null!;
        public static ConfigEntry<int> EDamageConfig = null!;
        public static ConfigEntry<string> PerMoonSpawnChanceConfig = null!;
        public static ConfigEntry<WraithSpawnPosition> WraithSpawnPositionConfig = null!;
        public static ConfigEntry<GameStle> gameStleConfig = null!;
        public static Dictionary<string, float> GetParsedMoonSpawn()
        {
            Dictionary<string, float> dict = new Dictionary<string, float>();
            if (string.IsNullOrEmpty(PerMoonSpawnChanceConfig.Value))
            {
                return new Dictionary<string, float>();
            }
            foreach (var item in PerMoonSpawnChanceConfig.Value.Split(',').ToList())
            {
                string[] parts = item.Split(':');
                float f;
                if (float.TryParse(parts[1], out f))
                {
                    dict.Add(parts[0], f);
                }
                else
                {
                    Logger.LogWarning($"Failed to parse ({item})!");
                }
            }
            return dict;
        }


        private void Awake()
        {
            Logger = base.Logger;
            Instance = this;

            NetcodePatcher();
            Patch();

            BindConfigs();

            LoadAssetBundle();

            RegisterRockAssets();

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        }

        public void BindConfigs()
        {
            SpawnChanceConfig = Config.Bind(
                "WaterWraith",
                "Spawn Chance",
                25f,
                "The chance for a Water Wraith to spawn. (0-100)"
            );

            PerMoonSpawnChanceConfig = Config.Bind(
                "WaterWraith",
                "Per-Moon Spawn Chance",
                "7510 Zeranos:0,61 March:35",
                "The chance for a Water Wraith to spawn per moon these values will override"+
                " the base spawn chance when spawning on a moon in the list."+
                " (Formatted just like Lethal Level Loader's configs)"+
                " (sperate with commas no spaces inbetween sperate moonnames and spawn chances with colons)"+
                " (moonname1:25,moonname2:35,moonname3:55...) (0-100)"
            );

            ChaseEnemyConfig = Config.Bind(
                "WaterWraith",
                "Chase Enemys",
                true,
                "Makes the water wraith chase enemies as well as players.");

            WraithSpawnPositionConfig = Config.Bind(
                "WaterWraith",
                "Wraith Spawn Position",
                WraithSpawnPosition.OnlyIndoors,
                "The position where the Water Wraith will spawn."
            );

            SpawnTimerConfig = Config.Bind(
                "WaterWraith",
                "Spawn Timer (Seconds)",
                180f,
                "The amount of time it takes for the wraith to spawn."
            );

            DamageConfig = Config.Bind(
                "WaterWraith",
                "Damage",
                50,
                "The ammount of damage the water wraith deals.(0-100)"
            );

            EDamageConfig = Config.Bind(
                "WaterWraith",
                "Enemy Damage",
                1,
                "The ammount of damage the water wraith deals to Enemies. Recomended: (0-2)"
            );

            gameStleConfig = Config.Bind(
                "WaterWraith",
                "Game Stle",
                GameStle.Pikmin4,
                "The game stle of the water wraith."
            );

            gameStleConfig.SettingChanged += (sender, e) => { UpdateAllOverrides(); };
        }

        public void UpdateAllOverrides()
        {
            try
            {
                foreach (WaterWraithMesh mesh in GameObject.FindObjectsOfType<WaterWraithMesh>())
                {
                    mesh.SetOverride((int)gameStleConfig.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating overrides: {ex.Message}");
            }
        }

        internal static void LoadAssetBundle()
        {
            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyLocation == null)
            {
                throw new InvalidOperationException("Unable to determine assembly location.");
            }

            string bundlePath = Path.Combine(assemblyLocation, "wraithassets");
            assetBundle = AssetBundle.LoadFromFile(bundlePath);

            if (assetBundle == null)
            {
                throw new InvalidOperationException("Failed to load AssetBundle.");
            }
        }

        internal static void RegisterRockAssets()
        {
            EnemyType WraithEnemy = assetBundle.LoadAsset<EnemyType>("Assets/ModAsset/WaterType.asset");
            TerminalNode WraithNode = assetBundle.LoadAsset<TerminalNode>("Assets/ModAsset/WaterNode.asset");
            LethalLib.Modules.Enemies.RegisterEnemy(WraithEnemy, 0, LethalLib.Modules.Levels.LevelTypes.All, WraithNode, null!);
        }

        public static bool IsDependencyLoaded(string pluginGUID)
        {
            return Chainloader.PluginInfos.ContainsKey(pluginGUID);
        }

        internal static void Patch()
        {
            Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

            Logger.LogDebug("Patching...");

            Harmony.PatchAll();

            Logger.LogDebug("Finished patching!");
        }

        internal static void Unpatch()
        {
            Logger.LogDebug("Unpatching...");

            Harmony?.UnpatchSelf();

            Logger.LogDebug("Finished unpatching!");
        }

        private void NetcodePatcher()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }
    }
}
