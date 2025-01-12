using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using LethalMin;
using Unity.Netcode;
using UnityEngine;
using WaterWraithMod.Patches;

namespace WaterWraithMod.Scripts
{
    public class WaterWraithAI : EnemyAI
    {

        public AISearchRoutine roamFactory = null!;
        public EnemyAI TargetEnemy = null!;
        public List<EnemyAI> TargetedEnemies = null!;
        public WaterWraithMesh WMesh = null!;
        public AudioSource moveAud = null!;
        public AudioClip KillSFX = null!;
        public bool CanEnterState2 = false;
        public bool IsChaseingEnemy = false;
        public bool IsWandering = false;
        float timeSinceHitting;

        public override void Start()
        {
            base.Start();
            moveAud.volume = 0;
            inSpecialAnimation = true;
            List<GameObject> AllSpawnPoints = new List<GameObject>();
            GameObject[] spawnPointsIn = GameObject.FindGameObjectsWithTag("AINode");
            GameObject[] spawnPointsOut = GameObject.FindGameObjectsWithTag("OutsideAINode");
            AllSpawnPoints.AddRange(spawnPointsOut);
            AllSpawnPoints.AddRange(spawnPointsIn);
            GameObject closestSpawnPoint = AllSpawnPoints.OrderBy(p => Vector3.Distance(p.transform.position, transform.position)).FirstOrDefault();
            WaterWraithMod.Logger.LogInfo($"WaterWraith: Destemined {closestSpawnPoint.name}({closestSpawnPoint.tag})" +
             $" is the position it spawned at: {closestSpawnPoint.transform.position} - {transform.position}");
            if (closestSpawnPoint.CompareTag("OutsideAINode"))
            {
                WaterWraithMod.Logger.LogInfo("WaterWraith: WaterWraith determined it spawned outside");
                isOutside = true;
            }
            else
            {
                WaterWraithMod.Logger.LogInfo("WaterWraith: WaterWraith determined it spawned inside");
                isOutside = false;
            }
            WMesh.SetOverride((int)WaterWraithMod.gameStleConfig.Value);
            StartCoroutine(WaitForCrush());
        }

        IEnumerator WaitForCrush()
        {
            yield return new WaitForSeconds(8.5f);
            inSpecialAnimation = false;
            WaterWraithMod.Logger.LogInfo("WaterWraith: Should be able to move now");
            yield return new WaitForSeconds(2f);
            CanEnterState2 = true;
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (StartOfRound.Instance.livingPlayers == 0 || isEnemyDead)
            {
                moveAud.volume = 0;
                return;
            }

            switch (currentBehaviourStateIndex)
            {
                case 0:
                    if (!IsWandering)
                    {
                        WaterWraithMod.Logger.LogInfo("WaterWraith: Starting Search!");
                        StartSearch(transform.position, roamFactory);
                        IsWandering = true;
                    }

                    //Chase Condisitions
                    if (CheckLineOfSightForPlayer(360, 15) && CanEnterState2)
                    {
                        StopSearch(roamFactory);
                        SetMovingTowardsTargetPlayer(CheckLineOfSightForPlayer(360, 15));
                        IsChaseingEnemy = false;
                        WaterWraithMod.Logger.LogInfo("WaterWraith: Chasing player");
                        SwitchToBehaviourClientRpc(1);
                        break;
                    }
                    List<GameObject> enemyObjects = RoundManager.Instance.SpawnedEnemies.
                    Where(enemy => enemy != null && enemy.enemyType != enemyType && !TargetedEnemies.Contains(enemy)).Select(enemy => enemy.gameObject).ToList();
                    if (CheckLineOfSight(enemyObjects, 360, 15)
                        && CanEnterState2
                        && WaterWraithMod.ChaseEnemyConfig.Value)
                    {
                        if (CheckLineOfSight(enemyObjects, 360, 15).GetComponentInChildren<EnemyAI>() != null)
                        {
                            StopSearch(roamFactory);
                            TargetEnemy = CheckLineOfSight(enemyObjects, 360, 15).GetComponentInChildren<EnemyAI>();
                            IsChaseingEnemy = true;
                            WaterWraithMod.Logger.LogInfo("WaterWraith: Chasing enemy");
                            SwitchToBehaviourClientRpc(1);
                        }
                        break;
                    }
                    break;
                case 1:
                    if (IsWandering)
                    {
                        StopSearch(roamFactory);
                        IsWandering = false;
                    }

                    //EnemyIf
                    if (IsChaseingEnemy &&
                        (TargetEnemy == null ||
                        TargetEnemy.isEnemyDead ||
                        TargetedEnemies.Contains(TargetEnemy) ||
                        Vector3.Distance(transform.position, TargetEnemy.transform.position) > 20f))
                    {
                        TargetEnemy = null!;
                        IsChaseingEnemy = false;
                        WaterWraithMod.Logger.LogInfo("WaterWraith: stopped Chasing enemy");
                        SwitchToBehaviourClientRpc(0);
                        break;
                    }
                    else if (IsChaseingEnemy)
                    {
                        SetDestinationToPosition(TargetEnemy.transform.position);
                    }

                    //PlayerIf
                    if (!IsChaseingEnemy &&
                        (targetPlayer == null ||
                        targetPlayer.isPlayerDead ||
                        !targetPlayer.isPlayerControlled ||
                        Vector3.Distance(transform.position, targetPlayer.transform.position) > 30f))
                    {
                        targetPlayer = null!;
                        movingTowardsTargetPlayer = false;
                        if (Vector3.Distance(transform.position, RoundManager.FindMainEntrancePosition()) < 10)
                        {
                            roamFactory.currentTargetNode = ChooseFarthestNodeFromPosition(RoundManager.FindMainEntrancePosition()).gameObject;
                            roamFactory.choseTargetNode = true;
                            WaterWraithMod.Logger.LogInfo("WaterWraith: Setting position away from main");
                        }
                        WaterWraithMod.Logger.LogInfo("WaterWraith: stopped Chasing player");
                        SwitchToBehaviourClientRpc(0);
                        break;
                    }

                    break;
            }
        }
        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            if (timeSinceHitting < 0.5f)
            {
                return;
            }
            timeSinceHitting = 0;
            PlayerControllerB P = other.gameObject.GetComponent<PlayerControllerB>();

