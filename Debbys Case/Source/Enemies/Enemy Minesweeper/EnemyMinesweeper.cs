using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
namespace REPODebbysCase.Enemies
{
    public class EnemyMinesweeper : MonoBehaviour
    {
        public enum State
        {
            Spawn,
            Idle,
            Roam,
            Investigate,
            Stalk,
            Attack,
            Leave,
            Stun,
            Despawn
        }
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public State currentState;
        public bool stateImpulse;
        public float stateTimer;
        public Enemy enemyBase;
        public Rigidbody rigidbody;
        public PhotonView photonView;
        public EnemyNavMeshAgent navAgent;
        public PlayerAvatar targetPlayer;
        public PhysGrabObject targetObject;
        public HurtCollider hurtCollider;
        public Vector3 newPosition;
        public List<GameObject> objectContainer;
        public Dictionary<PlayerAvatar, bool> playersTumbled;
        public EnemyMinesweeperAnim animator;
        public Transform visuals;
        public Vector3 deathLocation;
        public float visionTimer;
        public float playerVisionTimer;
        public float objectVisionTimer;
        public bool stalkObject;
        public float stalkNewPointTimer;
        public bool respawnItems;
        public float respawnItemsTimer = 0f;
        public int containedAmount;
        public Vector3 originalScale;
        public float stalkAttackCooldown = 0f;
        public bool dead = false;
        public void Awake()
        {
            log.LogDebug("An Enemy Minesweeper has spawned!");
            originalScale = visuals.localScale;
        }
        public void UpdateState(State newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                stateTimer = 0f;
                stateImpulse = true;
                if (SemiFunc.IsMultiplayer())
                {
                    photonView.RPC("UpdateStateRPC", RpcTarget.All, newState);
                }
                else
                {
                    UpdateStateRPC(newState);
                }
            }
        }
        [PunRPC]
        public void UpdateStateRPC(State newState)
        {
            currentState = newState;
        }
        public void Update()
        {
            if (!LevelGenerator.Instance.Generated || SemiFunc.IsNotMasterClient())
            {
                return;
            }
            RespawnSuckedItems();
            SelectPlayerGrabbed();
            if (enemyBase.CurrentState == EnemyState.Despawn)
            {
                UpdateState(State.Despawn);
            }
            else if (enemyBase.IsStunned())
            {
                UpdateState(State.Stun);
            }
            switch (currentState)
            {
                case State.Spawn:
                    StateSpawn();
                    break;
                case State.Idle:
                    StateIdle();
                    break;
                case State.Roam:
                    StateRoam();
                    break;
                case State.Investigate:
                    StateInvestigate();
                    break;
                case State.Stalk:
                    StateStalk();
                    break;
                case State.Attack:
                    StateAttack();
                    break;
                case State.Leave:
                    StateLeave();
                    break;
                case State.Stun:
                    StateStun();
                    break;
                case State.Despawn:
                    StateDespawn();
                    break;
            }
        }
        public void FixedUpdate()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && currentState == State.Attack && playersTumbled != null)
            {
                List<PlayerAvatar> players = SemiFunc.PlayerGetAllPlayerAvatarWithinRange(5f, hurtCollider.transform.position, true, LayerMask.GetMask("Default", "StaticGrabObject"));
                List<PhysGrabObject> physGrabObjects = SemiFunc.PhysGrabObjectGetAllWithinRange(5f, hurtCollider.transform.position, true, 0, enemyBase.Rigidbody.physGrabObject);
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].isTumbling)
                    {
                        players[i].tumble.OverrideEnemyHurt(0.5f);
                        if (!physGrabObjects.Contains(players[i].tumble.physGrabObject))
                        {
                            physGrabObjects.Add(players[i].tumble.physGrabObject);
                        }
                    }
                    else if (!players[i].isDisabled && playersTumbled.ContainsKey(players[i]))
                    {
                        if (!playersTumbled[players[i]])
                        {
                            playersTumbled[players[i]] = true;
                            players[i].tumble.TumbleRequest(true, false);
                            players[i].tumble.TumbleOverrideTime(2f);
                            players[i].tumble.OverrideEnemyHurt(0.5f);
                        }
                    }
                }
                for (int i = 0; i < physGrabObjects.Count; i++)
                {
                    if (physGrabObjects[i].isCart || physGrabObjects[i].isKinematic || (physGrabObjects[i].TryGetComponent<EnemyRigidbody>(out EnemyRigidbody thisRigidbody) && thisRigidbody == enemyBase.Rigidbody))
                    {
                        continue;
                    }
                    if (physGrabObjects[i].TryGetComponent<ItemToggle>(out ItemToggle toggleable))
                    {
                        if (physGrabObjects[i].TryGetComponent<ItemGrenade>(out ItemGrenade grenade))
                        {
                            if (SemiFunc.IsMultiplayer())
                            {
                                photonView.RPC("GrenadeResetRPC", RpcTarget.All, grenade.photonView.ViewID);
                            }
                            else
                            {
                                grenade.GrenadeReset();
                            }
                        }
                        else
                        {
                            toggleable.ToggleItem(false);
                        }
                    }
                    physGrabObjects[i].OverrideBreakEffects(0.25f);
                    physGrabObjects[i].OverrideZeroGravity(0.25f);
                    physGrabObjects[i].OverrideIndestructible(0.25f);
                    Vector3 forceDirection = (hurtCollider.transform.position - physGrabObjects[i].transform.position).normalized;
                    if (physGrabObjects[i].transform.position.y < hurtCollider.transform.position.y)
                    {
                        forceDirection.y += 0.1f;
                    }
                    physGrabObjects[i].rb.AddForce(forceDirection * 2500f * Time.fixedDeltaTime, ForceMode.Force);
                    physGrabObjects[i].rb.AddForce((SemiFunc.PhysFollowDirection(physGrabObjects[i].rb.transform, forceDirection, physGrabObjects[i].rb, 10f) * 2f) / physGrabObjects[i].rb.mass, ForceMode.Force);
                }
            }
            
        }
        [PunRPC]
        public void GrenadeResetRPC(int id)
        {
            PhotonView.Find(id).GetComponent<ItemGrenade>().GrenadeReset();
        }
        public void SelectPlayerGrabbed()
        {
            if (targetPlayer != null)
            {
                if (targetObject != targetPlayer.physGrabber.grabbedPhysGrabObject && targetPlayer.physGrabber.grabbedPhysGrabObject != null && targetPlayer.physGrabber.grabbedPhysGrabObject.playerGrabbing.Contains(targetPlayer.physGrabber))
                {
                    UpdateTarget(targetPlayer.photonView.ViewID, targetPlayer.physGrabber.grabbedPhysGrabObject.photonView.ViewID, targetPlayer, targetPlayer.physGrabber.grabbedPhysGrabObject);
                }
            }
        }
        public void AttackOrIdle()
        {
            if (SemiFunc.EnemyGetNearestPhysObject(enemyBase) != Vector3.zero)
            {
                UpdateState(State.Attack);
                return;
            }
            UpdateState(State.Idle);
            return;
        }
        public void StateSpawn()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Spawn\"");
                visuals.localScale = originalScale;
                UpdateTarget(-1, -1);
                stateTimer = 2f;
                navAgent.Warp(rigidbody.transform.position);
                navAgent.ResetPath();
                SetImpulses(340);
                dead = false;
            }
            if (stateTimer > 0f)
            {
                stateTimer -= Time.deltaTime;
            }
            else
            {
                UpdateState(State.Idle);
            }
        }
        public void StateIdle()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Idle\"");
                UpdateTarget(-1, -1);
                stateTimer = UnityEngine.Random.Range(5f, 10f);
                navAgent.Warp(rigidbody.transform.position);
                navAgent.ResetPath();
                SetImpulses(340);
            }
            if (!SemiFunc.EnemySpawnIdlePause())
            {
                stateTimer -= Time.deltaTime;
                if (SemiFunc.EnemyForceLeave(enemyBase))
                {
                    UpdateState(State.Leave);
                    return;
                }
                if (stateTimer <= 0f)
                {
                    UpdateState(State.Roam);
                    return;
                }
            }
        }
        public void StateRoam()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Roam\"");
                UpdateTarget(-1, -1);
                stateTimer = UnityEngine.Random.Range(7.5f, 15f);
                LevelPoint nextLocation = SemiFunc.LevelPointGet(enemyBase.transform.position, 7.5f, 30f) ?? SemiFunc.LevelPointGet(enemyBase.transform.position, 0f, float.MaxValue);
                if (NavMesh.SamplePosition(nextLocation.transform.position + UnityEngine.Random.insideUnitSphere * 3f, out var hit, 5f, -1) && Physics.Raycast(hit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
                {
                    newPosition = hit.position;
                }
                else
                {
                    stateImpulse = true;
                    return;
                }
                enemyBase.Rigidbody.notMovingTimer = 0f;
                SetImpulses(372);
            }
            if (SemiFunc.EnemyForceLeave(enemyBase))
            {
                UpdateState(State.Leave);
                return;
            }
            navAgent.SetDestination(newPosition);
            if (enemyBase.Rigidbody.notMovingTimer > 2f)
            {
                stateTimer -= Time.deltaTime;
            }
            if (stateTimer <= 0f || Vector3.Distance(enemyBase.transform.position, newPosition) < 1f)
            {
                AttackOrIdle();
            }
        }
        public void StateInvestigate()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Investigate\"");
                UpdateTarget(-1, -1);
                stateTimer = UnityEngine.Random.Range(10f, 25f);
                enemyBase.Rigidbody.notMovingTimer = 0f;
                SetImpulses(373);
            }
            if (SemiFunc.EnemyForceLeave(enemyBase))
            {
                UpdateState(State.Leave);
            }
            navAgent.SetDestination(newPosition);
            navAgent.OverrideAgent(navAgent.DefaultSpeed * 2f, navAgent.DefaultAcceleration, 0.2f);
            enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed * 2f);
            enemyBase.Vision.StandOverride(0.25f);
            if (enemyBase.Rigidbody.notMovingTimer > 2f)
            {
                stateTimer -= Time.deltaTime;
            }
            if (Vector3.Distance(enemyBase.transform.position, newPosition) < 1f)
            {
                UpdateState(State.Idle);
                return;
            }
            if (navAgent.CanReach(newPosition, 1f) && !NavMesh.SamplePosition(newPosition, out var _, 0.5f, -1))
            {
                UpdateState(State.Roam);
                return;
            }
            if (stateTimer <= 0f)
            {
                AttackOrIdle();
            }
        }
        public void StateStalk()
        {
            if (stateImpulse)
            {
                stalkAttackCooldown = 4f;
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Stalk\"");
                stateTimer = UnityEngine.Random.Range(7.5f, 17.5f);
                enemyBase.Rigidbody.notMovingTimer = 0f;
                SetImpulses(502);
            }
            if (SemiFunc.EnemyForceLeave(enemyBase))
            {
                UpdateState(State.Leave);
                return;
            }
            VisionCheck();
            if (targetPlayer != null && !stalkObject)
            {
                if (!enemyBase.OnScreen.GetOnScreen(targetPlayer))
                {
                    newPosition = Vector3.Lerp(enemyBase.transform.position, targetPlayer.transform.position, 0.8f);
                    navAgent.OverrideAgent(navAgent.DefaultSpeed / 2f, navAgent.DefaultAcceleration, 0.2f);
                    enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed / 2f);
                }
                else if (stalkNewPointTimer <= 0f || Vector3.Distance(enemyBase.transform.position, newPosition) < 0.25f)
                {
                    LevelPoint newLevelPoint = null;
                    if (Vector3.Distance(enemyBase.transform.position, targetPlayer.transform.position) > 10f)
                    {
                        List<LevelPoint> playerLevelPoints = SemiFunc.LevelPointGetWithinDistance(targetPlayer.transform.position, 5f, 10f);
                        float max = float.MaxValue;
                        for (int i = 0; i < playerLevelPoints.Count; i++)
                        {
                            float newMax = Vector3.Distance(enemyBase.transform.position, playerLevelPoints[i].transform.position);
                            if (newMax < max)
                            {
                                max = newMax;
                                newLevelPoint = playerLevelPoints[i];
                            }
                        }
                        if (newLevelPoint != null)
                        {
                            newPosition = newLevelPoint.transform.position;
                        }
                        else
                        {
                            List<LevelPoint> spareLevelPoints = SemiFunc.LevelPointGetWithinDistance(targetPlayer.transform.position, 10f, 20f);
                            newLevelPoint = spareLevelPoints[UnityEngine.Random.Range(0, spareLevelPoints.Count)];
                            newPosition = newLevelPoint.transform.position;
                        }
                        navAgent.OverrideAgent(navAgent.DefaultSpeed, navAgent.DefaultAcceleration, 0.2f);
                        enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed);
                    }
                    else if (targetObject == null)
                    {
                        newLevelPoint = SemiFunc.LevelPointInTargetRoomGet(targetPlayer.RoomVolumeCheck, 5f, 10f);
                        if (newLevelPoint != null)
                        {
                            newPosition = newLevelPoint.transform.position;
                        }
                        else
                        {
                            List<LevelPoint> spareLevelPoints = SemiFunc.LevelPointGetWithinDistance(targetPlayer.transform.position, 10f, 20f);
                            newLevelPoint = spareLevelPoints[UnityEngine.Random.Range(0, spareLevelPoints.Count)];
                            newPosition = newLevelPoint.transform.position;
                        }
                        navAgent.OverrideAgent(navAgent.DefaultSpeed, navAgent.DefaultAcceleration, 0.2f);
                        enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed);
                    }
                    else
                    {
                        newPosition = Vector3.Lerp(enemyBase.transform.position, targetObject.transform.position, 0.8f);
                        navAgent.OverrideAgent(navAgent.DefaultSpeed / 2f, navAgent.DefaultAcceleration, 0.2f);
                        enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed / 2f);
                    }
                    stalkNewPointTimer = UnityEngine.Random.Range(1f, 4f);
                }
                else
                {
                    stalkNewPointTimer -= Time.deltaTime;
                }
            }
            else if (targetObject != null && stalkObject)
            {
                newPosition = Vector3.Lerp(enemyBase.transform.position, targetObject.transform.position, 0.8f);
                navAgent.OverrideAgent(navAgent.DefaultSpeed / 2f, navAgent.DefaultAcceleration, 0.2f);
                enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed / 2f);
            }
            else
            {
                UpdateState(State.Investigate);
                return;
            }
            if (Vector3.Distance(newPosition, enemyBase.transform.position) > 0.5f)
            {
                navAgent.SetDestination(newPosition);
            }
            enemyBase.Vision.StandOverride(0.25f);
            stateTimer -= Time.deltaTime;
            if (stalkAttackCooldown > 0f)
            {
                stalkAttackCooldown -= Time.deltaTime;
            }
            if (stateTimer <= 0f || enemyBase.Rigidbody.touchingCartTimer > 0f || (stateTimer <= 5f && (targetPlayer != null && Vector3.Distance(enemyBase.transform.position, targetPlayer.transform.position) < 2.5f) || (targetObject != null && Vector3.Distance(enemyBase.transform.position, targetObject.transform.position) < 2.5f)))
            {
                UpdateState(State.Attack);
            }
        }
        public void StateAttack()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Attack\"");
                UpdateTarget(-1, -1);
                navAgent.ResetPath();
                navAgent.Warp(enemyBase.Rigidbody.transform.position);
                playersTumbled = new Dictionary<PlayerAvatar, bool>();
                stateTimer = 10f;
                List<PlayerAvatar> players = SemiFunc.PlayerGetAll();
                for (int i = 0; i < players.Count; i++)
                {
                    playersTumbled.Add(players[i], false);
                }
                SetImpulses(350);
            }
            stateTimer -= Time.deltaTime;
            enemyBase.Rigidbody.physGrabObject.OverrideMass(20f);
            enemyBase.Rigidbody.physGrabObject.OverrideDrag(5f);
            enemyBase.Rigidbody.physGrabObject.OverrideAngularDrag(10f);
            if (stateTimer <= 0f)
            {
                UpdateState(State.Leave);
            }
        }
        public void StateLeave()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Leave\"");
                UpdateTarget(-1, -1);
                stateTimer = UnityEngine.Random.Range(5f, 15f);
                LevelPoint newLevelPoint = SemiFunc.LevelPointGetFurthestFromPlayer(base.transform.position, 5f);
                if (NavMesh.SamplePosition(newLevelPoint.transform.position + UnityEngine.Random.insideUnitSphere * 3f, out var hit, 5f, -1) && Physics.Raycast(hit.position, Vector3.down, 5f, LayerMask.GetMask("Default")))
                {
                    newPosition = hit.position;
                }
                else
                {
                    log.LogDebug("Minesweeper: Trying to Leave Again");
                    stateImpulse = true;
                    return;
                }
                SetImpulses(373);
            }
            enemyBase.Vision.DisableVision(0.25f);
            navAgent.SetDestination(newPosition);
            navAgent.OverrideAgent(navAgent.DefaultSpeed * 2f, navAgent.DefaultAcceleration, 0.2f);
            enemyBase.Rigidbody.OverrideFollowPosition(1f, navAgent.DefaultSpeed * 2f);
            stateTimer -= Time.deltaTime;
            if (Vector3.Distance(enemyBase.transform.position, newPosition) <= 1f || stateTimer <= 0f)
            {
                UpdateState(State.Idle);
            }
        }
        public void StateStun()
        {
            if (stateImpulse)
            {
                stateImpulse = false;
                log.LogDebug("Minesweeper State: \"Stun\"");
                navAgent.ResetPath();
                navAgent.Warp(rigidbody.transform.position);
                SetImpulses(340);
            }
            else if (!enemyBase.IsStunned())
            {
                UpdateState(State.Idle);
            }
        }
        public void StateDespawn()
        {
            if (stateImpulse)
            {
                navAgent.ResetPath();
                navAgent.Warp(enemyBase.Rigidbody.transform.position);
                UpdateTarget(-1, -1);
                SetImpulses(340);
                stateImpulse = false;
                dead = true;
            }
        }
        public void OnSpawn()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.EnemySpawn(enemyBase))
            {
                SetImpulses(470);
                UpdateState(State.Spawn);
            }
        }
        public void OnInvestigate()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && (currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate))
            {
                log.LogDebug("Minesweeper: On Investigate");
                newPosition = enemyBase.StateInvestigate.onInvestigateTriggeredPosition;
                UpdateState(State.Investigate);
            }
        }
        public void OnVision()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && (currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate))
            {
                log.LogDebug("Minesweeper: Vision Triggered");
                int idPlayer = -1;
                int idObj = -1;
                if (SemiFunc.IsMultiplayer())
                {
                    if (enemyBase.Vision.onVisionTriggeredPlayer.physGrabber.grabbedPhysGrabObject != null && enemyBase.Vision.onVisionTriggeredPlayer.physGrabber.grabbedPhysGrabObject.playerGrabbing.Contains(enemyBase.Vision.onVisionTriggeredPlayer.physGrabber))
                    {
                        idObj = enemyBase.Vision.onVisionTriggeredPlayer.physGrabber.grabbedPhysGrabObject.photonView.ViewID;
                    }
                    idPlayer = enemyBase.Vision.onVisionTriggeredPlayer.photonView.ViewID;
                }
                if (enemyBase.Vision.onVisionTriggeredPlayer.physGrabber.grabbedPhysGrabObject != null && enemyBase.Vision.onVisionTriggeredPlayer.physGrabber.grabbedPhysGrabObject.playerGrabbing.Contains(enemyBase.Vision.onVisionTriggeredPlayer.physGrabber))
                {
                    UpdateTarget(idPlayer, idObj, enemyBase.Vision.onVisionTriggeredPlayer, enemyBase.Vision.onVisionTriggeredPlayer.physGrabber.grabbedPhysGrabObject);
                }
                else
                {
                    UpdateTarget(idPlayer, idObj, enemyBase.Vision.onVisionTriggeredPlayer, null);
                }
                UpdateState(State.Stalk);
            }
        }
        public void OnGrabbed()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && (currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate || currentState == State.Leave || currentState == State.Stalk))
            {
                log.LogDebug("Minesweeper: Grabbed");
                UpdateState(State.Attack);
            }
        }
        public void OnTouchPlayer()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && currentState != State.Attack)
            {
                log.LogDebug("Minesweeper: Touch Player");
                if (currentState == State.Stalk && stalkAttackCooldown <= 0f)
                {
                    UpdateState(State.Attack);
                    return;
                }
                int idPlayer = -1;
                int idObj = -1;
                if (SemiFunc.IsMultiplayer())
                {
                    idPlayer = enemyBase.Rigidbody.onTouchPlayerAvatar.photonView.ViewID;
                    if (enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber.grabbedPhysGrabObject != null && enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber.grabbedPhysGrabObject.playerGrabbing.Contains(enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber))
                    {
                        idObj = enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber.grabbedPhysGrabObject.photonView.ViewID;
                    }
                }
                if (enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber.grabbedPhysGrabObject != null && enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber.grabbedPhysGrabObject.playerGrabbing.Contains(enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber))
                {
                    UpdateTarget(idPlayer, idObj, enemyBase.Rigidbody.onTouchPlayerAvatar, enemyBase.Rigidbody.onTouchPlayerAvatar.physGrabber.grabbedPhysGrabObject);
                }
                else
                {
                    UpdateTarget(idPlayer, idObj, enemyBase.Rigidbody.onTouchPlayerAvatar, null);
                }
                UpdateState(State.Stalk);
            }
        }
        public void OnTouchPlayerPhys()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && (currentState == State.Idle || currentState == State.Roam || currentState == State.Investigate || currentState == State.Leave || currentState == State.Stalk))
            {
                log.LogDebug("Minesweeper: Touch Player Phys");
                if (currentState == State.Stalk && stalkAttackCooldown <= 0f)
                {
                    UpdateState(State.Attack);
                    return;
                }
                if (enemyBase.Rigidbody.onTouchPlayerGrabbedObjectPhysObject.GetComponent<PhysGrabHinge>() != null)
                {
                    return;
                }
                int idPlayer = -1;
                int idObj = -1;
                if (SemiFunc.IsMultiplayer())
                {
                    idPlayer = enemyBase.Rigidbody.onTouchPlayerGrabbedObjectPhysObject.playerGrabbing[0].playerAvatar.photonView.ViewID;
                    idObj = enemyBase.Rigidbody.onTouchPlayerGrabbedObjectPhysObject.photonView.ViewID;
                }
                UpdateTarget(idPlayer, idObj, enemyBase.Rigidbody.onTouchPlayerGrabbedObjectPhysObject.playerGrabbing[0].playerAvatar, enemyBase.Rigidbody.onTouchPlayerGrabbedObjectPhysObject);
                UpdateState(State.Stalk);
            }
        }
        public void OnTouchPhys()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && (currentState == State.Roam || currentState == State.Investigate || currentState == State.Stalk))
            {
                log.LogDebug("Minesweeper: Touch Phys");
                if (currentState == State.Stalk && enemyBase.Rigidbody.onTouchPhysObjectPhysObject == targetObject && stalkAttackCooldown <= 0f)
                {
                    UpdateState(State.Attack);
                    return;
                }
                if (enemyBase.Rigidbody.onTouchPhysObjectPhysObject.GetComponent<PhysGrabHinge>() != null)
                {
                    return;
                }
                int idObj = -1;
                if (SemiFunc.IsMultiplayer())
                {
                    idObj = enemyBase.Rigidbody.onTouchPhysObjectPhysObject.photonView.ViewID;
                }
                UpdateTarget(-1, idObj, null, enemyBase.Rigidbody.onTouchPhysObjectPhysObject);
                UpdateState(State.Stalk);
            }
        }
        public void OnHurt()
        {
            animator.hurtSound.Play(transform.position);
        }
        public void OnDeath()
        {
            log.LogDebug("Minesweeper: Death");
            visuals.localScale = Vector3.zero;
            GameDirector.instance.CameraShake.ShakeDistance(3f, 3f, 10f, base.transform.position, 0.5f);
            GameDirector.instance.CameraImpact.ShakeDistance(3f, 3f, 10f, base.transform.position, 0.05f);
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                enemyBase.EnemyParent.SpawnedTimerSet(0f);
                deathLocation = transform.position + (Vector3.up / 5f);
                containedAmount = objectContainer.Count;
                objectContainer.Shuffle();
                respawnItems = true;
                dead = true;
            }
        }
        public void RespawnSuckedItems()
        {
            if (respawnItems)
            {
                if (objectContainer.Count == 0)
                {
                    enemyBase.EnemyParent.Despawn();
                    respawnItems = false;
                    return;
                }
                respawnItemsTimer -= Time.deltaTime;
                if (respawnItemsTimer <= 0f)
                {
                    respawnItemsTimer = 5f / containedAmount;
                    GameObject suckedItem = objectContainer[0];
                    PhysGrabObject physGrabObject = suckedItem.GetComponent<PhysGrabObject>();
                    Vector3 newPos = ItemTeleportPosition(suckedItem);
                    if (SemiFunc.IsMultiplayer())
                    {
                        suckedItem.GetComponent<PhotonTransformView>().Teleport(newPos, suckedItem.transform.rotation);
                        photonView.RPC("SetActiveSuckedItemRPC", RpcTarget.All, true, suckedItem.GetComponent<PhotonView>().ViewID, newPos);
                    }
                    else
                    {
                        suckedItem.transform.position = newPos;
                        suckedItem.SetActive(true);
                        Instantiate(AssetManager.instance.prefabTeleportEffect, newPos, Quaternion.identity).transform.localScale = Vector3.one;
                        animator.respawnItemSound.Play(newPos);
                    }
                    physGrabObject.OverrideBreakEffects(0.5f);
                    physGrabObject.OverrideIndestructible(0.5f);
                    objectContainer.RemoveAt(0);
                }
            }
        }
        public Vector3 ItemTeleportPosition(GameObject item)
        {
            Vector3 pos = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                Vector3 newLocation = deathLocation + new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
                RaycastHit[] hits = Physics.RaycastAll(newLocation, deathLocation - newLocation, Mathf.Max(0f, Vector3.Distance(newLocation, deathLocation) - 0.2f), SemiFunc.LayerMaskGetVisionObstruct());
                bool hitSelf = false;
                for (int j = 0; j < hits.Length; j++)
                {
                    if (hits[j].transform != item.transform)
                    {
                        hitSelf = true;
                        break;
                    }
                }
                if (!hitSelf && Physics.OverlapBox(newLocation, item.GetComponent<PhysGrabObject>().boundingBox / 2f, item.transform.rotation, SemiFunc.LayerMaskGetVisionObstruct()).Length == 0)
                {
                    pos = newLocation;
                    break;
                }
            }
            if (pos == Vector3.zero)
            {
                pos = deathLocation;
            }
            return pos;
        }
        public void OnPhysVortex()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer())
            {
                for (int i = 0; i < hurtCollider.hits.Count; i++)
                {
                    if (hurtCollider.hits[i].hitType != HurtCollider.HitType.PhysObject || !hurtCollider.hits[i].hitObject.activeSelf)
                    {
                        continue;
                    }
                    if (hurtCollider.hits[i].hitObject.GetComponent<PhysGrabHinge>() != null)
                    {
                        continue;
                    }
                    GameObject suckedItem = hurtCollider.hits[i].hitObject;
                    objectContainer.Add(suckedItem);
                    Vector3 originalPos = suckedItem.transform.position;
                    if (SemiFunc.IsMultiplayer())
                    {
                        suckedItem.GetComponent<PhotonTransformView>().Teleport(new Vector3(0, 1000f, 0), suckedItem.transform.rotation);
                        photonView.RPC("SetActiveSuckedItemRPC", RpcTarget.All, false, suckedItem.GetComponent<PhotonView>().ViewID, originalPos);
                    }
                    else
                    {
                        suckedItem.transform.position = new Vector3(0, 1000f, 0);
                        suckedItem.SetActive(false);
                        Instantiate(AssetManager.instance.prefabTeleportEffect, originalPos, Quaternion.identity).transform.localScale = Vector3.one;
                    }
                    stateTimer -= 0.5f;
                }
            }
        }
        public void VisionCheck()
        {
            visionTimer -= Time.deltaTime;
            if (visionTimer <= 0)
            {
                visionTimer = 0.1f;
                if (targetPlayer != null)
                {
                    Vector3 playerDirection = targetPlayer.PlayerVisionTarget.VisionTransform.position - animator.eyeTransform.position;
                    if (Physics.Raycast(animator.eyeTransform.position, playerDirection, playerDirection.magnitude, LayerMask.GetMask("Default")))
                    {
                        playerVisionTimer -= 0.1f;
                        if (playerVisionTimer <= 0f)
                        {
                            if (targetObject != null)
                            {
                                UpdateTarget(-1, targetObject.photonView.ViewID, null, targetObject);
                            }
                            else
                            {
                                UpdateTarget(-1, -1);
                            }
                        }
                    }
                    else if (playerVisionTimer != 1f)
                    {
                        playerVisionTimer = 1f;
                    }   
                }
                if (targetObject != null)
                {
                    Vector3 objectDirection = targetObject.transform.position - animator.eyeTransform.position;
                    if (Physics.Raycast(animator.eyeTransform.position, objectDirection, objectDirection.magnitude, LayerMask.GetMask("Default")))
                    {
                        objectVisionTimer -= 0.1f;
                        if (objectVisionTimer <= 0f)
                        {
                            if (targetPlayer != null)
                            {
                                UpdateTarget(targetPlayer.photonView.ViewID, -1, targetPlayer);
                            }
                            else
                            {
                                UpdateTarget(-1, -1);
                            }
                        }
                    }
                    else if (objectVisionTimer != 1f)
                    {
                        objectVisionTimer = 1f;
                    }
                }
                if (targetObject != null && !targetObject.grabbed)
                {
                    stalkObject = true;
                }
                else
                {
                    stalkObject = false;
                }
            }
        }
        [PunRPC]
        public void SetActiveSuckedItemRPC(bool active, int id, Vector3 pos)
        {
            PhotonView.Find(id).gameObject.SetActive(active);
            Instantiate(AssetManager.instance.prefabTeleportEffect, pos, Quaternion.identity).transform.localScale = Vector3.one;
            if (active)
            {
                animator.respawnItemSound.Play(pos);
            }
        }
        public void UpdateTarget(int id, int idObj, PlayerAvatar player = null, PhysGrabObject physGrabObject = null)
        {
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("UpdateTargetRPC", RpcTarget.All, id, idObj, null, null);
            }
            else
            {
                UpdateTargetRPC(-1, -1, player, physGrabObject);
            }
        }
        [PunRPC]
        public void UpdateTargetRPC(int id, int idObj, PlayerAvatar player = null, PhysGrabObject physGrabObject = null)
        {
            PlayerAvatar newPlayer;
            PhysGrabObject newObject;
            if (SemiFunc.IsMultiplayer())
            {
                newPlayer = SemiFunc.PlayerAvatarGetFromPhotonID(id);
                if (idObj != -1)
                {
                    newObject = PhotonView.Find(idObj).GetComponent<PhysGrabObject>();
                }
                else
                {
                    newObject = null;
                }
                
            }
            else
            {
                newPlayer = player;
                newObject = physGrabObject;
            }
            if (newObject != null && newObject.GetComponent<PhysGrabHinge>() != null)
            {
                newObject = targetObject;
            }
            if (newPlayer == null)
            {
                log.LogDebug("Minesweeper: New target player is \"null\"!");
            }
            else
            {
                log.LogDebug($"Minesweeper: New target player is \"{newPlayer.playerName}\"!");
            }
            if (newObject == null)
            {
                log.LogDebug("Minesweeper: New target object is \"null\"!");
            }
            else
            {
                log.LogDebug($"Minesweeper: New target object is \"{newObject.name}\"!");
            }
            if (targetPlayer != newPlayer && newPlayer != null)
            {
                animator.noticeSound.Play(transform.position);
            }
            else if (targetObject != newObject && newObject != null)
            {
                animator.interestSound.Play(transform.position);
            }
            targetPlayer = newPlayer;
            targetObject = newObject;
        }
        public void SetImpulses(int integer = 0)
        {
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("SetImpulsesRPC", RpcTarget.All, integer);
            }
            else
            {
                SetImpulsesRPC(integer);
            }
        }
        [PunRPC]
        public void SetImpulsesRPC(int integer = 0)
        {
            string binary = Convert.ToString(integer, 2);
            while (binary.Length < 9)
            {
                binary = $"0{binary}";
            }
            bool[] impulse = binary.Select(x => x == '1').ToArray();
            if (impulse[0])
            {
                if (impulse[1])
                {
                    animator.stalkEmergeImpulse = true;
                }
                else
                {
                    animator.stalkDisappearImpulse = true;
                }
            }
            if (impulse[2])
            {
                if (impulse[3])
                {
                    animator.bounceStartImpulse = true;
                }
                else
                {
                    animator.bounceStopImpulse = true;
                }
            }
            if (impulse[4])
            {
                if (impulse[5])
                {
                    animator.vortexEmergeImpulse = true;
                }
                else
                {
                    animator.vortexLeaveImpulse = true;
                }
            }
            if (impulse[6])
            {
                if (impulse[7])
                {
                    animator.lidOpenImpulse = true;
                }
                else
                {
                    animator.lidCloseImpulse = true;
                }
            }
            if (impulse[8])
            {
                animator.fireImpulse = true;
            }
        }
    }
}