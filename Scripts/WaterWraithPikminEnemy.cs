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
        public List<EnemyAICollisionDetect> enemyAICollisionDetects = null!;
        public List<PikminAI> PikminImmune = new List<PikminAI>();
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
            foreach (WaterWraithMeshOverride WWMO in GetComponentsInChildren<WaterWraithMeshOverride>(true))
            {
                GameObject child = WWMO.PikminColider;

                child.gameObject.SetActive(true);
                PikminLatchTrigger latchTrigger = LethalMin.Patches.EnemyAIPatch.AddLatchTriggerToColider(child.gameObject, transform);
                LatchTriggers.Add(latchTrigger);
                enemyAICollisionDetects.Add(child.GetComponent<EnemyAICollisionDetect>());
                WaterWraithMod.Logger.LogInfo($"Added {child.name} to LatchTriggers");
            }
        }

        protected void LateUpdate()
        {
            bool cba = waterWraithAI.isVurable.Value;


            if (CanBeAttacked && !waterWraithAI.isVurable.Value)
            {
                PikminImmune.Clear();
                foreach (PikminLatchTrigger trigger in LatchTriggers)
                {
                    PikminImmune.AddRange(trigger.PikminOnLatch);
                    trigger.RemoveAllPikmin(0);
                }
            }
            CanBeAttacked = cba;
            foreach (EnemyAICollisionDetect EAICD in enemyAICollisionDetects)
            {
                EAICD.enabled = CanBeAttacked;
            }
        }
    }
}
