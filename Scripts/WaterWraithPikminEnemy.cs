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
    public class WaterWraithPikminEnemy : PikminEnemy
    {
        WaterWraithAI waterWraithAI = null!;
        public EnemyAICollisionDetect enemyAICollisionDetect = null!;
        protected override void Start()
        {
            base.Start();
            waterWraithAI = enemyScript as WaterWraithAI ?? throw new System.Exception("WaterWraithPE: enemyScript is not a WaterWraithAI");
            if (waterWraithAI == null)
            {
                enabled = false;
                return;
            }
            OverrideCanDie = true;
            foreach (PikminLatchTrigger trigger in LatchTriggers)
            {
                Destroy(trigger);
            }
            LatchTriggers.Clear();
            foreach (Collider child in GetComponentsInChildren<Collider>(true))
            {
                if (child.name == "LethalMinLatchTrigger")
                {
                    child.gameObject.SetActive(true);
                    enemyAICollisionDetect = child.gameObject.AddComponent<EnemyCollisionDetectHidden>();
                    PikminLatchTrigger latchTrigger = LethalMin.Patches.EnemyAIPatch.AddLatchTriggerToColider(child.gameObject, transform);
                    LatchTriggers.Add(latchTrigger);
                    WaterWraithMod.Logger.LogInfo($"Added {child.name} to LatchTriggers");
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            bool cba = waterWraithAI.isVurable.Value;


            if (CanBeAttacked && !waterWraithAI.isVurable.Value)
            {
                LatchTriggers[0].RemoveAllPikmin(0);
            }
            CanBeAttacked = cba;
            enemyAICollisionDetect.enabled = cba;
        }
    }
}
