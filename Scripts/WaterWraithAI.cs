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
        public NetworkVariable<bool> isVurable = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> isRunning = new NetworkVariable<bool>(false);
        float timeSinceHitting;
        float timeInEnemyChase;
        float timeBeingScared;
        public GameObject[] TireObjectsToDisable = [];
        public Collider[] ColidersToDisable = [];
        TireFragments[] TireObjectsToEnable = [];
        public PlayerControllerB PlayerFleeingFrom = null!;
        public PlayerControllerB PlayerStunnedBy = null!;
        public AudioClip[] DethSounds = [];
        public AudioClip[] HurtSounds = [];
        WraithTire[] Tires = [];

        public override void Start()
        {
            base.Start();
            TireObjectsToEnable = GetComponentsInChildren<TireFragments>(true);
            Tires = GetComponentsInChildren<WraithTire>(true);
            WaterWraithMod.Logger.LogInfo($"Tires: {Tires.Length}");
            WaterWraithMod.Logger.LogInfo($"DulyAbledTires: {TireObjectsToEnable.Length}");
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
                    if (WaterWraithMod.ChaseEnemyConfig.Value
                        && CheckLineOfSight(enemyObjects, 360, 15)
                        && CanEnterState2)
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
                        agent.speed = 7f;
                        agent.acceleration = 999;
                        agent.angularSpeed = 300;
                        SetDestinationToPosition(FleePos);
                        if (Vector3.Distance(transform.position, FleePos) < 2f
                            || !agent.CalculatePath(FleePos, new NavMeshPath()))
                        {
                            if (Vector3.Distance(transform.position, FleePos) < 2f)
                            {
                                WaterWraithMod.Logger.LogInfo("WaterWraith: Exiting flee due to distance");
                            }
                            if (!agent.CalculatePath(FleePos, new NavMeshPath()))
                            {
                                WaterWraithMod.Logger.LogInfo("WaterWraith: Exiting flee do to no pos");
                            }
                            WaterWraithMod.Logger.LogInfo($"Flee pos: {FleePos}, Water pos: {transform.position}");
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
                        FleePos = ChooseFarthestPositionFromPosition(b.transform.position);
                        SetDestinationToPosition(FleePos);
                        WaterWraithMod.Logger.LogInfo("WaterWraith: Fleeing from player!");
                        break;
                    }
                    break;
                case 3:
                    if (IsWandering)
                    {
                        StopSearch(roamFactory);
                        IsWandering = false;
                    }
                    SetDestinationToPosition(transform.position);
                    agent.speed = 0;
                    agent.acceleration = 0;
                    agent.angularSpeed = 0;
                    if (timeBeingScared > 5)
                    {
                        agent.speed = 3.5f;
                        agent.acceleration = 8;
                        agent.angularSpeed = 120;
                        timeBeingScared = 0;
                        CanEnterState2 = true;
                        isVurable.Value = false;
                        WaterWraithMod.Logger.LogInfo("WaterWraith: Resetting");
                        if (HasLostTires)
                        {
                            FleePos = ChooseFarthestPositionFromPosition(transform.position, 80f);
                            SwitchToBehaviourClientRpc(2);
                        }
                        else
                        {
                            SwitchToBehaviourClientRpc(0);
                        }
                        break;
                    }
                    break;
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null!, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            if (!isVurable.Value)
            {
                return;
            }
            if (isEnemyDead || inSpecialAnimation)
            {
                return;
            }
            enemyHP -= force;
            timeBeingScared += Random.Range(0.0f, 1);
            if (enemyHP <= 0)
            {
                if (!HasLostTires)
                {
                    //if (IsServer)
                    RemoveTiresServerRpc();
                }
                else
                {
                    KillWraithServerRpc();
                }
            }
        }

        public override void HitFromExplosion(float distance)
        {
            base.HitFromExplosion(distance);
            WaterWraithMod.Logger.LogInfo($"Wraith kabboom!");
            SetScaredServerRpc();
        }
        public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null!)
        {
            base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
            if (!setToStunned) { return; }
            WaterWraithMod.Logger.LogInfo($"Wraith cstuned!");
            SetScaredServerRpc();
        }
        [ServerRpc(RequireOwnership = false)]
        public void RemoveTiresServerRpc()
        {
            WaterWraithMod.Logger.LogInfo($"Removing On Serer");
            if (!inSpecialAnimation && !HasLostTires)
            {
                RemoveTiresClientRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetScaredServerRpc()
        {
            if (!isVurable.Value)
            {
                WaterWraithMod.Logger.LogInfo($"SetScared");
                SwitchToBehaviourClientRpc(3);
                timeBeingScared = 0;
                isVurable.Value = true;
            }
            else
            {
                WaterWraithMod.Logger.LogInfo($"Already scared");
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
            targetPlayer = null;
            enemyHP = 10;
            inSpecialAnimation = true;
            moveAud.volume = 0;
            HasLostTires = true;
            creatureAnimator.SetTrigger("KnockedOff");
            creatureAnimator.SetBool("HasLostRollers", true);
            foreach (GameObject Go in TireObjectsToDisable)
            {
                Go.SetActive(false);
            }
            foreach (TireFragments frag in TireObjectsToEnable)
            {
                GameObject Go = frag.gameObject;
                if (Go.transform.parent.gameObject.activeSelf)
                    Go.SetActive(true);
                Go.transform.SetParent(null, true);
            }
            foreach (Collider col in ColidersToDisable)
            {
                col.enabled = false;
            }
            foreach (WraithTire Wtire in Tires)
            {
                Wtire.enabled = false;
            }
            StartCoroutine(WaitToFlee());
        }

        IEnumerator WaitToFlee()
        {
            yield return new WaitForSeconds(2.6f);
            FleePos = ChooseFarthestPositionFromPosition(transform.position);
            SwitchToBehaviourClientRpc(2);
            inSpecialAnimation = false;

        }


        [ServerRpc(RequireOwnership = false)]
        public void KillWraithServerRpc()
        {
            if (!inSpecialAnimation && !isEnemyDead)
            {
                WaterWraithMod.Logger.LogInfo("Killing Wraith");

                KillWraithClientRpc();
            }
        }

        [ClientRpc]
        public void KillWraithClientRpc()
        {
            creatureAnimator.SetTrigger("Die");
            inSpecialAnimation = true;
            creatureVoice.PlayOneShot(DethSounds[Random.Range(0, DethSounds.Length - 1)]);
            if (IsServer)
                StartCoroutine(CallKillOnServer());
        }

        IEnumerator CallKillOnServer()
        {
            yield return new WaitForSeconds(2.5f);
            KillEnemyOnOwnerClient();
        }



        public void DamagePlayer(PlayerControllerB player)
        {
            if (isVurable.Value)
            {
                return;
            }
            if (player != null)
            {
                //Create a vector 3 that is pointed away from the water wraith's direciton
                Vector3 backDirection = (player.transform.position - transform.position).normalized;

                player.DamagePlayer(WaterWraithMod.DamageConfig.Value, true, true, CauseOfDeath.Crushing, 1, false, -backDirection * 5);
                if (player.isPlayerDead)
                {
                    creatureSFX.PlayOneShot(KillSFX);
                }
                else
                {
                    player.externalForceAutoFade += backDirection * 7;
                }
            }
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);

            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player != null && HasLostTires && !isVurable.Value && isRunning.Value)
            {
                player.DamagePlayer(0);
                Vector3 backDirection = (player.transform.position - transform.position).normalized;
                player.externalForceAutoFade += backDirection * 2.5f;
            }
        }



        public void DamageEnemy(EnemyAI enemy)
        {
            if (isVurable.Value)
            {
                return;
            }
            if (!IsServer)
            {
                return;
            }
            if (enemy != null && !enemy.isEnemyDead)
            {
                if (WaterWraithMod.IsDependencyLoaded("NoteBoxz.LethalMin") && LETHALMIN_ISRESISTANTTOCRUSH(enemy))
                {
                    TargetedEnemies.Add(enemy);
                    WaterWraithMod.Logger.LogInfo($"Cannot Hit Pikmin: {enemy.name}");
                    return;
                }
                WaterWraithMod.Logger.LogInfo($"Hitting enemy: {enemy.name}");
                int HPbeforeHit = enemy.enemyHP;
                enemy.HitEnemyServerRpc(WaterWraithMod.EDamageConfig.Value, -1, true);

                if (!enemy.isEnemyDead)
                    enemy.stunNormalizedTimer += UnityEngine.Random.Range(0f, 1f);

                if (HPbeforeHit == enemy.enemyHP && enemy == TargetEnemy)
                {
                    WaterWraithMod.Logger.LogInfo($"Water Wraith has determined that target enemy {enemy.name} is unkillable");
                    TargetedEnemies.Add(enemy);
                }
            }
        }
        public bool LETHALMIN_ISRESISTANTTOCRUSH(EnemyAI ai)
        {
            PikminAI Pai = ai.GetComponent<PikminAI>();
            if (Pai != null)
            {
                return LethalMin.Utils.PikminChecks.IsPikminResistantToHazard(Pai, PikminHazard.Crush, this);
            }
            return false;
        }

        Vector3 LastPosition;
        float LastPosCheck;
        bool lastIsScaredCheck;
        void LateUpdate()
        {
            if (isEnemyDead || inSpecialAnimation)
            {
                return;
            }

            if (IsServer)
            {
                if (isVurable.Value)
                {
                    timeBeingScared += Time.deltaTime;
                }
                if (!HasLostTires)
                    moveAud.volume = agent.velocity.normalized.magnitude;

                creatureAnimator.SetBool("Moving", agent.velocity.normalized.magnitude > 0.1f);
                creatureAnimator.SetBool("IsRunning", HasLostTires && FleePos != Vector3.zero);
                isRunning.Value = HasLostTires && FleePos != Vector3.zero;
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
                creatureAnimator.SetBool("IsRunning", HasLostTires && isRunning.Value);
            }

            creatureAnimator.SetBool("IsScared", isVurable.Value);
            if (lastIsScaredCheck != isVurable.Value)
            {
                lastIsScaredCheck = isVurable.Value;
                if (isVurable.Value == true)
                {
                    creatureAnimator.SetTrigger("Scared");
                }
            }

            timeSinceHitting += Time.deltaTime;
        }


        public Vector3 ChooseFarthestPositionFromPosition(Vector3 pos, float MaxDistance = 25f)
        {
            Vector3 directionAwayFromPos = (transform.position - pos).normalized;
            Vector3 targetPosition = transform.position + directionAwayFromPos * MaxDistance;

            NavMeshHit hit;
            Vector3 result = transform.position;
            float maxDistanceFound = 0f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * (MaxDistance * 0.1f);
                randomOffset.y = 0;
                Vector3 samplePosition = targetPosition + randomOffset;

                if (NavMesh.SamplePosition(samplePosition, out hit, MaxDistance, NavMesh.AllAreas))
                {
                    float distanceFromPos = Vector3.Distance(hit.position, pos);
                    if (distanceFromPos > maxDistanceFound && !PathIsIntersectedByLineOfSight(hit.position, false, false))
                    {
                        maxDistanceFound = distanceFromPos;
                        result = hit.position;
                    }
                }
            }

            WaterWraithMod.Logger.LogInfo($"Chosen flee position: {result}, Distance: {maxDistanceFound}");
            if (maxDistanceFound == 0)
            {
                WaterWraithMod.Logger.LogInfo($"Wraith cornnered!");
                SetScaredServerRpc();
            }
            else
            {
                if (isVurable.Value)
                {
                    isVurable.Value = false;
                }
            }
            return result;
        }
    }
}
