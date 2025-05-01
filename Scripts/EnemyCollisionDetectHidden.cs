using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using LethalMin;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using WaterWraithMod.Patches;

namespace WaterWraithMod.Scripts
{
    public class EnemyCollisionDetectHidden : EnemyAICollisionDetect
    {
        public new void OnTriggerStay(Collider other)
        {

        }
    }
}
