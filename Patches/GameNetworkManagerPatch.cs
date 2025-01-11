using HarmonyLib;
using UnityEngine;
using Unity.Netcode;
using WaterWraithMod;

namespace WaterWraithMod.Patches
{
    [HarmonyPatch(typeof(GameNetworkManager))]
    internal class GameNetworkManagerPatch
    {
        public static bool HasInitalized;
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void Init(GameNetworkManager __instance)
        {
            if (HasInitalized == true) { WaterWraithMod.Logger.LogWarning("Already initalized WaterWraithMod"); return; }

            EnemyType WraithEnemy = WaterWraithMod.assetBundle.LoadAsset<EnemyType>("Assets/ModAsset/WaterType.asset");
            NetworkManager.Singleton.AddNetworkPrefab(WraithEnemy.enemyPrefab);
            WaterWraithMod.Logger.LogInfo("WaterWraithMod initialized on network");

            HasInitalized = true;
        }
    }
}