using UnityEngine;
namespace REPODebbysCase.Enemies
{
    public class EnemyMinesweeperAnim : MonoBehaviour
    {
        readonly BepInEx.Logging.ManualLogSource log = DebbysCase.instance.log;
        public EnemyMinesweeper minesweeperMain;
        public SpringQuaternion mainSpring;
        public Quaternion mainRotation;
        public SpringQuaternion stalkSpring;
        public Transform eyeTransform;
        public Animator animator;
        public ParticleSystem[] vortexParticles;
        public ParticleSystem[] smokeParticles;
        public ParticleSystem[] fireParticles;
        public Sound fireBurstSound;
        public Sound engineSoundLoop;
        public Sound vortexSoundLoop;
        public Sound laughSoundLoop;
        public Sound stunSoundLoop;
        public Sound mumbleSoundLoop;
        public Sound noticeSound;
        public Sound interestSound;
        public Sound hurtSound;
        public Sound respawnItemSound;
        public Sound squelchSound;
        public bool fireImpulse;
        public float fireTimer;
        public bool boostOn;
        public Light[] fireLights;
        public float randomPeerTimer = Random.Range(1f, 4f);
        public bool stalkEmergeImpulse;
        public bool stalkDisappearImpulse;
        public bool bounceStartImpulse;
        public bool bounceStopImpulse;
        public bool vortexEmergeImpulse;
        public bool vortexLeaveImpulse;
        public bool lidOpenImpulse;
        public bool lidCloseImpulse;
        public void Awake()
        {
            for (int i = 0; i < fireLights.Length; i++)
            {
                fireLights[i].gameObject.SetActive(true);
                fireLights[i].enabled = false;
            }
        }
        public void Update()
        {
            if (minesweeperMain.currentState == EnemyMinesweeper.State.Stun || minesweeperMain.currentState == EnemyMinesweeper.State.Attack)
            {
                if (animator.GetFloat("Override Speed") != 3f)
                {
                    log.LogDebug("Minesweeper: Animation Override Speed: \"3f\"");
                    animator.SetFloat("Override Speed", 3f);
                }
            }
            else if (animator.GetFloat("Override Speed") != 1f)
            {
                log.LogDebug("Minesweeper: Animation Override Speed: \"1f\"");
                animator.SetFloat("Override Speed", 1f);
            }
            RandomPeeps();
            CalculateTrackSpeeds();
            CalculateRotators();
            if (stalkEmergeImpulse)
            {
                stalkEmergeImpulse = false;
                animator.SetBool("Stalk Emerge", true);
                squelchSound.Play(eyeTransform.position);
            }
            if (stalkDisappearImpulse)
            {
                stalkDisappearImpulse = false;
                animator.SetBool("Stalk Emerge", false);
                squelchSound.Play(eyeTransform.position);
            }
            if (bounceStartImpulse)
            {
                bounceStartImpulse = false;
                animator.SetBool("Engine Bounce", true);
                for (int i = 0; i < smokeParticles.Length; i++)
                {
                    smokeParticles[i].Play();
                }
            }
            if (bounceStopImpulse)
            {
                bounceStopImpulse = false;
                animator.SetBool("Engine Bounce", false);
                for (int i = 0; i < smokeParticles.Length; i++)
                {
                    smokeParticles[i].Stop();
                }
            }
            if (vortexEmergeImpulse)
            {
                vortexEmergeImpulse = false;
                animator.SetBool("Vortex", true);
                for (int i = 0; i < vortexParticles.Length; i++)
                {
                    vortexParticles[i].Play();
                }
            }
            if (vortexLeaveImpulse)
            {
                vortexLeaveImpulse = false;
                animator.SetBool("Vortex", false);
                for (int i = 0; i < vortexParticles.Length; i++)
                {
                    vortexParticles[i].Stop();
                }
            }
            if (lidOpenImpulse)
            {
                lidOpenImpulse = false;
                animator.SetBool("Lid Open", true);
            }
            if (lidCloseImpulse)
            {
                lidCloseImpulse = false;
                animator.SetBool("Lid Open", false);
            }
            if (fireImpulse)
            {
                log.LogDebug("Minesweeper: Start Fire");
                fireImpulse = false;
                fireTimer = Random.Range(0.2f, 0.6f);
                boostOn = true;
                for (int i = 0; i < fireParticles.Length; i++)
                {
                    fireParticles[i].Play();
                }
                for (int i = 0; i < fireLights.Length; i++)
                {
                    fireLights[i].intensity = 2.5f;
                    fireBurstSound.Play(fireLights[i].transform.position);
                }
            }
            if (fireTimer >= 0f)
            {
                fireTimer -= Time.deltaTime;
            }
            else if (boostOn)
            {
                boostOn = false;
                log.LogDebug("Minesweeper: Stop Fire");
                for (int i = 0; i < fireParticles.Length; i++)
                {
                    fireParticles[i].Stop();
                }
                for (int i = 0; i < fireLights.Length; i++)
                {
                    fireLights[i].intensity = 0f;
                }
            }
            engineSoundLoop.PlayLoop(animator.GetBool("Engine Bounce"), 1f, 1f);
            vortexSoundLoop.PlayLoop(animator.GetBool("Vortex"), 1f, 0.5f);
            laughSoundLoop.PlayLoop(animator.GetBool("Vortex"), 0.5f, 2f);
            stunSoundLoop.PlayLoop(minesweeperMain.currentState == EnemyMinesweeper.State.Stun, 2f, 2f);
            mumbleSoundLoop.PlayLoop(!animator.GetBool("Stalk Emerge") && !animator.GetBool("Vortex"), 1f, 1f);
        }
        public void RandomPeeps()
        {
            if (!animator.GetBool("Stalk Emerge"))
            {
                if (randomPeerTimer > 0f)
                {
                    randomPeerTimer -= Time.deltaTime;
                }
                else
                {
                    int random = Random.Range(0, 4);
                    animator.SetInteger("Random Peer", random);
                    animator.SetTrigger("Peer");
                    log.LogDebug($"Minesweeper: Peering \"{random}\"");
                    randomPeerTimer = Random.Range(1f, 4f);
                    squelchSound.Play(eyeTransform.position);
                }
            }
        }
        public void CalculateTrackSpeeds()
        {
            float rightAmount = (Mathf.InverseLerp(-5f, 5f, minesweeperMain.rigidbody.angularVelocity.y) * 2f) - 1f;
            if (Mathf.Abs(rightAmount) > 0.1f)
            {
                animator.SetFloat("Right Track Speed", rightAmount * -1f);
                animator.SetFloat("Left Track Speed", rightAmount);
            }
            else if (minesweeperMain.rigidbody.velocity.magnitude >= 0.1f)
            {
                animator.SetFloat("Right Track Speed", 0.5f);
                animator.SetFloat("Left Track Speed", 0.5f);
            }
            else
            {
                animator.SetFloat("Right Track Speed", 0f);
                animator.SetFloat("Left Track Speed", 0f);
            }
        }
        public void CalculateRotators()
        {
            if (minesweeperMain.currentState != EnemyMinesweeper.State.Attack && minesweeperMain.navAgent.AgentVelocity.normalized.magnitude > 0.1f)
            {
                mainRotation = Quaternion.LookRotation(minesweeperMain.navAgent.AgentVelocity.normalized);
                mainRotation.eulerAngles = new Vector3(0f, mainRotation.eulerAngles.y, 0f);
            }
            minesweeperMain.transform.rotation = SemiFunc.SpringQuaternionGet(mainSpring, mainRotation);
            if (eyeTransform.gameObject.activeSelf)
            {
                Quaternion eyeLookDir;
                if (minesweeperMain.targetPlayer != null)
                {
                    eyeLookDir = Quaternion.LookRotation(minesweeperMain.targetPlayer.PlayerVisionTarget.VisionTransform.transform.position - eyeTransform.position);
                }
                else if (minesweeperMain.targetObject != null)
                {
                    eyeLookDir = Quaternion.LookRotation(minesweeperMain.targetObject.transform.position - eyeTransform.position);
                }
                else
                {
                    eyeLookDir = Quaternion.identity;
                }
                Quaternion targetRot = SemiFunc.SpringQuaternionGet(stalkSpring, eyeLookDir);
                eyeTransform.rotation = targetRot;
            }
            else if (eyeTransform.localRotation != Quaternion.identity)
            {
                eyeTransform.localRotation = Quaternion.identity;
            }
        }
    }
}