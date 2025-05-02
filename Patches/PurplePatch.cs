using HarmonyLib;
using UnityEngine;
using LethalMin;
using WaterWraithMod.Scripts;

namespace WaterWraithMod.Patches
{
    [HarmonyPatch(typeof(PurplePikminAI))]
    internal class PurplePikminAIPatch
    {
        [HarmonyPatch(nameof(PurplePikminAI.DoSlam))]
        [HarmonyPostfix]
        public static void SlamDone(PurplePikminAI __instance)
        {
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