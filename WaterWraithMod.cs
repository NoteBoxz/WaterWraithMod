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
using WaterWraithMod.Scripts;

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
        internal static EnemyType WraithEnemyType { get; private set; } = null!;

        public static ConfigEntry<float> SpawnChanceConfig = null!;
        public static ConfigEntry<float> SpawnTimerConfig = null!;
        public static ConfigEntry<bool> ChaseEnemyConfig = null!;
        public static ConfigEntry<int> DamageConfig = null!;
        public static ConfigEntry<int> EDamageConfig = null!;
        public static ConfigEntry<string> PerMoonSpawnChanceConfig = null!;
        public static ConfigEntry<float> PlayerCollisionBufferMultiplier = null!;
        public static ConfigEntry<WraithSpawnPosition> WraithSpawnPositionConfig = null!;
        public static ConfigEntry<float> WaterWaithSpawnPositionChance = null!;
        public static ConfigEntry<int> PlayerDetectionRange = null!;
        public static ConfigEntry<float> EnemyDetectionRange = null!;
        public static ConfigEntry<float> PlayerChaseExitDistanceThreshold = null!;
        public static ConfigEntry<float> EnemyChaseExitDistanceThreshold = null!;
        public static ConfigEntry<float> EnemyChaseTimer = null!;
        public static ConfigEntry<float> PlayerOutOfLOSTimer = null!;
        public static ConfigEntry<float> RecoveryTime = null!;
        public static ConfigEntry<bool> KnowWherePlayerIsWhenOutOfLOS = null!;
        public static ConfigEntry<GameGeneration> GameGenerationConfig = null!;
        public static Dictionary<string, float> GetParsedMoonSpawn()
        {
            Dictionary<string, float> dict = new Dictionary<string, float>();
            if (string.IsNullOrEmpty(PerMoonSpawnChanceConfig.Value))
            {
                return new Dictionary<string, float>();
            }
            foreach (var item in PerMoonSpawnChanceConfig.Value.Split(',').ToList())
            {
                if (string.IsNullOrEmpty(item))
                {
                    Logger.LogWarning($"Empty item found in PerMoonSpawnChanceConfig." +
                    " please do not have empty items in your config.");
                    continue;
                }
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

            if (IsDependencyLoaded("NoteBoxz.LethalMin"))
            {
                LETHALMIN_AddDef();
            }

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        }

        public void BindConfigs()
        {
            GameGenerationConfig = Config.Bind(
                "Water Wraith",
                "Game Generation",
                GameGeneration.Pikmin4,
                "The game generation of the water wraith."
            );

            RecoveryTime = Config.Bind(
                "Water Wraith",
                "Recovery Time",
                10f,
                "The time it takes for the water wraith to recover after being scared"
            );

            SpawnChanceConfig = Config.Bind(
                "Spawning",
                "Spawn Chance",
                25f,
                "The chance for a Water Wraith to spawn. (0-100)"
            );

            PerMoonSpawnChanceConfig = Config.Bind(
                "Spawning",
                "Per-Moon Spawn Chance",
                "Experimentation:10,Assurance:15,Vow:20,March:30,Adamance:35,Rend:45,Dine:50,Titan:50,Embrion:70,Artifice:70,Zeranos:0,Gordion:0",
                "The chance for a Water Wraith to spawn per moon these values will override" +
                " the base spawn chance when spawning on a moon in the list." +
                " (Formatted just like Lethal Level Loader's configs)" +
                " (sperate with commas no spaces inbetween sperate moonnames and spawn chances with colons)" +
                " (moonname1:25,moonname2:35,moonname3:55...) (0-100)"
            );

            ChaseEnemyConfig = Config.Bind(
                "Enemy Interaction",
                "Chase Enemys",
                true,
                "Makes the water wraith chase enemies as well as players.");

            WraithSpawnPositionConfig = Config.Bind(
                "Spawning",
                "Wraith Spawn Position",
                WraithSpawnPosition.OnlyIndoors,
                "The position where the Water Wraith will spawn."
            );

            WaterWaithSpawnPositionChance = Config.Bind(
                "Spawning",
                "Spawn Position Chance",
                50f,
                "The chance for the Water Wraith to spawn outdoors or indoors if IndoorsAndOutdoors config is chosen, [0 = indoors] [100 = outdoors]. (0-100)"
            );

            SpawnTimerConfig = Config.Bind(
                "Spawning",
                "Spawn Timer (Seconds)",
                300f,
                "The amount of time it takes for the wraith to spawn."
            );

            DamageConfig = Config.Bind(
                "Player Interaction",
                "Damage",
                50,
                "The ammount of damage the water wraith deals.(0-100)"
            );

            PlayerCollisionBufferMultiplier = Config.Bind(
                "Player Interaction",
                "Collision Buffer",
                1.4f,
                "Multiplies the timer that is used to buffer damage players, higher values = instant death"
            );

            EDamageConfig = Config.Bind(
                "Enemy Interaction",
                "Enemy Damage",
                1,
                "The ammount of damage the water wraith deals to Enemies. Recomended: (1-3)"
            );

            PlayerDetectionRange = Config.Bind(
                "Player Interaction",
                "Player Detection Range",
                15,
                "The maximum range at which the Water Wraith can detect players. (in meters)"
            );

            EnemyDetectionRange = Config.Bind(
                "Enemy Interaction",
                "Enemy Detection Range",
                15f,
                "The maximum range at which the Water Wraith can detect other enemies. (in meters)"
            );

            PlayerChaseExitDistanceThreshold = Config.Bind(
                "Player Interaction",
                "Player Chase Exit Distance",
                30f,
                "The distance a player needs to be from the Water Wraith to exit chase state. (in meters)"
            );

            EnemyChaseExitDistanceThreshold = Config.Bind(
                "Enemy Interaction",
                "Enemy Chase Exit Distance",
                20f,
                "The distance an enemy needs to be from the Water Wraith to exit chase state. (in meters)"
            );

            EnemyChaseTimer = Config.Bind(
                "Enemy Interaction",
                "Enemy Chase Timer",
                25f,
                "The maximum time the Water Wraith will chase an enemy before losing interest. (in seconds)"
            );

            PlayerOutOfLOSTimer = Config.Bind(
                "Player Interaction",
                "Player Out of LOS Timer",
                10f,
                "How long the Water Wraith will continue chasing a player after losing line of sight. (in seconds)"
            );

            KnowWherePlayerIsWhenOutOfLOS = Config.Bind(
                "Player Interaction",
                "Know Where Player Is When Out of LOS",
                true,
                "If the Water Wraith will know where the player is when they are out of line of sight. " +
                 "This will make it so players will need to be out of it's LOS for the timer's duration. " +
                 "Making it harder for players to lose the Water Wraith by going out of it's LOS."
            );

            GameGenerationConfig.SettingChanged += (sender, e) => { UpdateAllOverrides(); };
        }

        public void UpdateAllOverrides()
        {
            try
            {
                foreach (WaterWraithMesh mesh in GameObject.FindObjectsOfType<WaterWraithMesh>())
                {
                    mesh.SetOverride((int)GameGenerationConfig.Value);
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
            WraithEnemyType = assetBundle.LoadAsset<EnemyType>("Assets/ModAsset/WaterType.asset");
            TerminalNode WraithNode = assetBundle.LoadAsset<TerminalNode>("Assets/ModAsset/WaterNode.asset");
            LethalLib.Modules.Enemies.RegisterEnemy(WraithEnemyType, 0, LethalLib.Modules.Levels.LevelTypes.All, WraithNode, null!);
        }

        internal static void LETHALMIN_AddDef()
        {
            LethalMin.CustomPikminEnemyOverrideDef def = WraithEnemyType.enemyPrefab.AddComponent<LethalMin.CustomPikminEnemyOverrideDef>();
            def.CustomPikminEnemyOverrideType = typeof(WaterWraithPikminEnemy);
        }

        public static bool IsDependencyLoaded(string pluginGUID)
        {
            return Chainloader.PluginInfos.ContainsKey(pluginGUID);
        }


        private void NetcodePatcher()
        {
            Type[] types = GetTypesWithErrorHandling();
            foreach (var type in types)
            {
                try
                {
                    var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        try
                        {
                            var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                            if (attributes.Length > 0)
                            {
                                method.Invoke(null, null);
                            }
                        }
                        catch (Exception methodException)
                        {
                            Logger.LogWarning($"Error invoking method {method.Name} in type {type.FullName}: {methodException.Message}");
                            if (methodException.InnerException != null)
                            {
                                Logger.LogWarning($"Inner exception: {methodException.InnerException.Message}");
                            }
                        }
                    }
                }
                catch (Exception typeException)
                {
                    Logger.LogWarning($"Error processing type {type.FullName}: {typeException.Message}");
                }
            }

            Logger.LogInfo("NetcodePatcher completed.");
        }
        public static List<Type> LibraryTypes = new List<Type>();
        internal static void Patch()
        {
            Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

            Logger.LogDebug("Patching...");

            try
            {
                // Get all types from the executing assembly
                Type[] types = GetTypesWithErrorHandling();

                // Patch everything except FilterEnemyTypesPatch
                foreach (var type in types)
                {
                    try
                    {
                        Harmony.PatchAll(type);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Error patching type {type.FullName}: {e.Message}");
                        if (e.InnerException != null)
                        {
                            Logger.LogError($"Inner exception: {e.InnerException.Message}");
                        }
                    }
                }

                // Only patch FilterEnemyTypesPatch if LethalMon is loaded
                if (IsDependencyLoaded("NoteBoxz.LethalMin"))  // Replace with actual LethalMon GUID
                {
                    Logger.LogInfo("LethalMin detected. Patching Purple Pikmin.");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error during patching process: {e.Message}");
                if (e.InnerException != null)
                {
                    Logger.LogError($"Inner exception: {e.InnerException.Message}");
                }
            }

            Logger.LogDebug("Finished patching!");
        }

        private static Type[] GetTypesWithErrorHandling()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Logger.LogWarning("ReflectionTypeLoadException caught while getting types. Some types will be skipped.");
                foreach (var loaderException in e.LoaderExceptions)
                {
                    Logger.LogWarning($"Loader Exception: {loaderException.Message}");
                    if (loaderException is FileNotFoundException fileNotFound)
                    {
                        Logger.LogWarning($"Could not load file: {fileNotFound.FileName}");
                    }
                }
                return e.Types.Where(t => t != null).ToArray();
            }
            catch (Exception e)
            {
                Logger.LogError($"Unexpected error while getting types: {e.Message}");
                return new Type[0];
            }
        }

        internal static void Unpatch()
        {
            Logger.LogDebug("Unpatching...");

            Harmony?.UnpatchSelf();

            Logger.LogDebug("Finished unpatching!");
        }
    }
}
