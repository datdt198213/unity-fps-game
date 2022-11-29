using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Player : MonoBehaviourPunCallbacks
{
    public Transform viewPoint;
    public float mouseSensitivity = 1f;
    private float verticalRotStore;
    private Vector2 mouseInput;

    public bool invertLook;

    public float moveSpeed = 5f, runSpeed = 8f;
    private float activeMoveSpeed;
    private Vector3 moveDir, movement;

    public CharacterController characterController;

    private Camera cam;

    public float jumpForce = 12f, gravityMod = 2.5f;
    public Transform groundCheckPoint;
    private bool isGrounded;
    public LayerMask groundLayer;
   
    public GameObject bulletImpact;
    // public float timeBetweenShots = .1f;
    private float shotCounter;
    public float muzzleDisplayTime;
    private float muzzleCounter;

    public float maxHeat = 10f,/* heatPerShot = 1f, */ coolRate = 4f, overHeatCoolRate = 5f;
    private float heatCounter;
    private bool overHeated;

    public Gun[] allGuns;
    private int selectedGun;

    public GameObject playerHitImpact;

    public int maxHealth = 100;
    private int currentHealth;

    public Animator animator;
    public GameObject playerModel;
    public Transform modelGunPoint, gunHolder;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        cam = Camera.main;

        UI.instance.weaponTemSlider.maxValue = maxHeat;

        // SwitchGun();
        photonView.RPC("SetGun", RpcTarget.All, selectedGun);

        currentHealth = maxHealth;

        // Transform newTransform = SpawnManager.instance.GetSpawnPoint();
        // transform.position = newTransform.position;
        // transform.rotation = newTransform.rotation;

        if(photonView.IsMine)
        {
            playerModel.SetActive(false);

            UI.instance.healthSlider.maxValue = maxHealth;
            UI.instance.healthSlider.value = currentHealth;
        }  
        else
        {
            gunHolder.parent = modelGunPoint;
            gunHolder.localPosition = Vector3.zero;
            gunHolder.localRotation = Quaternion.identity;
        }    

    }

    // Update is called once per frame
    void Update()
    {
        if(photonView.IsMine)
        {
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");
            mouseInput = new Vector2(mouseX, mouseY) * mouseSensitivity;

            transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y + mouseInput.x, transform.eulerAngles.z);

            verticalRotStore += mouseInput.y;
            verticalRotStore = Mathf.Clamp(verticalRotStore, -60f, 60f);
            
            if(invertLook) 
            {
                viewPoint.rotation = Quaternion.Euler(verticalRotStore, viewPoint.rotation.eulerAngles.y, viewPoint.rotation.eulerAngles.z);
            } else 
            {
                viewPoint.rotation = Quaternion.Euler(-verticalRotStore, viewPoint.rotation.eulerAngles.y, viewPoint.rotation.eulerAngles.z);
            }

            moveDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

            if(Input.GetKey(KeyCode.LeftShift)) 
            {
                activeMoveSpeed = runSpeed;
            }else 
            {
                activeMoveSpeed = moveSpeed;
            }
            
            float yVel = movement.y;
            movement = ((transform.forward * moveDir.z) + (transform.right * moveDir.x)).normalized * activeMoveSpeed;
            // transform.position += movement * moveSpeed * Time.deltaTime;

            if(allGuns[selectedGun].muzzleFlash.activeInHierarchy)
            {
                muzzleCounter -= Time.deltaTime;

                if(muzzleCounter <= 0)
                {
                    allGuns[selectedGun].muzzleFlash.SetActive(false);
                }

            }

            if(!overHeated)
            {

                if(Input.GetMouseButtonDown(0)) 
                {
                    Shoot();
                }

                if(Input.GetMouseButton(0) && allGuns[selectedGun].isAutomatic) 
                {
                    shotCounter -= Time.deltaTime;

                    if(shotCounter <= 0) 
                    {
                        Shoot();
                    }
                }
                heatCounter -= coolRate * Time.deltaTime;
            } else
            {
                heatCounter -= overHeatCoolRate * Time.deltaTime;
                if(heatCounter <= 0)
                {
                    overHeated = false;

                    UI.instance.overheatedMessage.gameObject.SetActive(false);
                }
            }
            
            if(heatCounter < 0)
            {
                heatCounter = 0f;
            }

            UI.instance.weaponTemSlider.value = heatCounter;

            if(!characterController.isGrounded)
            {
                movement.y = yVel;
            }

            isGrounded = Physics.Raycast(groundCheckPoint.position, Vector3.down, .25f, groundLayer);

            if(Input.GetButtonDown("Jump") && isGrounded)
            {
                movement.y = jumpForce;
            }

            movement.y += Physics.gravity.y * Time.deltaTime * gravityMod;

            characterController.Move(movement * Time.deltaTime);

            if(Input.GetAxisRaw("Mouse ScrollWheel") > 0f)
            {
                selectedGun++;
            
                if(selectedGun >= allGuns.Length)
                {
                    selectedGun = 0;
                }
                // SwitchGun();
                photonView.RPC("SetGun", RpcTarget.All, selectedGun);


            } else if(Input.GetAxisRaw("Mouse ScrollWheel") < 0f)
            {
                selectedGun--;

                if(selectedGun < 0)
                {
                    selectedGun = allGuns.Length - 1;
                }
                // SwitchGun(); 
                photonView.RPC("SetGun", RpcTarget.All, selectedGun);

            }
            
            for(int i = 0; i < allGuns.Length; i++)
            {
                // Debug.Log("Input key" + Input.GetKeyDown((i + 1).ToString()));
                if(Input.GetKeyDown((i + 1).ToString())) 
                {
                    selectedGun = i;
                    // SwitchGun();
                    // Debug.Log(PhotonNetwork.NickName + " press " + (i + 1));
                    photonView.RPC("SetGun", RpcTarget.All, selectedGun);
                }
            }

            animator.SetBool("grounded", isGrounded);
            animator.SetFloat("speed", moveDir.magnitude);

            if(Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
            } else if(Cursor.lockState == CursorLockMode.None)
            {
                if(Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
        }
    }

    private void Shoot()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        ray.origin = cam.transform.position;

        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            // Debug.Log("We hit " + hit.collider.gameObject.name);

            if(hit.collider.gameObject.tag == "Player")
            {
                Debug.Log("Hit " + hit.collider.gameObject.GetPhotonView().Owner.NickName);

                PhotonNetwork.Instantiate(playerHitImpact.name, hit.point, Quaternion.identity);

                hit.collider.gameObject.GetPhotonView().RPC("DealDamage", RpcTarget.All, photonView.Owner.NickName, allGuns[selectedGun].shotDamage, PhotonNetwork.LocalPlayer.ActorNumber);
            } 
            else 
            {
                GameObject bulletImpactObject = Instantiate(bulletImpact, hit.point + (hit.normal * .002f), Quaternion.LookRotation(hit.normal, Vector3.up));

                Destroy(bulletImpactObject, 10f);
            }
        }

        shotCounter = allGuns[selectedGun].timeBetweenShots;

        heatCounter += allGuns[selectedGun].heatPerShot;
        if(heatCounter >= maxHeat)
        {
            heatCounter = maxHeat;

            overHeated = true;

            UI.instance.overheatedMessage.gameObject.SetActive(true);
        }

        allGuns[selectedGun].muzzleFlash.SetActive(true);
        muzzleCounter = muzzleDisplayTime;
    }

    private void SwitchGun() 
    {
        foreach(Gun gun in allGuns)
        {
            gun.gameObject.SetActive(false);
        }

        allGuns[selectedGun].gameObject.SetActive(true);

        allGuns[selectedGun].muzzleFlash.SetActive(false);
    }

    private void LateUpdate() 
    {
        if(photonView.IsMine)
        {
            if(MatchManager.instance.state == MatchManager.GameState.Playing)
            {
                cam.transform.position = viewPoint.position;
                cam.transform.rotation = viewPoint.rotation;
            } else
            {
                cam.transform.position = MatchManager.instance.mapCameraPoint.position;
                cam.transform.rotation = MatchManager.instance.mapCameraPoint.rotation;
            }
        } 
    }

    [PunRPC]
    public void DealDamage(string damager, int damageAmount, int actor)
    {
       TakeDamage(damager, damageAmount, actor);
    }

    public void TakeDamage(string damager, int damageAmount, int actor)
    {
        if(photonView.IsMine)
        {
            // Debug.Log(photonView.Owner.NickName + " has been hit by " + damager);
            // gameObject.SetActive(false);

            currentHealth -= damageAmount;
            
            if (currentHealth <= 0)
            {
                currentHealth = 0;

                PlayerSpawner.instance.Die(damager);

                MatchManager.instance.UpdateStatsSend(actor, 0, 1);
            }
            
            UI.instance.healthSlider.value = currentHealth;
        }

    }

    [PunRPC]
    public void SetGun(int gunToSwitchTo)
    {
        if(gunToSwitchTo < allGuns.Length)
        {
            selectedGun = gunToSwitchTo;
            SwitchGun();
        }
    }
}
