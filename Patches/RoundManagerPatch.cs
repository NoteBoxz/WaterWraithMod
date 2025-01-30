using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterWraithMod.Patches
{
    public enum WraithSpawnPosition
    {
        OnlyIndoors,
        OnlyOutdoors,
        IndoorsAndOutdoors
    }
    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch
    {
        public static bool IsSpawningWaterWraithThisRound;
        public static float WWTimer;
        [HarmonyPatch("PredictAllOutsideEnemies")]
        [HarmonyPostfix]
        public static void PredictAllOutsideEnemiesPostFix(RoundManager __instance)
        {
            if (!__instance.IsServer || RoundManager.Instance.currentLevel.sceneName == "CompanyBuilding")
            {
                return;
            }

            IsSpawningWaterWraithThisRound = false;

            float RNG = UnityEngine.Random.Range(0.0f, 100f);
            float AIvalue = WaterWraithMod.SpawnChanceConfig.Value;
            // WaterWraithMod.Logger.LogInfo($"LVNAME: {__instance.currentLevel.name}, PName: {__instance.currentLevel.PlanetName}, UH: {__instance.currentLevel.sceneName}");
            // foreach (var item in WaterWraithMod.GetParsedMoonSpawn())
            // {
            //     WaterWraithMod.Logger.LogInfo(item);
            // }
            if (WaterWraithMod.GetParsedMoonSpawn().ContainsKey(__instance.currentLevel.PlanetName))
            {
                WaterWraithMod.GetParsedMoonSpawn().TryGetValue(__instance.currentLevel.PlanetName, out AIvalue);
                WaterWraithMod.Logger.LogInfo($"override value: {AIvalue}");
            }
            WaterWraithMod.Logger.LogInfo($"RNG: {RNG}, CHANCE: {AIvalue}");
            if (RNG < AIvalue)
            {
                WWTimer = WaterWraithMod.SpawnTimerConfig.Value;
                IsSpawningWaterWraithThisRound = true;
            }
        }

        // public static void SpawnThingy()
        // {
        //     RoundManager __instance = RoundManager.Instance;
        //     EnemyType WraithEnemy = WaterWraithMod.assetBundle.LoadAsset<EnemyType>("Assets/ModAsset/WaterType.asset");
        //     int SpawnableIndex = -1;

        //     for (int i = 0; i < __instance.currentLevel.Enemies.Count; i++)
        //     {
        //         EnemyType t = __instance.currentLevel.Enemies[i].enemyType;
        //         if (t == WraithEnemy)
        //         {
        //             SpawnableIndex = i;
        //             WaterWraithMod.Logger.LogInfo($"Found a WaterWraith at index {i} in the enemies list!");
        //             break;
        //         }
        //     }

        //     if (SpawnableIndex == -1)
        //     {
        //         WaterWraithMod.Logger.LogError("No WaterWraith found in the enemies list!");
        //         return;
        //     }

        //     // Get the local player's transform
        //     Transform playerTransform = StartOfRound.Instance.localPlayerController.transform;

        //     // Calculate a position in front of the player
        //     Vector3 spawnPosition = playerTransform.position + playerTransform.forward * 3f; // 3 units in front of the player

        //     // Spawn the Water Wraith at the calculated position
        //     __instance.SpawnEnemyOnServer(
        //         spawnPosition,
        //         playerTransform.rotation.eulerAngles.y,
        //         SpawnableIndex
        //     );
        // }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(RoundManager __instance)
        {
            if (!__instance.IsServer || StartOfRound.Instance.inShipPhase || StartOfRound.Instance.livingPlayers == 0)
            {
                return;
            }

            if (IsSpawningWaterWraithThisRound)
            {
                if (WWTimer > 0)
                {
                    WWTimer -= Time.deltaTime;
                }
                else
                {
                    IsSpawningWaterWraithThisRound = false;
                    GameObject[] spawnPointsIn = GameObject.FindGameObjectsWithTag("AINode");
                    GameObject[] spawnPointsOut = GameObject.FindGameObjectsWithTag("OutsideAINode");
                    EnemyType WraithEnemy = WaterWraithMod.assetBundle.LoadAsset<EnemyType>("Assets/ModAsset/WaterType.asset");
                    int SpawnableIndex = -1;

                    for (int i = 0; i < __instance.currentLevel.Enemies.Count; i++)
                    {
                        EnemyType t = __instance.currentLevel.Enemies[i].enemyType;
                        if (t == WraithEnemy)
                        {
                            SpawnableIndex = i;
                            WaterWraithMod.Logger.LogInfo($"Found a WaterWraith at index {i} in the enemies list!");
                            break;
                        }
                    }

                    if (SpawnableIndex == -1)
                    {
                        WaterWraithMod.Logger.LogWarning("No WaterWraith found in the enemies list! Injecting...");
                        SpawnableEnemyWithRarity waterRare = new SpawnableEnemyWithRarity();
                        waterRare.enemyType = WraithEnemy;
                        waterRare.rarity = 0;
                        __instance.currentLevel.Enemies.Add(waterRare);
                        SpawnableIndex = __instance.currentLevel.Enemies.IndexOf(waterRare);
                        WaterWraithMod.Logger.LogInfo($"WaterWraith added to the enemies list at index {SpawnableIndex}");
                    }

                    int RNG = UnityEngine.Random.Range(0, 100);

                    switch (WaterWraithMod.WraithSpawnPositionConfig.Value)
                    {
                        case WraithSpawnPosition.OnlyIndoors:
                            if (spawnPointsIn.Length > 0)
                            {
                                __instance.SpawnEnemyOnServer(
                                    spawnPointsIn[UnityEngine.Random.Range(0, spawnPointsIn.Length)].transform.position,
                                    WraithEnemy.enemyPrefab.transform.rotation.y, SpawnableIndex);
                            }
                            break;
                        case WraithSpawnPosition.OnlyOutdoors:
                            if (spawnPointsOut.Length > 0)
                            {
                                __instance.SpawnEnemyOnServer(
                                    spawnPointsOut[UnityEngine.Random.Range(0, spawnPointsOut.Length)].transform.position,
                                    WraithEnemy.enemyPrefab.transform.rotation.y, SpawnableIndex);
                            }
                            break;
                        case WraithSpawnPosition.IndoorsAndOutdoors:
                            List<GameObject> EverySpawnPoint = new List<GameObject>();
                            EverySpawnPoint.AddRange(spawnPointsOut);
                            EverySpawnPoint.AddRange(spawnPointsIn);
                            if (EverySpawnPoint.Count > 0)
                            {
                                __instance.SpawnEnemyOnServer(
                                    EverySpawnPoint[UnityEngine.Random.Range(0, EverySpawnPoint.Count)].transform.position,
                                    WraithEnemy.enemyPrefab.transform.rotation.y, SpawnableIndex);
                            }
                            break;
                    }
                }
            }
        }
    }
}