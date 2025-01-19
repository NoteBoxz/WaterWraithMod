using HarmonyLib;
using UnityEngine;
using Unity.Netcode;
using WaterWraithMod;
using LethalMin;
using WaterWraithMod.Scripts;

namespace WaterWraithMod.Patches
{
    [HarmonyPatch(typeof(PurplePikmin))]
    internal class PurplePikminPatch
    {
        [HarmonyPatch("PlaySlamSFXClientRpc")]
        [HarmonyPostfix]
        public static void Init(PurplePikmin __instance)
        {
            if (!__instance.IsServer)
            {
                return;
            }

            foreach (WaterWraithAI wraith in GameObject.FindObjectsOfType<WaterWraithAI>())
            {
                if (Vector3.Distance(wraith.transform.position, __instance.transform.position) < 10f)
                {
                    if (!wraith.isVurable.Value)
                        wraith.SetScaredServerRpc();
                }
            }
        }
    }
}