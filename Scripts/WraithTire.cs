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
    public class WraithTire : MonoBehaviour
    {
        public WaterWraithAI ai = null!;

        private Dictionary<Collider, float> objectsInTriggerWDelay = new Dictionary<Collider, float>();
        private const float collisionDelay = 0.5f; // Adjust this value as needed

        private void OnTriggerEnter(Collider other)
        {
            if(!enabled){return;}
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            EnemyAI enemy = other.GetComponent<EnemyAI>();
            if (player == null && enemy == null)
            {
                return;
            }

            if (!objectsInTriggerWDelay.ContainsKey(other))
            {
                objectsInTriggerWDelay.Add(other, 2f);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            //objectsInTriggerWDelay.Remove(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if(!enabled){return;}
            Collider key = other;
            if (objectsInTriggerWDelay.ContainsKey(other))
            {
                objectsInTriggerWDelay[key] += Time.deltaTime;
                WaterWraithMod.Logger.LogInfo($"Buffering collision with {other.name} for {objectsInTriggerWDelay[key]} seconds...");
                if (objectsInTriggerWDelay[key] >= collisionDelay)
                {
                    HandleCollision(key);
                    objectsInTriggerWDelay[key] = 0f;
                }
            }
        }

        private void HandleCollision(Collider other)
        {
            if(!enabled){return;}
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player != null && !player.isPlayerDead)
            {
                ai.DamagePlayer(player);
            }
            else
            {
                EnemyAI enemy = other.GetComponent<EnemyAI>();
                if (enemy != null && !enemy.isEnemyDead)
                {
                    ai.DamageEnemy(enemy);
                }
                else
                {
                    objectsInTriggerWDelay.Remove(other);
                    WaterWraithMod.Logger.LogInfo($"Remvoing null/dead component from trigger: {other?.name}");
                }
            }
        }

        public List<Collider> GetobjectsInTriggerWDelay()
        {
            return new List<Collider>(objectsInTriggerWDelay.Keys);
        }

        public List<T> GetObjectsOfType<T>() where T : Component
        {
            return objectsInTriggerWDelay.Keys
                .Select(c => c.GetComponent<T>())
                .Where(component => component != null)
                .ToList();
        }
    }
}