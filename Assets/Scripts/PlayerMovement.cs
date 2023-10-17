using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{

	// Parameters
	[SerializeField] private float _moveSpeed = 4500f;
	[SerializeField] private float _walkSpeed = 20f;
	[SerializeField] private float _jumpCooldown = 0.25f;
	[SerializeField] private float _jumpForce = 550f;
	[SerializeField] private float _wallRunGravity = 1f;
	[SerializeField] private float _maxSlopeAngle = 35f;

	// States
	private bool _grounded;
	private bool _readyToJump;
	private bool _jumping;
	private bool _crouching;
	private bool _wallRunning;
	private bool _cancelling;
	private bool _readyToWallrun = true;
	private bool _airborne;
	private bool _onGround;
	private bool _cancellingGrounded;
	private bool _cancellingWall;

	// Player control settings
	private const float _sensitivity = 75f;
	[SerializeField] private LayerMask _groundLayers;

	// Controls
	private float _x;
	private float _y;
	private float _xRotation;
	private float _desiredX;

	// Components
	[SerializeField] private Transform _cameraHolder;
	[SerializeField] private Transform _pivot;
	private Rigidbody _rigidbody;
	private Transform _transformSelf;

	// Wall-running
	private Vector3 _normalVector;
	private Vector3 _wallNormalVector;
	private Vector3 _wallRunPos;
	private float _offsetMultiplier;
	private float _offsetVel;
	private float _distance;
	private float _actualWallRotation;
	private float _wallRotationVel;
	private float _wallRunRotation;

	private PlayerInput _playerInput;


	private void Start()
	{
		_transformSelf = transform;
		_rigidbody = GetComponent<Rigidbody>();
		_readyToJump = true;
		_wallNormalVector = Vector3.up;
		
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

	}

	private void OnEnable()
	{
		RegisterInput();
	}

	#region Input

	private void RegisterInput()
	{
		_playerInput = new PlayerInput();
		_playerInput.KM.Crouch.performed += _ => OnCrouchPressed(true);
		_playerInput.KM.Crouch.canceled += _ => OnCrouchPressed(false);

		_playerInput.KM.Jump.performed += _ => OnJumpPressed(true);
		_playerInput.KM.Jump.canceled += _ => OnJumpPressed(false);
		_playerInput.Enable();
	}

	private void OnJumpPressed(bool check)
	{
		_jumping = check;
	}

	private void OnCrouchPressed(bool check)
	{
		_crouching = check;
		if (check)
		{
			StartCrouch();
		}
		else
		{
			StopCrouch();
		}
	}
	
	private void HandleInput()
	{
		_x = Input.GetAxisRaw("Horizontal");
		_y = Input.GetAxisRaw("Vertical");
	}

	#endregion

	#region Update

	private void LateUpdate()
	{
		WallRunning();
	}

	private void FixedUpdate()
	{
		Movement();
	}

	private void Update()
	{
		HandleInput();
		Look();
	}

	#endregion

	#region Movement

	private void StartCrouch()
	{
		float num = 400f;
		transform.localScale = new Vector3(1f, 0.5f, 1f);
		var oldPosition = _transformSelf.position;
		_transformSelf.position = new Vector3(oldPosition.x, oldPosition.y - 0.5f, oldPosition.z);
		if (_rigidbody.velocity.magnitude > 0.1f && _grounded)
		{
			_rigidbody.AddForce(_pivot.transform.forward * num);
		}
	}

	private void StopCrouch()
	{
		transform.localScale = new Vector3(1f, 1.5f, 1f);
		var oldPosition = _transformSelf.position;
		_transformSelf.position = new Vector3(oldPosition.x, oldPosition.y + 0.5f, oldPosition.z);
	}

	private void Movement()
	{
		_rigidbody.AddForce(Vector3.down * (Time.deltaTime * 10f));
		Vector2 magnitude = FindVelRelativeToLook();
		float magX = magnitude.x;
		float magY = magnitude.y;
		CounterMovement(_x, _y, magnitude);
		if (_readyToJump && _jumping)
		{
			Jump();
			if (_crouching && _grounded)
			{
				_rigidbody.AddForce(Vector3.down * (Time.deltaTime * 3000f));
				return;
			}
		}
		
		if (_x > 0f && magX > _walkSpeed)
		{
			_x = 0f;
		}
		else if (_x < 0f && magX < 0f - _walkSpeed)
		{
			_x = 0f;
		}
		else if (_y > 0f && magY > _walkSpeed)
		{
			_y = 0f;
		}
		else if (_y < 0f && magY < 0f - _walkSpeed)
		{
			_y = 0f;
		}
		float speedMultiplier = 1f;
		float forwardMultiplier = 1f;
		if (!_grounded)
		{
			speedMultiplier = 0.5f;
			forwardMultiplier = 0.5f;
		}
		if (_grounded && _crouching)
		{
			forwardMultiplier = 0f;
		}
		if (_wallRunning)
		{
			forwardMultiplier = 0.3f;
			speedMultiplier = 0.3f;
		}
		_rigidbody.AddForce(_pivot.transform.forward * (_y * _moveSpeed * Time.deltaTime * speedMultiplier * forwardMultiplier));
		_rigidbody.AddForce(_pivot.transform.right * (_x * _moveSpeed * Time.deltaTime * speedMultiplier));
	}

	private void ResetJump()
	{
		_readyToJump = true;
	}

	private void Jump()
	{
		if ((_grounded || _wallRunning) && _readyToJump)
		{
			Vector3 velocity = _rigidbody.velocity;
			_readyToJump = false;
			_rigidbody.AddForce(Vector2.up * (_jumpForce * 1.5f));
			_rigidbody.AddForce(_normalVector * (_jumpForce * 0.5f));
			if (_rigidbody.velocity.y < 0.5f)
			{
				_rigidbody.velocity = new Vector3(velocity.x, 0f, velocity.z);
			}
			else if (_rigidbody.velocity.y > 0f)
			{
				_rigidbody.velocity = new Vector3(velocity.x, velocity.y / 2f, velocity.z);
			}
			if (_wallRunning)
			{
				_rigidbody.AddForce(_wallNormalVector * (_jumpForce * 3f));
			}
			Invoke(nameof(ResetJump), _jumpCooldown);
			if (_wallRunning)
			{
				_wallRunning = false;
			}
		}
	}

	#endregion

	private void Look()
	{
		float xInput = Input.GetAxis("Mouse X") * _sensitivity * Time.fixedDeltaTime;
		float yInput = Input.GetAxis("Mouse Y") * _sensitivity * Time.fixedDeltaTime;

		_desiredX += xInput;
		_xRotation = Mathf.Clamp(_xRotation - yInput, -90f, 90f);

		// Adjust the Wall Run Rotation
		AdjustWallRunRotation();

		_cameraHolder.transform.localRotation = Quaternion.Euler(_xRotation, _desiredX, _actualWallRotation);
		_pivot.transform.localRotation = Quaternion.Euler(0f, _desiredX, 0f);
	}

	private void AdjustWallRunRotation()
	{
		FindWallRunRotation();
		_actualWallRotation = Mathf.SmoothDamp(_actualWallRotation, _wallRunRotation, ref _wallRotationVel, 0.2f);
	}


	private void CounterMovement(float movementInputX, float movementInputY, Vector2 movementMagnitude)
	{
		if (!_grounded || _jumping )
		{
			return;
		}

		float counterMovementThreshold = 0.16f;
		float movementZeroThreshold = 0.01f;


		ApplyCounterMomentum(movementInputX, movementInputY, movementMagnitude, movementZeroThreshold, counterMovementThreshold);
		CapMovementSpeed(_walkSpeed);
	}

	private void ApplyCounterMomentum(float movementInputX, float movementInputY, Vector2 movementMagnitude, float movementZeroThreshold, float counterMovementThreshold) 
	{
		if (ShouldApplyCounterMomentum(movementMagnitude.x, movementInputX, movementZeroThreshold)) 
		{
			_rigidbody.AddForce(_pivot.transform.right * (_moveSpeed * Time.deltaTime * -movementMagnitude.x * counterMovementThreshold));
		}

		if (ShouldApplyCounterMomentum(movementMagnitude.y, movementInputY, movementZeroThreshold))
		{
			_rigidbody.AddForce(_pivot.transform.forward * (_moveSpeed * Time.deltaTime * -movementMagnitude.y * counterMovementThreshold));
		}
	}

	private bool ShouldApplyCounterMomentum(float movementMagnitude, float movementInput, float movementZeroThreshold)
	{
		return Math.Abs(movementMagnitude) > movementZeroThreshold && (Math.Abs(movementInput) < 0.05f || (movementMagnitude < -movementZeroThreshold && movementInput > 0f) || (movementMagnitude > movementZeroThreshold && movementInput < 0f));
	}

	private void CapMovementSpeed(float speedCap)
	{
		var currentVelocity = _rigidbody.velocity;
		if (Mathf.Sqrt(Mathf.Pow(currentVelocity.x, 2f) + Mathf.Pow(currentVelocity.z, 2f)) > speedCap) 
		{
			float yVelocity = currentVelocity.y;
			Vector3 cappedVelocity = currentVelocity.normalized * speedCap;
			_rigidbody.velocity = new Vector3(cappedVelocity.x, yVelocity, cappedVelocity.z);
		}
	}


	private Vector2 FindVelRelativeToLook()
	{
		float current = _pivot.transform.eulerAngles.y;
		var velocity = _rigidbody.velocity;
		float target = Mathf.Atan2(velocity.x, velocity.z) * 57.29578f;
		float angle = Mathf.DeltaAngle(current, target);
		float inverseAngle = 90f - angle;
		float magnitude = _rigidbody.velocity.magnitude;
		return new Vector2(y: magnitude * Mathf.Cos(angle * ((float)Math.PI / 180f)), x: magnitude * Mathf.Cos(inverseAngle * ((float)Math.PI / 180f)));
	}

	private void FindWallRunRotation()
	{
		if (!_wallRunning)
		{
			_wallRunRotation = 0f;
			return;
		}

		var cameraRotation = _cameraHolder.transform.rotation;
		_ = new Vector3(0f, cameraRotation.y, 0f).normalized;
		float current = cameraRotation.eulerAngles.y;
		float angle = Vector3.SignedAngle(new Vector3(0f, 0f, 1f), _wallNormalVector, Vector3.up);
		float deltaAngle = Mathf.DeltaAngle(current, angle);
		_wallRunRotation = (0f - deltaAngle / 90f) * 15f;
		if (!_readyToWallrun)
		{
			return;
		}
		if ((Mathf.Abs(_wallRunRotation) < 4f && _y > 0f && Math.Abs(_x) < 0.1f) || (Mathf.Abs(_wallRunRotation) > 22f && _y < 0f && Math.Abs(_x) < 0.1f))
		{
			if (!_cancelling)
			{
				_cancelling = true;
				CancelInvoke("CancelWallrun");
				Invoke("CancelWallrun", 0.2f);
			}
		}
		else
		{
			_cancelling = false;
			CancelInvoke("CancelWallrun");
		}
	}

	private void CancelWallrun()
	{
		MonoBehaviour.print("cancelled");
		Invoke("GetReadyToWallrun", 0.1f);
		_rigidbody.AddForce(_wallNormalVector * 600f);
		_readyToWallrun = false;
	}

	private void GetReadyToWallrun()
	{
		_readyToWallrun = true;
	}

	private void WallRunning()
	{
		if (_wallRunning)
		{
			_rigidbody.AddForce(-_wallNormalVector * (Time.deltaTime * _moveSpeed));
			_rigidbody.AddForce(Vector3.up * (Time.deltaTime * _rigidbody.mass * 100f * _wallRunGravity));
		}
	}

	private bool IsFloor(Vector3 v)
	{
		return Vector3.Angle(Vector3.up, v) < _maxSlopeAngle;
	}


	private bool IsWall(Vector3 v)
	{
		return Math.Abs(90f - Vector3.Angle(Vector3.up, v)) < 0.1f;
	}


	private void StartWallRun(Vector3 normal)
	{
		if (!_grounded && _readyToWallrun)
		{
			_wallNormalVector = normal;
			float num = 20f;
			if (!_wallRunning)
			{
				var currentVelocity = _rigidbody.velocity;
				currentVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
				_rigidbody.velocity = currentVelocity;
				_rigidbody.AddForce(Vector3.up * num, ForceMode.Impulse);
			}
			_wallRunning = true;
		}
	}

	private void OnCollisionStay(Collision other)
	{
		int layer = other.gameObject.layer;
		if (_groundLayers != (_groundLayers | (1 << layer)))
		{
			return;
		}
		for (int i = 0; i < other.contactCount; i++)
		{
			Vector3 normal = other.contacts[i].normal;
			if (IsFloor(normal))
			{
				if (_wallRunning)
				{
					_wallRunning = false;
				}
				_grounded = true;
				_normalVector = normal;
				_cancellingGrounded = false;
				CancelInvoke(nameof(StopGrounded));
			}
			if (IsWall(normal) && layer == LayerMask.NameToLayer("Ground"))
			{
				StartWallRun(normal);
				_cancellingWall = false;
				CancelInvoke(nameof(StopWall));
			}
		}
		float num = 3f;
		if (!_cancellingGrounded)
		{
			_cancellingGrounded = true;
			Invoke(nameof(StopGrounded), Time.deltaTime * num);
		}
		if (!_cancellingWall)
		{
			_cancellingWall = true;
			Invoke(nameof(StopWall), Time.deltaTime * num);
		}
	}

	private void StopGrounded()
	{
		_grounded = false;
	}

	private void StopWall()
	{
		_wallRunning = false;
	}
}
