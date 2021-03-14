using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace com.brutz.Player
{
    public class player_fire : MonoBehaviourPunCallbacks
    {
        [SerializeField] Transform firePoint;
        [SerializeField] Camera playerCamera;
        [SerializeField] Material typeOfLaser;
        [SerializeField] GameObject Weapon;
        [SerializeField] List<AudioClip> audioClips;
        [SerializeField] List<AudioClip> killClips;
        [SerializeField] AudioClip teleportSound;

        [SerializeField] GameObject bloodObject;
        [SerializeField] GameObject bloodObject2;

        private SpawnManager manager;
        private player_controller playerController;
        private LineRenderer laser;
        private Vector3 CachedWeaponLocalPosition;
        
        private float laserWidth = .5f;
        //Delay of the player firing the laser gun
        
        private float nextFire = 0f;
        private float fireDelay = 1f;

        //Delay of the player activating the skill
        private float nextSkill = 0f;
        private float skillDelay = 5f;

        //Superlaser skill
        private bool superLaserAvailable = false;

        //Check if ray is going to the void
        private bool isRayFinite = false;



        public Vector2 offsetSpeed = Vector2.one;

        private void Start()
        {
            if (photonView.IsMine)
            {
                gameObject.layer = 10;
            } else
            {
                gameObject.layer = 11;
            }
            manager = GameObject.Find("EventSystem").GetComponent<SpawnManager>();
            playerController = gameObject.GetComponent<player_controller>();
            CachedWeaponLocalPosition = Weapon.transform.localPosition;
        }

        // Update is called once per frame
        void Update()
        {
            Weapon.transform.localPosition = Vector3.Lerp(Weapon.transform.localPosition, CachedWeaponLocalPosition, Time.deltaTime * 3f);
            
            if (!photonView.IsMine) return;
            
            if (Input.GetButtonDown("Fire1") && Time.time >= nextFire)
            {
                nextFire = Time.time + fireDelay;
                photonView.RPC("fireLaser", RpcTarget.All);

            }

            if(Input.GetKeyDown(KeyCode.Mouse1) && Time.time >= nextSkill)
            {
                nextSkill = Time.time + skillDelay;
                GameObject teleportEffect = new GameObject();
                AudioSource teleportSource = teleportEffect.AddComponent<AudioSource>();
                teleportSource.PlayOneShot(teleportSound);
                teleportSource.volume = .4f;
                photonView.RPC("teleportSkill", RpcTarget.All);
                Destroy(teleportEffect, 1f);
            }

            if(Input.GetKeyDown(KeyCode.R))
            {
                photonView.RPC("deathEffect", RpcTarget.All);
            }
        }

        [PunRPC]
        private void teleportSkill()
        {
            if(photonView.IsMine)
            {
                RaycastHit hit;
                
                //Teleport the player
                if(Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, 100f)) {
                    StartCoroutine(playerController.TeleportToPoint(gameObject.transform.localPosition, hit.point, .2f));
                }
            }
        }

        public void fireLaser()
        {

            //Gun FX
            Vector3 newWeaponPos = new Vector3(Weapon.transform.localPosition.x, Weapon.transform.localPosition.y, Weapon.transform.localPosition.z * 2f);
            Vector3 kickback = Vector3.Lerp(Weapon.transform.localPosition, newWeaponPos, Time.deltaTime * 4f);
            Weapon.transform.localPosition -= kickback;
            

            RaycastHit hit;
            if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, 500f))
            {
                fireLaser(hit, true);
                
                //If hitting a player 
 
                    if(hit.collider.gameObject.layer == 11)
                    {
                        //hit.collider.gameObject.GetPhotonView().RPC("death", RpcTarget.All);
                        //Debug.LogWarning("Collided with a player");
                    }
                
            }
            else
            {
                fireLaser(hit, false);
            }
            
        }

        private void fireLaser(RaycastHit hit, bool isFinite)
        {
            //GameObject setup
            isRayFinite = isFinite;
            GameObject line = new GameObject("Laser");
            laser = line.AddComponent<LineRenderer>();
            AudioSource laserAudioSource = line.AddComponent<AudioSource>();

            //Play audio
            System.Random rndNumber = new System.Random();
            int index = rndNumber.Next(audioClips.Count);
            laserAudioSource.transform.position = firePoint.transform.position;
            laserAudioSource.volume = .2f;
            laserAudioSource.PlayOneShot(audioClips[index]);

            //Set laser properties
            laser.startWidth = laserWidth;
            laser.SetPosition(0, firePoint.transform.position);
            if(isFinite)
            {
                laser.SetPosition(1, hit.point);
            } else
            {
                laser.SetPosition(1, playerCamera.transform.forward * 500f);
            }

            //Rescale texture due the distance of fire
            typeOfLaser.mainTextureScale = new Vector2((firePoint.transform.position.magnitude - hit.point.magnitude) / 3, 1);

            //Laser texture
            laser.material = typeOfLaser;

            //Destroying
            Destroy(line, fireDelay);
        }

        
        private void death()
        {
            if(photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
                Destroy(gameObject);
                manager.Spawn();
            }

            System.Random random = new System.Random();
            GameObject killEffect = Instantiate(bloodObject, gameObject.transform.position, gameObject.transform.rotation);
            GameObject killEffect2 = Instantiate(bloodObject, gameObject.transform.position, gameObject.transform.rotation);
            AudioSource killsound = killEffect.AddComponent<AudioSource>();
            killsound.PlayOneShot(killClips[random.Next(killClips.Count)]);
            Destroy(killEffect, 2f);
            Destroy(killEffect2, 2f);
        }
    }
}