            if (P != null)
            {
                Vector3 backDirection = P.transform.forward;
                P.DamagePlayer(WaterWraithMod.DamageConfig.Value, true, true, CauseOfDeath.Crushing, 1, false, backDirection * 25);
                if (P.isPlayerDead)
                {
                    creatureSFX.PlayOneShot(KillSFX);
                }
                else
                {
                    P.externalForceAutoFade += -backDirection * 5;
                }
            }
        }
        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null!)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);
            if (timeSinceHitting < 0.5f)
            {
                return;
            }
            timeSinceHitting = 0;
            if (collidedEnemy != null)
            {
                if (WaterWraithMod.IsDependencyLoaded("NoteBoxz.LethalMin") && LETHALMIN_ISRESISTANTTOCRUSH(collidedEnemy))
                {
                    timeSinceHitting = 1;
                    return;
                }
                collidedEnemy.HitEnemy(2, null, true);
                if (!collidedEnemy.isEnemyDead)
                    collidedEnemy.stunNormalizedTimer = UnityEngine.Random.Range(0.1f, 1f);

                TargetedEnemies.Add(collidedEnemy);
            }
        }

        public bool LETHALMIN_ISRESISTANTTOCRUSH(EnemyAI ai)
        {
            PikminAI Pai = ai.GetComponent<PikminAI>();
            if (Pai != null)
            {
                return LethalMin.LethalMin.IsPikminResistantToHazard(Pai.PminType, HazardType.Crush);
            }
            return false;
        }

        Vector3 LastPosition;
        float LastPosCheck;
        void LateUpdate()
        {
            if (IsServer)
            {
                moveAud.volume = agent.velocity.normalized.magnitude;

                creatureAnimator.SetBool("Moving", agent.velocity.normalized.magnitude > 0.1f);
            }
            else
            {
                LastPosCheck += Time.deltaTime;
                if (Vector3.Distance(LastPosition, transform.position) > 0.1f)
                {
                    LastPosition = transform.position;
                    moveAud.volume = 1f;
                    creatureAnimator.SetBool("Moving", true);
                }
                else if (LastPosCheck > 1f)
                {
                    LastPosCheck = 0f;
                    moveAud.volume = 0f;
                    creatureAnimator.SetBool("Moving", false);
                }
            }

            timeSinceHitting += Time.deltaTime;
        }
    }
}
