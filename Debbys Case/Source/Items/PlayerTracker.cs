using Photon.Pun;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace REPODebbysCase.Items
{
    public class PlayerTracker : MonoBehaviour
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public static List<PlayerTracker> currentTrackers;
        public PhotonView photonView;
        public int tracker;
        public PhysGrabObject physGrabObject;
        public ItemEquippable itemEquippable;
        public ItemToggle itemToggle;
        public TextMeshPro trackingNumber;
        public Transform trackerTransform;
        public bool movingNext = false;
        public float nextTimer = 0f;
        public int currentPlayer = 0;
        public PlayerTracker trackingPlayer;
        public Vector2 localPos = Vector2.zero;
        public Vector3 trackerVelocity = Vector3.zero;
        public void Update()
        {
            if (!LevelGenerator.Instance.Generated)
            {
                return;
            }
            if (movingNext)
            {
                localPos = Vector2.Lerp(Vector2.zero, localPos, nextTimer);
                nextTimer -= Time.deltaTime * 2f;
            }
            else if (trackingPlayer != null)
            {
                Vector3 direction = trackerTransform.InverseTransformDirection((trackerTransform.position - trackingPlayer.transform.position).normalized);
                localPos = new Vector2(direction.x, direction.y);
            }
            trackerTransform.localPosition = Vector3.SmoothDamp(trackerTransform.localPosition, new Vector3(localPos.x, 0f, localPos.y), ref trackerVelocity, 0.5f);
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            if (itemToggle.toggleState)
            {
                if (!movingNext)
                {
                    SetGoingNext(true);
                    SelectTracker();
                }
                else if (nextTimer <= 0f)
                {
                    itemToggle.ToggleItem(false);
                    SetGoingNext(false);
                    SelectTracker(NextValue(currentPlayer));
                }
            }
        }
        public void SetGoingNext(bool goingNext)
        {
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("SetGoingNextRPC", RpcTarget.All, goingNext);
            }
            else
            {
                SetGoingNextRPC(goingNext);
            }
        }
        [PunRPC]
        public void SetGoingNextRPC(bool goingNext)
        {
            if (goingNext)
            {
                nextTimer = 1f;
            }
            movingNext = goingNext;
        }
        public void SelectTracker(int number = -1)
        {
            if (SemiFunc.IsMultiplayer())
            {
                photonView.RPC("SelectTrackerRPC", RpcTarget.All, number);
            }
            else
            {
                SelectTrackerRPC(number);
            }
        }
        [PunRPC]
        public void SelectTrackerRPC(int number)
        {
            string newNumber = string.Empty;
            currentPlayer = number;
            trackingPlayer = null;
            if (number != -1)
            {
                newNumber = number.ToString();
                trackingPlayer = currentTrackers.Find((x) => x.tracker == currentPlayer);
            }
            trackingNumber.text = newNumber;
        }
        public int NextValue(int number)
        {
            int newNumber = number + 1;
            if (newNumber > currentTrackers.Count)
            {
                newNumber = 1;
            }
            return newNumber;
        }
        public void OnDestroy()
        {
            currentTrackers.Remove(this);
        }
    }
    public class MotherTracker : MonoBehaviour
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public PhotonView photonView;
        public PhysGrabObject physGrabObject;
        public ItemAttributes itemAttributes;
        public ItemEquippable itemEquippable;
        public ItemToggle itemToggle;
        public GameObject[] toDisable;
        public GameObject[] toEnable;
        public Sound useSound;
        public Sound floatLoop;
        public float balanceForce = 4f;
        public float floatPower = 5f;
        public float floatHeight = 0.5f;
        public float glidePower = 0.5f;
        public bool released = false;
        public void FixedUpdate()
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer() || !LevelGenerator.Instance.Generated || released || physGrabObject.grabbed)
            {
                return;
            }
            Quaternion rotator = Quaternion.FromToRotation(transform.up, Vector3.up);
            physGrabObject.rb.AddTorque(new Vector3(rotator.x, rotator.y, rotator.z) * balanceForce);
            if (Physics.Raycast(physGrabObject.rb.worldCenterOfMass, -Vector3.up, out RaycastHit hit, floatHeight, LayerMask.GetMask("Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "Enemy", "Player"), QueryTriggerInteraction.Ignore) && !physGrabObject.colliders.Contains(hit.collider.transform))
            {
                physGrabObject.rb.AddForce(transform.up * (floatPower / hit.distance) * (1.1f - (Quaternion.Angle(Quaternion.identity, rotator) / 360f)));
            }
            else
            {
                physGrabObject.rb.AddForce(transform.up * floatPower * glidePower * (1.1f - (Quaternion.Angle(Quaternion.identity, rotator) / 360f)));
            }
        }
        public void Update()
        {
            if (!LevelGenerator.Instance.Generated || released)
            {
                return;
            }
            floatLoop.PlayLoop(!physGrabObject.grabbed, 1f, 1f);
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }
            if (itemToggle.toggleState)
            {
                physGrabObject.impactDetector.indestructibleBreakEffects = true;
                Release();
            }
        }
        public void Release()
        {
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
            itemToggle.ToggleDisable(true);
            itemAttributes.DisableUI(true);
            Destroy(itemEquippable);
            for (int i = 0; i < toDisable.Length; i++)
            {
                toDisable[i].SetActive(false);
            }
            for (int i = 0; i < toEnable.Length; i++)
            {
                PlayerTracker playerTracker = toEnable[i].GetComponent<PlayerTracker>();
                PlayerTracker.currentTrackers.Add(playerTracker);
                toEnable[i].SetActive(true);
                int trackingPlayer = i + 1;
                playerTracker.tracker = trackingPlayer;
                trackingPlayer++;
                if (trackingPlayer > toEnable.Length)
                {
                    trackingPlayer = 1;
                }
                playerTracker.currentPlayer = trackingPlayer;
                useSound.Play(toEnable[i].transform.position);
            }
            released = true;
        }
    }
}