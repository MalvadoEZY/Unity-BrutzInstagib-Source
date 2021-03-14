using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace com.brutz.Player
{
    public class player_controller : MonoBehaviour
    {

        [SerializeField] GameObject playerCamera;
        [SerializeField] Transform weapon;

        [SerializeField] AudioSource footstepAudio;
        [SerializeField] List<AudioClip> audioClips;

        private CharacterController playerController;

        public float sensivity = 1f;

        private float jumpForce = -.7f; //0f - 1f
        private float wallPushForce = -5f;

        private float playerRunningSpeed = 20f;
        private float gravity = -23f;
        float cameraPitch = 0f;

        private float smoothTime = .3f;
        private Vector2 currentDir = Vector2.zero;
        private Vector2 currentDirVelocity = Vector2.zero;

        Vector3 move;
        //Velocity of the indentity
        Vector3 velocity;


        public Transform groundCheck;
        public Transform wallCheck;

        //Distance triggers
        private float groundDistance = .3f;
        private float minWallDistance = 0.1f;
        private float maxWallDistance = 4f;

        private RaycastHit wallDetector;

        private bool isGrounded;
        private bool isOnWall;
        
        private string currentWall;
        private string previousWall;

        private int availableJumps = 2;

        private float maxAngle = 75.0f;
        private float minAngle = -75f;

        private bool allowMovementUpdate = true;
        private bool allowCameraUpdate = true;

        private bool isFalling;
        

        // Start is called before the first frame update
        void Start()
        {
            //Assign variables
            playerController = gameObject.GetComponent<CharacterController>();

            //Set as main player
            if (photonView.IsMine)
            {
                playerCamera.SetActive(true);
            }

            if (Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (photonView.IsMine)
            {
                //Will play the slinding sound
                CameraMovement();
                PlayerMovement();
                PlayerKeyBinds();
            }
        }

        private void PlayerKeyBinds()
        {
            if (!allowMovementUpdate) return;

            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = jumpForce * gravity;
                JumpEffect();
                availableJumps -= 1;
            }
            
            if (availableJumps >= 1 && !isGrounded)
            {
                if (Input.GetButtonDown("Jump"))
                {
                   
                    if (isOnWall)
                    {
                        //Doesn't allow player jump on the same wall of before
                        if (currentWall == previousWall) return;
                        JumpEffect();
                        previousWall = currentWall;

                        //Reflection 
                        Vector3 reflection = Vector3.Reflect(move, wallDetector.normal);

                        velocity = reflection * 20f;
                        velocity.y -= wallPushForce * 2;

                    } else
                    {
                        //If tries to jump in the fucking air
                        availableJumps = 0;
                    }
                }
                
            }
        }

        private void JumpEffect()
        {
            System.Random randomClip = new System.Random();
            int index = randomClip.Next(audioClips.Count);
            footstepAudio.volume = .3f;
            footstepAudio.PlayOneShot(audioClips[index]);
        }

        private void CameraMovement()
        {
            if (!allowCameraUpdate) return;

            Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            cameraPitch -= mouseDelta.y;

            //Locks the player camera in certain pitch
            cameraPitch = Mathf.Clamp(cameraPitch, minAngle, maxAngle);
            playerCamera.transform.localEulerAngles = Vector3.right * cameraPitch;
            transform.Rotate(Vector3.up * mouseDelta.x);
            
            //Rotate the weapon

            weapon.transform.localEulerAngles = Vector3.right * cameraPitch;
        }

        private void PlayerMovement()
        {
            checkGround();
            checkWall();

            Vector2 targetDir = Vector2.zero;

            if (allowMovementUpdate)
            {
                targetDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                targetDir.Normalize();
            }

            if (isGrounded && velocity.y < 0)
            {
                availableJumps = 2;
                //Resets the reflects
                velocity = Vector3.zero;
                velocity.y = -2f;
                currentDir = Vector2.SmoothDamp(currentDir, targetDir, ref currentDirVelocity, .2f);

            }
            else
            {
                //mid air smooth    
                currentDir = Vector2.SmoothDamp(currentDir, targetDir, ref currentDirVelocity, smoothTime);
            }

            //Move Player
            move = (transform.forward * currentDir.y + transform.right * currentDir.x);
            playerController.Move(move * playerRunningSpeed * Time.deltaTime);
            
            //Gravity
            velocity.y += gravity * Time.deltaTime;
            playerController.Move(velocity * Time.deltaTime);

        }

        //Check player ground 
        private void checkGround()
        {
            Debug.DrawRay(groundCheck.position, Vector3.down, Color.red);
            RaycastHit hit;

            if (Physics.Raycast(groundCheck.position, -Vector3.up, out hit, groundDistance))
            {
                //--
                if (hit.collider.gameObject.layer == 10 || hit.collider.gameObject.layer == 11) return;
                
                Renderer collider = hit.collider.GetComponent<Renderer>();

                string MaterialName = collider.material.name;
                bool GroundMaterial = MaterialName.Contains("Ground");
                bool JumpMaterial = MaterialName.Contains("JumpPad");

               
                //Check map triggers

                if (GroundMaterial)
                {
                    isGrounded = true;
                    previousWall = null;
                    return;
                }
                else if (JumpMaterial)
                {
                    velocity = hit.normal * 45f;
                    velocity.y = Vector3.up.y * 30f;
                    StartCoroutine(FreezeMovement(.4f));
                } 
            }
            isGrounded = false;
        }

        //Check player is on wall 
        private void checkWall()
        {
            RaycastHit hit;
            
            if (Physics.SphereCast(wallCheck.position, minWallDistance, move.normalized, out hit, maxWallDistance))
            {
                //If detects a player on collision range ignores
                if (hit.collider.gameObject.layer == 11 || hit.collider.gameObject.layer == 10) return;

                bool WallMaterial = hit.collider.GetComponent<Renderer>().material.name.Contains("Wall");

                if (WallMaterial)
                {
                    currentWall = hit.collider.name;
                    isOnWall = true;
                    wallDetector = hit;
                    return;
                }
            }
            isOnWall = false;

        }

        #region Enum's

        //Smooth teleportation
        public IEnumerator TeleportToPoint(Vector3 origin, Vector3 destination, float travelTime)
        {
            allowMovementUpdate = false;
            allowCameraUpdate = false;

            float timeElapsed = 0f;
            //While the teleport has not finished. Player cannot use controls.
            while (timeElapsed < travelTime)
            {
                transform.position = Vector3.Lerp(origin, destination, timeElapsed / travelTime);
                timeElapsed += Time.deltaTime;

                yield return null;
            }
            allowCameraUpdate = true;
            allowMovementUpdate = true;

        }

        //Freeze controls for a certain of time
        public IEnumerator FreezeCameraMovement(float time)
        {
            allowCameraUpdate = false;
            float timeElapsed = 0f;
            while (timeElapsed < time)
            {
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            allowCameraUpdate = true;
        }

        //Freeze controls for a certain of time
        public IEnumerator FreezeMovement(float time)
        {
            allowMovementUpdate = false;
            float timeElapsed = 0f;
            while (timeElapsed < time)
            {
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            allowMovementUpdate = true;
        }

        #endregion
    }
}