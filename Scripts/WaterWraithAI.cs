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
        public bool HasLostTires;
        public Vector3 FleePos;
        public bool IsVunerable;
        float timeSinceHitting;
        float timeInEnemyChase;
        public GameObject[] TireObjectsToDisable = [];
        public GameObject[] TireObjectsToEnable = [];
        public PlayerControllerB PlayerFleeingFrom;

        public override void Start()
        {
            base.Start();
            WMesh.SetOverride((int)WaterWraithMod.gameStleConfig.Value);
            creatureAnimator.SetTrigger("Spawn");
            inSpecialAnimation = true;
            moveAud.volume = 0;
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
            if (StartOfRound.Instance.livingPlayers == 0 || isEnemyDead || inSpecialAnimation)
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
                            timeInEnemyChase = 0;
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
                        Vector3.Distance(transform.position, TargetEnemy.transform.position) > 20f)
                        || timeInEnemyChase > 25)
                    {
                        if (TargetEnemy != null)
                            TargetedEnemies.Add(TargetEnemy);

                        TargetEnemy = null!;
                        IsChaseingEnemy = false;
                        WaterWraithMod.Logger.LogInfo("WaterWraith: stopped Chasing enemy");
                        SwitchToBehaviourClientRpc(0);
                        break;
                    }
                    else if (IsChaseingEnemy)
                    {
                        SetDestinationToPosition(TargetEnemy.transform.position);
                        timeInEnemyChase += Time.deltaTime;
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
                case 2:
                    if (FleePos != Vector3.zero)
                    {
                        if (IsWandering)
                        {
                            StopSearch(roamFactory);
                            IsWandering = false;
                        }
                        agent.speed = 15;
                        agent.acceleration = 999;
                        agent.angularSpeed = 300;
                        SetDestinationToPosition(FleePos);
                        if (Vector3.Distance(transform.position, FleePos) < 30f
                            || !agent.CalculatePath(FleePos, new NavMeshPath()))
                        {
                            FleePos = Vector3.zero;
                        }
                    }
                    if (!IsWandering && FleePos == Vector3.zero)
                    {
                        WaterWraithMod.Logger.LogInfo("WaterWraith: Starting Search 2!");
                        agent.speed = 3.5f;
                        agent.acceleration = 8;
                        agent.angularSpeed = 120;
                        StartSearch(transform.position, roamFactory);
                        IsWandering = true;
                    }
                    PlayerControllerB b = CheckLineOfSightForPlayer(360, 15);
                    if (IsWandering && b)
                    {
                        FleePos = ChooseFarthestNodeFromPosition(b.transform.position).position;
                        SetDestinationToPosition(FleePos);
                        WaterWraithMod.Logger.LogInfo("WaterWraith: Fleeing from player!");
                        break;
                    }
                    break;
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            enemyHP -= force;
            if (enemyHP <= 0)
            {
                if (!HasLostTires)
                {
                    if (IsServer)
                        RemoveTiresClientRpc();
                }
                else
                {
                    KillEnemyOnOwnerClient();
                }
            }
        }

        public override void KillEnemy(bool destroy = false)
        {
            base.KillEnemy(destroy);
        }

        [ClientRpc]
        public void RemoveTiresClientRpc()
        {
            if (HasLostTires)
            {
                WaterWraithMod.Logger.LogWarning("The wraith has already removed it's tires");
                return;
            }
            WaterWraithMod.Logger.LogInfo("Removing Tires");
            if (IsServer && IsWandering)
            {
                StopSearch(roamFactory);
                IsWandering = false;
            }
            enemyHP = 5;
            inSpecialAnimation = true;
            moveAud.volume = 0;
            HasLostTires = true;
            creatureAnimator.SetTrigger("KnockedOff");
            creatureAnimator.SetBool("HasLostRollers", true);
            foreach (GameObject Go in TireObjectsToDisable)
            {
                Go.SetActive(false);
            }
            foreach (GameObject Go in TireObjectsToEnable)
            {
                Go.SetActive(true);
                Go.transform.SetParent(null, true);
            }
            StartCoroutine(WaitToFlee());
        }

        IEnumerator WaitToFlee()
        {
            yield return new WaitForSeconds(2.6f);
            FleePos = ChooseFarthestNodeFromPosition(transform.position).position;
            SwitchToBehaviourClientRpc(2);
            inSpecialAnimation = false;

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
                P.DamagePlayer(WaterWraithMod.DamageConfig.Value, true, true, CauseOfDeath.Crushing, 1, false, backDirection * 10);
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
            if (collidedEnemy != null && !collidedEnemy.isEnemyDead)
            {
                if (WaterWraithMod.IsDependencyLoaded("NoteBoxz.LethalMin") && LETHALMIN_ISRESISTANTTOCRUSH(collidedEnemy))
                {
                    timeSinceHitting = 1;
                    return;
                }

                collidedEnemy.HitEnemy(WaterWraithMod.EDamageConfig.Value, null, true);

                if (!collidedEnemy.isEnemyDead)
                    collidedEnemy.stunNormalizedTimer += UnityEngine.Random.Range(0f, 1f);

                timeSinceHitting = 0;
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
                if (!HasLostTires)
                    moveAud.volume = agent.velocity.normalized.magnitude;

                creatureAnimator.SetBool("Moving", agent.velocity.normalized.magnitude > 0.1f);
                creatureAnimator.SetBool("IsRunning", HasLostTires && FleePos != Vector3.zero);
            }
            else
            {
                LastPosCheck += Time.deltaTime;
                if (Vector3.Distance(LastPosition, transform.position) > 0.1f)
                {
                    LastPosition = transform.position;
                    if (!HasLostTires)
                        moveAud.volume = 1f;
                    creatureAnimator.SetBool("Moving", true);
                }
                else if (LastPosCheck > 1f)
                {
                    LastPosCheck = 0f;
                    if (!HasLostTires)
                        moveAud.volume = 0f;
                    creatureAnimator.SetBool("Moving", false);
                }
            }

            timeSinceHitting += Time.deltaTime;
        }
    }
}
