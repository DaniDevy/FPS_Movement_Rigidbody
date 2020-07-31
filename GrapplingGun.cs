// PlayerMovement
using Audio;
using EZCameraShake;
using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
	public GameObject spawnWeapon;

	private float sensitivity = 50f;

	private float sensMultiplier = 1f;

	private bool dead;

	public PhysicMaterial deadMat;

	public Transform playerCam;

	public Transform orientation;

	public Transform gun;

	private float xRotation;

	public Rigidbody rb;

	private float moveSpeed = 4500f;

	private float walkSpeed = 20f;

	private float runSpeed = 10f;

	public bool grounded;

	public Transform groundChecker;

	public LayerMask whatIsGround;

	public LayerMask whatIsWallrunnable;

	private bool readyToJump;

	private float jumpCooldown = 0.25f;

	private float jumpForce = 550f;

	private float x;

	private float y;

	private bool jumping;

	private bool sprinting;

	private bool crouching;

	public LineRenderer lr;

	private Vector3 grapplePoint;

	private SpringJoint joint;

	private Vector3 normalVector;

	private Vector3 wallNormalVector;

	private bool wallRunning;

	private Vector3 wallRunPos;

	private DetectWeapons detectWeapons;

	public ParticleSystem ps;

	private ParticleSystem.EmissionModule psEmission;

	private Collider playerCollider;

	public bool exploded;

	public bool paused;

	public LayerMask whatIsGrabbable;

	private Rigidbody objectGrabbing;

	private Vector3 previousLookdir;

	private Vector3 grabPoint;

	private float dragForce = 700000f;

	private SpringJoint grabJoint;

	private LineRenderer grabLr;

	private Vector3 myGrabPoint;

	private Vector3 myHandPoint;

	private Vector3 endPoint;

	private Vector3 grappleVel;

	private float offsetMultiplier;

	private float offsetVel;

	private float distance;

	private float slideSlowdown = 0.2f;

	private float actualWallRotation;

	private float wallRotationVel;

	private float desiredX;

	private bool cancelling;

	private bool readyToWallrun = true;

	private float wallRunGravity = 1f;

	private float maxSlopeAngle = 35f;

	private float wallRunRotation;

	private bool airborne;

	private int nw;

	private bool onWall;

	private bool onGround;

	private bool surfing;

	private bool cancellingGrounded;

	private bool cancellingWall;

	private bool cancellingSurf;

	public LayerMask whatIsHittable;

	private float desiredTimeScale = 1f;

	private float timeScaleVel;

	private float actionMeter;

	private float vel;

	public static PlayerMovement Instance
	{
		get;
		private set;
	}

	private void Awake()
	{
		Instance = this;
		rb = GetComponent<Rigidbody>();
	}

	private void Start()
	{
		psEmission = ps.emission;
		playerCollider = GetComponent<Collider>();
		detectWeapons = (DetectWeapons)GetComponentInChildren(typeof(DetectWeapons));
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
		readyToJump = true;
		wallNormalVector = Vector3.up;
		CameraShake();
		if (spawnWeapon != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(spawnWeapon, base.transform.position, Quaternion.identity);
			detectWeapons.ForcePickup(gameObject);
		}
		UpdateSensitivity();
	}

	public void UpdateSensitivity()
	{
		if ((bool)GameState.Instance)
		{
			sensMultiplier = GameState.Instance.GetSensitivity();
		}
	}

	private void LateUpdate()
	{
		if (!dead && !paused)
		{
			DrawGrapple();
			DrawGrabbing();
			WallRunning();
		}
	}

	private void FixedUpdate()
	{
		if (!dead && !Game.Instance.done && !paused)
		{
			Movement();
		}
	}

	private void Update()
	{
		UpdateActionMeter();
		MyInput();
		if (!dead && !Game.Instance.done && !paused)
		{
			Look();
			DrawGrabbing();
			UpdateTimescale();
			if (base.transform.position.y < -200f)
			{
				KillPlayer();
			}
		}
	}

	private void MyInput()
	{
		if (dead || Game.Instance.done)
		{
			return;
		}
		x = Input.GetAxisRaw("Horizontal");
		y = Input.GetAxisRaw("Vertical");
		jumping = Input.GetButton("Jump");
		crouching = Input.GetButton("Crouch");
		if (Input.GetButtonDown("Cancel"))
		{
			Pause();
		}
		if (paused)
		{
			return;
		}
		if (Input.GetButtonDown("Crouch"))
		{
			StartCrouch();
		}
		if (Input.GetButtonUp("Crouch"))
		{
			StopCrouch();
		}
		if (Input.GetButton("Fire1"))
		{
			if (detectWeapons.HasGun())
			{
				detectWeapons.Shoot(HitPoint());
			}
			else
			{
				GrabObject();
			}
		}
		if (Input.GetButtonUp("Fire1"))
		{
			detectWeapons.StopUse();
			if ((bool)objectGrabbing)
			{
				StopGrab();
			}
		}
		if (Input.GetButtonDown("Pickup"))
		{
			detectWeapons.Pickup();
		}
		if (Input.GetButtonDown("Drop"))
		{
			detectWeapons.Throw((HitPoint() - detectWeapons.weaponPos.position).normalized);
		}
	}

	private void Pause()
	{
		if (!dead)
		{
			if (paused)
			{
				Time.timeScale = 1f;
				UIManger.Instance.DeadUI(b: false);
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
				paused = false;
			}
			else
			{
				paused = true;
				Time.timeScale = 0f;
				UIManger.Instance.DeadUI(b: true);
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
			}
		}
	}

	private void UpdateTimescale()
	{
		if (!Game.Instance.done && !paused && !dead)
		{
			Time.timeScale = Mathf.SmoothDamp(Time.timeScale, desiredTimeScale, ref timeScaleVel, 0.15f);
		}
	}

	private void GrabObject()
	{
		if (objectGrabbing == null)
		{
			StartGrab();
		}
		else
		{
			HoldGrab();
		}
	}

	private void DrawGrabbing()
	{
		if ((bool)objectGrabbing)
		{
			myGrabPoint = Vector3.Lerp(myGrabPoint, objectGrabbing.position, Time.deltaTime * 45f);
			myHandPoint = Vector3.Lerp(myHandPoint, grabJoint.connectedAnchor, Time.deltaTime * 45f);
			grabLr.SetPosition(0, myGrabPoint);
			grabLr.SetPosition(1, myHandPoint);
		}
	}

	private void StartGrab()
	{
		RaycastHit[] array = Physics.RaycastAll(playerCam.transform.position, playerCam.transform.forward, 8f, whatIsGrabbable);
		if (array.Length < 1)
		{
			return;
		}
		int num = 0;
		while (true)
		{
			if (num < array.Length)
			{
				MonoBehaviour.print("testing on: " + array[num].collider.gameObject.layer);
				if ((bool)array[num].transform.GetComponent<Rigidbody>())
				{
					break;
				}
				num++;
				continue;
			}
			return;
		}
		objectGrabbing = array[num].transform.GetComponent<Rigidbody>();
		grabPoint = array[num].point;
		grabJoint = objectGrabbing.gameObject.AddComponent<SpringJoint>();
		grabJoint.autoConfigureConnectedAnchor = false;
		grabJoint.minDistance = 0f;
		grabJoint.maxDistance = 0f;
		grabJoint.damper = 4f;
		grabJoint.spring = 40f;
		grabJoint.massScale = 5f;
		objectGrabbing.angularDrag = 5f;
		objectGrabbing.drag = 1f;
		previousLookdir = playerCam.transform.forward;
		grabLr = objectGrabbing.gameObject.AddComponent<LineRenderer>();
		grabLr.positionCount = 2;
		grabLr.startWidth = 0.05f;
		grabLr.material = new Material(Shader.Find("Sprites/Default"));
		grabLr.numCapVertices = 10;
		grabLr.numCornerVertices = 10;
	}

	private void HoldGrab()
	{
		grabJoint.connectedAnchor = playerCam.transform.position + playerCam.transform.forward * 5.5f;
		grabLr.startWidth = 0f;
		grabLr.endWidth = 0.0075f * objectGrabbing.velocity.magnitude;
		previousLookdir = playerCam.transform.forward;
	}

	private void StopGrab()
	{
		UnityEngine.Object.Destroy(grabJoint);
		UnityEngine.Object.Destroy(grabLr);
		objectGrabbing.angularDrag = 0.05f;
		objectGrabbing.drag = 0f;
		objectGrabbing = null;
	}

	private void StartCrouch()
	{
		float d = 400f;
		base.transform.localScale = new Vector3(1f, 0.5f, 1f);
		base.transform.position = new Vector3(base.transform.position.x, base.transform.position.y - 0.5f, base.transform.position.z);
		if (rb.velocity.magnitude > 0.1f && grounded)
		{
			rb.AddForce(orientation.transform.forward * d);
			AudioManager.Instance.Play("StartSlide");
			AudioManager.Instance.Play("Slide");
		}
	}

	private void StopCrouch()
	{
		base.transform.localScale = new Vector3(1f, 1.5f, 1f);
		base.transform.position = new Vector3(base.transform.position.x, base.transform.position.y + 0.5f, base.transform.position.z);
	}

	private void DrawGrapple()
	{
		if (grapplePoint == Vector3.zero || joint == null)
		{
			lr.positionCount = 0;
			return;
		}
		lr.positionCount = 2;
		endPoint = Vector3.Lerp(endPoint, grapplePoint, Time.deltaTime * 15f);
		offsetMultiplier = Mathf.SmoothDamp(offsetMultiplier, 0f, ref offsetVel, 0.1f);
		int num = 100;
		lr.positionCount = num;
		Vector3 position = gun.transform.GetChild(0).position;
		float num2 = Vector3.Distance(endPoint, position);
		lr.SetPosition(0, position);
		lr.SetPosition(num - 1, endPoint);
		float num3 = num2;
		float num4 = 1f;
		for (int i = 1; i < num - 1; i++)
		{
			float num5 = (float)i / (float)num;
			float num6 = num5 * offsetMultiplier;
			float num7 = (Mathf.Sin(num6 * num3) - 0.5f) * num4 * (num6 * 2f);
			Vector3 normalized = (endPoint - position).normalized;
			float num8 = Mathf.Sin(num5 * 180f * ((float)Math.PI / 180f));
			float num9 = Mathf.Cos(offsetMultiplier * 90f * ((float)Math.PI / 180f));
			Vector3 position2 = position + (endPoint - position) / num * i + ((Vector3)(num9 * num7 * Vector2.Perpendicular(normalized)) + offsetMultiplier * num8 * Vector3.down);
			lr.SetPosition(i, position2);
		}
	}

	private void FootSteps()
	{
		if (!crouching && !dead && (grounded || wallRunning))
		{
			float num = 1.2f;
			float num2 = rb.velocity.magnitude;
			if (num2 > 20f)
			{
				num2 = 20f;
			}
			distance += num2;
			if (distance > 300f / num)
			{
				AudioManager.Instance.PlayFootStep();
				distance = 0f;
			}
		}
	}

	private void Movement()
	{
		if (dead)
		{
			return;
		}
		rb.AddForce(Vector3.down * Time.deltaTime * 10f);
		Vector2 mag = FindVelRelativeToLook();
		float num = mag.x;
		float num2 = mag.y;
		FootSteps();
		CounterMovement(x, y, mag);
		if (readyToJump && jumping)
		{
			Jump();
		}
		float num3 = walkSpeed;
		if (sprinting)
		{
			num3 = runSpeed;
		}
		if (crouching && grounded && readyToJump)
		{
			rb.AddForce(Vector3.down * Time.deltaTime * 3000f);
			return;
		}
		if (x > 0f && num > num3)
		{
			x = 0f;
		}
		if (x < 0f && num < 0f - num3)
		{
			x = 0f;
		}
		if (y > 0f && num2 > num3)
		{
			y = 0f;
		}
		if (y < 0f && num2 < 0f - num3)
		{
			y = 0f;
		}
		float d = 1f;
		float d2 = 1f;
		if (!grounded)
		{
			d = 0.5f;
			d2 = 0.5f;
		}
		if (grounded && crouching)
		{
			d2 = 0f;
		}
		if (wallRunning)
		{
			d2 = 0.3f;
			d = 0.3f;
		}
		if (surfing)
		{
			d = 0.7f;
			d2 = 0.3f;
		}
		rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * d * d2);
		rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * d);
		SpeedLines();
	}

	private void SpeedLines()
	{
		float num = Vector3.Angle(rb.velocity, playerCam.transform.forward) * 0.15f;
		if (num < 1f)
		{
			num = 1f;
		}
		float rateOverTimeMultiplier = rb.velocity.magnitude / num;
		if (grounded && !wallRunning)
		{
			rateOverTimeMultiplier = 0f;
		}
		psEmission.rateOverTimeMultiplier = rateOverTimeMultiplier;
	}

	private void CameraShake()
	{
		float num = rb.velocity.magnitude / 9f;
		CameraShaker.Instance.ShakeOnce(num, 0.1f * num, 0.25f, 0.2f);
		Invoke("CameraShake", 0.2f);
	}

	private void ResetJump()
	{
		readyToJump = true;
	}

	private void Jump()
	{
		if ((grounded || wallRunning || surfing) && readyToJump)
		{
			MonoBehaviour.print("jumping");
			Vector3 velocity = rb.velocity;
			readyToJump = false;
			rb.AddForce(Vector2.up * jumpForce * 1.5f);
			rb.AddForce(normalVector * jumpForce * 0.5f);
			if (rb.velocity.y < 0.5f)
			{
				rb.velocity = new Vector3(velocity.x, 0f, velocity.z);
			}
			else if (rb.velocity.y > 0f)
			{
				rb.velocity = new Vector3(velocity.x, velocity.y / 2f, velocity.z);
			}
			if (wallRunning)
			{
				rb.AddForce(wallNormalVector * jumpForce * 3f);
			}
			Invoke("ResetJump", jumpCooldown);
			if (wallRunning)
			{
				wallRunning = false;
			}
			AudioManager.Instance.PlayJump();
		}
	}

	private void Look()
	{
		float num = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
		float num2 = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
		Vector3 eulerAngles = playerCam.transform.localRotation.eulerAngles;
		desiredX = eulerAngles.y + num;
		xRotation -= num2;
		xRotation = Mathf.Clamp(xRotation, -90f, 90f);
		FindWallRunRotation();
		actualWallRotation = Mathf.SmoothDamp(actualWallRotation, wallRunRotation, ref wallRotationVel, 0.2f);
		playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, actualWallRotation);
		orientation.transform.localRotation = Quaternion.Euler(0f, desiredX, 0f);
	}

	private void CounterMovement(float x, float y, Vector2 mag)
	{
		if (!grounded || jumping || exploded)
		{
			return;
		}
		float d = 0.16f;
		float num = 0.01f;
		if (crouching)
		{
			rb.AddForce(moveSpeed * Time.deltaTime * -rb.velocity.normalized * slideSlowdown);
			return;
		}
		if ((Math.Abs(mag.x) > num && Math.Abs(x) < 0.05f) || (mag.x < 0f - num && x > 0f) || (mag.x > num && x < 0f))
		{
			rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * (0f - mag.x) * d);
		}
		if ((Math.Abs(mag.y) > num && Math.Abs(y) < 0.05f) || (mag.y < 0f - num && y > 0f) || (mag.y > num && y < 0f))
		{
			rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * (0f - mag.y) * d);
		}
		if (Mathf.Sqrt(Mathf.Pow(rb.velocity.x, 2f) + Mathf.Pow(rb.velocity.z, 2f)) > walkSpeed)
		{
			float num2 = rb.velocity.y;
			Vector3 vector = rb.velocity.normalized * walkSpeed;
			rb.velocity = new Vector3(vector.x, num2, vector.z);
		}
	}

	public void Explode()
	{
		exploded = true;
		Invoke("StopExplosion", 0.1f);
	}

	private void StopExplosion()
	{
		exploded = false;
	}

	public Vector2 FindVelRelativeToLook()
	{
		float current = orientation.transform.eulerAngles.y;
		float target = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * 57.29578f;
		float num = Mathf.DeltaAngle(current, target);
		float num2 = 90f - num;
		float magnitude = rb.velocity.magnitude;
		return new Vector2(y: magnitude * Mathf.Cos(num * ((float)Math.PI / 180f)), x: magnitude * Mathf.Cos(num2 * ((float)Math.PI / 180f)));
	}

	private void FindWallRunRotation()
	{
		if (!wallRunning)
		{
			wallRunRotation = 0f;
			return;
		}
		_ = new Vector3(0f, playerCam.transform.rotation.y, 0f).normalized;
		new Vector3(0f, 0f, 1f);
		float num = 0f;
		float current = playerCam.transform.rotation.eulerAngles.y;
		if (Math.Abs(wallNormalVector.x - 1f) < 0.1f)
		{
			num = 90f;
		}
		else if (Math.Abs(wallNormalVector.x - -1f) < 0.1f)
		{
			num = 270f;
		}
		else if (Math.Abs(wallNormalVector.z - 1f) < 0.1f)
		{
			num = 0f;
		}
		else if (Math.Abs(wallNormalVector.z - -1f) < 0.1f)
		{
			num = 180f;
		}
		num = Vector3.SignedAngle(new Vector3(0f, 0f, 1f), wallNormalVector, Vector3.up);
		float num2 = Mathf.DeltaAngle(current, num);
		wallRunRotation = (0f - num2 / 90f) * 15f;
		if (!readyToWallrun)
		{
			return;
		}
		if ((Mathf.Abs(wallRunRotation) < 4f && y > 0f && Math.Abs(x) < 0.1f) || (Mathf.Abs(wallRunRotation) > 22f && y < 0f && Math.Abs(x) < 0.1f))
		{
			if (!cancelling)
			{
				cancelling = true;
				CancelInvoke("CancelWallrun");
				Invoke("CancelWallrun", 0.2f);
			}
		}
		else
		{
			cancelling = false;
			CancelInvoke("CancelWallrun");
		}
	}

	private void CancelWallrun()
	{
		MonoBehaviour.print("cancelled");
		Invoke("GetReadyToWallrun", 0.1f);
		rb.AddForce(wallNormalVector * 600f);
		readyToWallrun = false;
		AudioManager.Instance.PlayLanding();
	}

	private void GetReadyToWallrun()
	{
		readyToWallrun = true;
	}

	private void WallRunning()
	{
		if (wallRunning)
		{
			rb.AddForce(-wallNormalVector * Time.deltaTime * moveSpeed);
			rb.AddForce(Vector3.up * Time.deltaTime * rb.mass * 100f * wallRunGravity);
		}
	}

	private bool IsFloor(Vector3 v)
	{
		return Vector3.Angle(Vector3.up, v) < maxSlopeAngle;
	}

	private bool IsSurf(Vector3 v)
	{
		float num = Vector3.Angle(Vector3.up, v);
		if (num < 89f)
		{
			return num > maxSlopeAngle;
		}
		return false;
	}

	private bool IsWall(Vector3 v)
	{
		return Math.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;
	}

	private bool IsRoof(Vector3 v)
	{
		return v.y == -1f;
	}

	private void StartWallRun(Vector3 normal)
	{
		if (!grounded && readyToWallrun)
		{
			wallNormalVector = normal;
			float d = 20f;
			if (!wallRunning)
			{
				rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
				rb.AddForce(Vector3.up * d, ForceMode.Impulse);
			}
			wallRunning = true;
		}
	}

	private void OnCollisionEnter(Collision other)
	{
		if (other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
		{
			KillEnemy(other);
		}
	}

	private void OnCollisionExit(Collision other)
	{
	}

	private void OnCollisionStay(Collision other)
	{
		int layer = other.gameObject.layer;
		if ((int)whatIsGround != ((int)whatIsGround | (1 << layer)))
		{
			return;
		}
		for (int i = 0; i < other.contactCount; i++)
		{
			Vector3 normal = other.contacts[i].normal;
			if (IsFloor(normal))
			{
				if (wallRunning)
				{
					wallRunning = false;
				}
				if (!grounded && crouching)
				{
					AudioManager.Instance.Play("StartSlide");
					AudioManager.Instance.Play("Slide");
				}
				grounded = true;
				normalVector = normal;
				cancellingGrounded = false;
				CancelInvoke("StopGrounded");
			}
			if (IsWall(normal) && layer == LayerMask.NameToLayer("Ground"))
			{
				if (!onWall)
				{
					AudioManager.Instance.Play("StartSlide");
					AudioManager.Instance.Play("Slide");
				}
				StartWallRun(normal);
				onWall = true;
				cancellingWall = false;
				CancelInvoke("StopWall");
			}
			if (IsSurf(normal))
			{
				surfing = true;
				cancellingSurf = false;
				CancelInvoke("StopSurf");
			}
			IsRoof(normal);
		}
		float num = 3f;
		if (!cancellingGrounded)
		{
			cancellingGrounded = true;
			Invoke("StopGrounded", Time.deltaTime * num);
		}
		if (!cancellingWall)
		{
			cancellingWall = true;
			Invoke("StopWall", Time.deltaTime * num);
		}
		if (!cancellingSurf)
		{
			cancellingSurf = true;
			Invoke("StopSurf", Time.deltaTime * num);
		}
	}

	private void StopGrounded()
	{
		grounded = false;
	}

	private void StopWall()
	{
		onWall = false;
		wallRunning = false;
	}

	private void StopSurf()
	{
		surfing = false;
	}

	private void KillEnemy(Collision other)
	{
		if ((grounded && !crouching) || rb.velocity.magnitude < 3f)
		{
			return;
		}
		Enemy enemy = (Enemy)other.transform.root.GetComponent(typeof(Enemy));
		if ((bool)enemy && !enemy.IsDead())
		{
			UnityEngine.Object.Instantiate(PrefabManager.Instance.enemyHitAudio, other.contacts[0].point, Quaternion.identity);
			RagdollController ragdollController = (RagdollController)other.transform.root.GetComponent(typeof(RagdollController));
			if (grounded && crouching)
			{
				ragdollController.MakeRagdoll(rb.velocity * 1.2f * 34f);
			}
			else
			{
				ragdollController.MakeRagdoll(rb.velocity.normalized * 250f);
			}
			rb.AddForce(rb.velocity.normalized * 2f, ForceMode.Impulse);
			enemy.DropGun(rb.velocity.normalized * 2f);
		}
	}

	public Vector3 GetVelocity()
	{
		return rb.velocity;
	}

	public float GetFallSpeed()
	{
		return rb.velocity.y;
	}

	public Vector3 GetGrapplePoint()
	{
		return detectWeapons.GetGrapplerPoint();
	}

	public Collider GetPlayerCollider()
	{
		return playerCollider;
	}

	public Transform GetPlayerCamTransform()
	{
		return playerCam.transform;
	}

	public Vector3 HitPoint()
	{
		RaycastHit[] array = Physics.RaycastAll(playerCam.transform.position, playerCam.transform.forward, (int)whatIsHittable);
		if (array.Length < 1)
		{
			return playerCam.transform.position + playerCam.transform.forward * 100f;
		}
		if (array.Length > 1)
		{
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].transform.gameObject.layer == LayerMask.NameToLayer("Enemy"))
				{
					return array[i].point;
				}
			}
		}
		return array[0].point;
	}

	public float GetRecoil()
	{
		return detectWeapons.GetRecoil();
	}

	public void KillPlayer()
	{
		if (!Game.Instance.done)
		{
			CameraShaker.Instance.ShakeOnce(3f * GameState.Instance.cameraShake, 2f, 0.1f, 0.6f);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			UIManger.Instance.DeadUI(b: true);
			Timer.Instance.Stop();
			dead = true;
			rb.freezeRotation = false;
			playerCollider.material = deadMat;
			detectWeapons.Throw(Vector3.zero);
			paused = false;
			ResetSlowmo();
		}
	}

	public void Respawn()
	{
		detectWeapons.StopUse();
	}

	public void Slowmo(float timescale, float length)
	{
		if (GameState.Instance.slowmo)
		{
			CancelInvoke("Slowmo");
			desiredTimeScale = timescale;
			Invoke("ResetSlowmo", length);
			AudioManager.Instance.Play("SlowmoStart");
		}
	}

	private void ResetSlowmo()
	{
		desiredTimeScale = 1f;
		AudioManager.Instance.Play("SlowmoEnd");
	}

	public bool IsCrouching()
	{
		return crouching;
	}

	public bool HasGun()
	{
		return detectWeapons.HasGun();
	}

	public bool IsDead()
	{
		return dead;
	}

	public Rigidbody GetRb()
	{
		return rb;
	}

	private void UpdateActionMeter()
	{
		float target = 0.09f;
		if (rb.velocity.magnitude > 15f && (!dead || !Game.Instance.playing))
		{
			target = 1f;
		}
		actionMeter = Mathf.SmoothDamp(actionMeter, target, ref vel, 0.7f);
	}

	public float GetActionMeter()
	{
		return actionMeter * 22000f;
	}
}
