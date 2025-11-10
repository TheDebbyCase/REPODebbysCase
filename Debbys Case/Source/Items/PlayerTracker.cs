using Photon.Pun;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace REPODebbysCase.Items
{
    public class PlayerTracker : MonoBehaviour
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public PhotonView photonView;
        public PhysGrabObject physGrabObject;
        public int trackerID;
        public ItemToggle itemToggle;
        public ItemEquippable itemEquippable;
        public TextMeshPro trackingNumberText;
        public TextMeshPro idNumberText;
        public Transform rotatorTransform;
        public float rotatorTargetRot;
        public Transform pegTransform;
        public float pegTargetPos;
        public Sound trackingLoop;
        public Sound clickSound;
        public bool movingNext = false;
        public float nextTimer = 0f;
        public Vector3 forceRotation = new Vector3(110f, 0f, 180f);
        public PlayerTracker trackingPlayer;
        public int lastID = 0;
        public PlayerAvatar equippingPlayer;
        public void Awake()
        {
            if (SemiFunc.IsMasterClientOrSingleplayer() && !SemiFunc.RunIsLevel())
            {
                PhotonNetwork.Destroy(gameObject);
                return;
            }
            MotherTracker.activeTrackers.Add(this);
            itemEquippable = GetComponent<ItemEquippable>();
            trackingNumberText.text = "X";
        }
        public void FixedUpdate()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !LevelGenerator.Instance.Generated)
            {
                return;
            }
            if (physGrabObject.grabbed)
            {
                int nonRotatingGrabbers = physGrabObject.playerGrabbing.Count;
                for (int i = 0; i < physGrabObject.playerGrabbing.Count; i++)
                {
                    if (physGrabObject.playerGrabbing[i].isRotating)
                    {
                        nonRotatingGrabbers--;
                    }
                }
                if (nonRotatingGrabbers == physGrabObject.playerGrabbing.Count)
                {
                    physGrabObject.TurnXYZ(Quaternion.Euler(forceRotation.x, 0f, 0f), Quaternion.Euler(0f, forceRotation.y, 0f), Quaternion.Euler(0f, 0f, forceRotation.z));
                }
            }
        }
        public void Initialize()
        {
            List<int> currentNumbers = new List<int>();
            for (int i = 0; i < MotherTracker.activeTrackers.Count; i++)
            {
                currentNumbers.Add(MotherTracker.activeTrackers[i].trackerID);
            }
            int setNumber = 1;
            int loopCount = 0;
            while (currentNumbers.Contains(setNumber))
            {
                setNumber++;
                loopCount++;
                if (loopCount > currentNumbers.Count)
                {
                    setNumber = 1;
                    break;
                }
            }
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("InitializeRPC", RpcTarget.All, setNumber);
            }
            else
            {
                InitializeRPC(setNumber);
            }
        }
        [PunRPC]
        public void InitializeRPC(int newID)
        {
            trackerID = newID;
            idNumberText.text = trackerID.ToString();
            log.LogDebug($"Spawned new Player Tracker with id: \"{trackerID}\"");
        }
        public void Update()
        {
            if (!LevelGenerator.Instance.Generated)
            {
                return;
            }
            if (movingNext)
            {
                nextTimer -= Time.deltaTime * 2f;
                rotatorTransform.localRotation = Quaternion.Lerp(Quaternion.identity, Quaternion.Euler(0f, 0f, rotatorTargetRot), nextTimer);
                pegTransform.localPosition = Vector3.Lerp(Vector3.zero, new Vector3(pegTargetPos, 0f, 0f), nextTimer);
            }
            else if (trackingPlayer != null)
            {
                Vector3 target;
                if (trackingPlayer.itemEquippable.isEquipped)
                {
                    if (equippingPlayer == null)
                    {
                        PlayerAvatar player = null;
                        if (SemiFunc.IsMultiplayer())
                        {
                            PhotonView equipperPhotonView = PhotonView.Find(trackingPlayer.itemEquippable.ownerPlayerId);
                            if (equipperPhotonView != null)
                            {
                                player = equipperPhotonView.GetComponent<PlayerAvatar>();
                                if (player == null)
                                {
                                    player = equipperPhotonView.GetComponent<PhysGrabber>().playerAvatar;
                                }
                            }
                        }
                        else
                        {
                            player = PlayerAvatar.instance;
                        }
                        equippingPlayer = player;
                    }
                    target = rotatorTransform.parent.InverseTransformPoint(equippingPlayer.transform.position);
                }
                else
                {
                    if (equippingPlayer != null)
                    {
                        equippingPlayer = null;
                    }
                    target = rotatorTransform.parent.InverseTransformPoint(trackingPlayer.rotatorTransform.position);
                }
                rotatorTargetRot = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
                rotatorTransform.localRotation = Quaternion.Euler(0f, 0f, rotatorTargetRot);
                pegTargetPos = Mathf.InverseLerp(5f, 25f, Vector3.Distance(transform.position, rotatorTransform.parent.TransformPoint(target))) * 0.8f;
                pegTransform.localPosition = new Vector3(pegTargetPos, 0f, 0f);
            }
            trackingLoop.PlayLoop(trackingPlayer != null && physGrabObject.grabbed, 1f, 2f, Mathf.Lerp(0.5f, 4f, 0.8f - pegTargetPos));
            if (physGrabObject.grabbedLocal)
            {
                PhysGrabber.instance.OverrideGrabDistance(0.8f);
            }
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            if (!movingNext)
            {
                if (itemToggle.toggleState)
                {
                    NextTracker(true);
                }
            }
            else if (nextTimer <= 0f)
            {
                itemToggle.ToggleItem(false);
                NextTracker(false);
            }
        }
        public void NextTracker(bool goingNext)
        {
            int next = 0;
            if (!goingNext)
            {
                List<int> currentTrackers = new List<int>();
                for (int i = 0; i < MotherTracker.activeTrackers.Count; i++)
                {
                    currentTrackers.Add(MotherTracker.activeTrackers[i].trackerID);
                }
                next = lastID + 1;
                if (next > Mathf.Max(currentTrackers.ToArray()))
                {
                    next = 1;
                }
                if (next == trackerID)
                {
                    next++;
                }
                int loopCount = 0;
                while (!currentTrackers.Contains(next))
                {
                    next++;
                    if (loopCount > currentTrackers.Count)
                    {
                        next = lastID;
                        break;
                    }
                }
            }
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("NextTrackerRPC", RpcTarget.All, next, goingNext);
            }
            else
            {
                NextTrackerRPC(next, goingNext);
            }
        }
        [PunRPC]
        public void NextTrackerRPC(int id, bool goingNext)
        {
            if (goingNext)
            {
                nextTimer = 1f;
                trackingPlayer = null;
                trackingNumberText.text = "X";
            }
            else
            {
                lastID = id;
                trackingPlayer = MotherTracker.activeTrackers.Find((x) => x.trackerID == id);
                trackingNumberText.text = id.ToString();
                log.LogDebug($"Player Tracker with id: \"{trackerID}\" began tracking Player Tracker with id: \"{trackingPlayer.trackerID}\"");
                clickSound.Play(transform.position);
            }
            movingNext = goingNext;
        }
        public void OnDestroy()
        {
            MotherTracker.activeTrackers.Remove(this);
        }
    }
    public class MotherTracker : MonoBehaviour
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public static List<PlayerTracker> activeTrackers = new List<PlayerTracker>();
        public PhotonView photonView;
        public PhysGrabObject physGrabObject;
        public ItemAttributes itemAttributes;
        public ItemEquippable itemEquippable;
        public ItemToggle itemToggle;
        public Item trackerItem;
        public GameObject[] toDisable;
        public Transform[] toSpawnPositions;
        public Transform rayStart;
        public Sound useSound;
        public Sound floatLoop;
        public float balanceForce = 4f;
        public float floatPower = 5f;
        public float floatHeight = 0.5f;
        public float glidePower = 0.5f;
        public Animator animator;
        public ParticleSystem flameParticles;
        public Vector3 randomTorque;
        public float torqueTimer = 0f;
        public float torqueMultiplier = 1f;
        public float floatTimer = 0f;
        public bool released = false;
        public void FixedUpdate()
        {
            if (!LevelGenerator.Instance.Generated || released)
            {
                return;
            }
            Vector3 localTiltDir = transform.InverseTransformDirection(transform.up - Vector3.up);
            animator.SetFloat("Flap One Normal", Mathf.InverseLerp(-0.25f, 0.75f, localTiltDir.z));
            animator.SetFloat("Flap Two Normal", Mathf.InverseLerp(-0.25f, 0.75f, localTiltDir.x));
            animator.SetFloat("Flap Three Normal", Mathf.InverseLerp(0.25f, -0.75f, -localTiltDir.z));
            animator.SetFloat("Flap Four Normal", Mathf.InverseLerp(0.25f, -0.75f, -localTiltDir.x));
            if (!SemiFunc.IsMasterClientOrSingleplayer() || physGrabObject.grabbed)
            {
                return;
            }
            Quaternion rotator = Quaternion.FromToRotation(transform.up, Vector3.up);
            physGrabObject.rb.AddTorque(new Vector3(rotator.x, rotator.y, rotator.z) * balanceForce);
            if (Physics.Raycast(rayStart.position, -Vector3.up, out RaycastHit hit, floatHeight, LayerMask.GetMask("Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "Enemy", "Player"), QueryTriggerInteraction.Ignore) && !physGrabObject.colliders.Contains(hit.collider.transform))
            {
                physGrabObject.rb.AddForce(transform.up * (floatPower / hit.distance) * (1.1f - (Quaternion.Angle(Quaternion.identity, rotator) / 360f)));
            }
            else
            {
                physGrabObject.rb.AddForce(transform.up * floatPower * glidePower * (1.1f - (Quaternion.Angle(Quaternion.identity, rotator) / 360f)));
            }
            if (floatTimer > 0f)
            {
                floatTimer -= Time.deltaTime;
            }
            else if (torqueTimer > 0f)
            {
                torqueTimer -= Time.deltaTime;
                physGrabObject.rb.AddTorque(randomTorque * torqueMultiplier);
            }
            else if (Random.Range(0, 2) == 0)
            {
                floatTimer = Random.Range(2.5f, 7.5f);
            }
            else
            {
                torqueTimer = Random.Range(1f, 5f);
                randomTorque = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f));
            }
        }
        public void Update()
        {
            floatLoop.PlayLoop(!released, 1f, 1f, Mathf.Min(1.5f, Mathf.Max(1f, physGrabObject.rbVelocity.magnitude - 1f)));
            if (released)
            {
                return;
            }
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.RunIsLevel())
            {
                return;
            }
            if (itemToggle.toggleState)
            {
                itemToggle.ToggleItem(false);
                for (int i = 0; i < toSpawnPositions.Length; i++)
                {
                    REPOLib.Modules.Items.SpawnItem(trackerItem, toSpawnPositions[i].position, toSpawnPositions[i].rotation);
                }
                for (int i = 0; i < activeTrackers.Count; i++)
                {
                    activeTrackers[i].Initialize();
                }
                Release();
            }
        }
        public void Release()
        {
            physGrabObject.impactDetector.indestructibleBreakEffects = true;
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("ReleaseRPC", RpcTarget.All);
            }
            else
            {
                ReleaseRPC();
            }
        }
        [PunRPC]
        public void ReleaseRPC()
        {
            log.LogDebug("Releasing Player Trackers");
            itemToggle.ToggleDisable(true);
            itemAttributes.DisableUI(true);
            Destroy(itemEquippable);
            for (int i = 0; i < toDisable.Length; i++)
            {
                toDisable[i].SetActive(false);
            }
            flameParticles.Stop();
            animator.SetBool("Disabled", true);
            useSound.Play(transform.position);
            released = true;
        }
    }
}