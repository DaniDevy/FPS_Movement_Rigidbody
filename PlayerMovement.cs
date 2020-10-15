// Some stupid rigidbody based movement by Dani

using UnityEngine;

public class PlayerMovement : MonoBehaviour {

    Rigidbody rigidBody;

    //Assingables
    public Transform playerCam;
    public Transform orientation;
    BoxCollider feet;

    //Rotation and look
    float xRotation = 0;
    const float sensitivity = 50f;

    //Movement
    public float moveSpeed = 4500;
    public float maxSpeed = 20;
    bool grounded;

    const float threshold = 0.01f;
    public float maxSlopeAngle = 45f;

    //Crouch & Slide
    Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    Vector3 playerScale;
    public float slideForce = 400;

    public float friction = 0.175f;
    public float slideFriction = 0.2f;

    //Jumping
    public float jumpForce = 550f;

    //Input
    Vector2 inputDirection = new Vector2();
    bool crouching;

    //Sliding
    Vector3 normalVector = Vector3.up;

    void Awake() {
        feet = GetComponent<BoxCollider>();
        rigidBody = GetComponent<Rigidbody>();
    }

    void Start() {
        playerScale =  transform.localScale;
        LockCursor();
    }

    void LockCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void FixedUpdate() {
        Movement();
    }

    void Update() {
        MyInput();
        Look();
    }

    /// <summary>
    /// Find user input. Should put this in its own class but im lazy
    /// </summary>
    void MyInput() {
        inputDirection.x = Input.GetAxisRaw("Horizontal");
        inputDirection.y = Input.GetAxisRaw("Vertical");
        inputDirection.Normalize();


        if (Input.GetButton("Jump"))
        {
            Jump();
        }

        //Crouching
        if (Input.GetKeyDown(KeyCode.LeftControl))
            StartCrouch();
        if (Input.GetKeyUp(KeyCode.LeftControl))
            StopCrouch();

        feet.enabled = inputDirection.magnitude == 0 && !crouching;
    }

    void StartCrouch() {
        crouching = true;
        // squash the player
        transform.localScale = crouchScale;
        // move them up
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
        // if player is moving at a fast enough speed
        if (rigidBody.velocity.magnitude > 0.5f) {
            // and on the ground
            if (grounded) {
                // slide boost forward
                rigidBody.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    void StopCrouch() {
        crouching = false;
        // reset scale
        transform.localScale = playerScale;
        // move down
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    void Movement() {
        rigidBody.AddForce(Vector3.down * 10 * Time.deltaTime);

        ApplyFriction();

        //Set max speed
        float maxSpeed = this.maxSpeed;

        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (crouching && grounded) {
            rigidBody.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        // Movement in air
        if (!grounded) {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Movement while sliding
        if (grounded && crouching) multiplierV = 0f;

        //Apply forces to move player
        // more easily adjust left/right while in the air than forward/back
        rigidBody.AddForce(orientation.transform.right * inputDirection.x * moveSpeed * Time.deltaTime * multiplier);
        rigidBody.AddForce(orientation.transform.forward * inputDirection.y * moveSpeed * Time.deltaTime * multiplier * multiplierV);

        if (rigidBody.velocity.magnitude > maxSpeed) {
            rigidBody.velocity = rigidBody.velocity.normalized * maxSpeed;
        }
    }

    void Jump()
    {
        if (grounded)
        {
            grounded = false;
            rigidBody.AddForce(normalVector * jumpForce);
        }
    }

    void Look() {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime;

        //Find current look rotation
        Vector3 rot = playerCam.transform.localRotation.eulerAngles;
        float desiredY = rot.y + mouseX;

        //Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //Perform the rotations
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredY, 0);
        orientation.transform.localRotation = Quaternion.Euler(0, desiredY, 0);
    }

    void ApplyFriction() {
        if (!grounded) return;

        //Slow down sliding
        if (crouching) {
            rigidBody.AddForce(moveSpeed * Time.deltaTime * -rigidBody.velocity.normalized * slideFriction);
            return;
        }

        Vector3 inverseVelocity = -orientation.InverseTransformDirection(rigidBody.velocity);

        if (inputDirection.x == 0)
        {
            rigidBody.AddForce(inverseVelocity.x * orientation.transform.right * moveSpeed * friction * Time.deltaTime);
        }
        if (inputDirection.y == 0)
        {
            rigidBody.AddForce(inverseVelocity.z * orientation.transform.forward * moveSpeed * friction * Time.deltaTime);
        }
    }

    bool IsFloorAngle(Vector3 v) {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    bool cancellingGrounded;

    /// <summary>
    /// Handle ground detection
    /// </summary>
    void OnCollisionStay(Collision other)
    {
        int layer = other.gameObject.layer;
        int ground = LayerMask.NameToLayer("Ground");
        if (layer != ground) return;

        //Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;
            if (IsFloorAngle(normal))
            {
                grounded = true;
                normalVector = normal;
                cancellingGrounded = false;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        if (!cancellingGrounded)
        {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime);
        }
    }

    private void StopGrounded()
    {
        grounded = false;
    }
}
