using UnityEngine;
using DG.Tweening;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform targetPlayer;
    public Transform TargetPlayer => targetPlayer;
    [SerializeField] private PlayerBodyParts parts;
    public PlayerBodyParts Parts => parts;

    [Header("Stats")]
    [SerializeField] public UnitStats stats;

    [Header("Settings")]
    [SerializeField] private PlayerColor playerColor = PlayerColor.Blue;
    public PlayerColor PlayerColor => playerColor;

    [Header("Character Facing")]
    [Tooltip("Enable this when the original character artwork faces right.")]
    [SerializeField] private bool spriteFacesRight = true;

    [Tooltip(
        "The character keeps its previous facing direction while the mouse " +
        "is almost directly above or below it."
    )]
    [SerializeField] private float facingDeadZone = 0.1f;

    [Header("Aim Settings")]
    [Tooltip("Use 0 when the weapon artwork naturally points right.")]
    [SerializeField] private float angleOffset = 0f;

    [Header("Upward Aim Offset")]
    [SerializeField] private float upwardHandOffset = 0.2f;
    [SerializeField] private float upwardHorizontalOffset = 0.15f;
    [SerializeField] private float upwardOffsetSmoothTime = 0.08f;

    [Header("Camera Follow")]
    [SerializeField] private float cameraSmoothTime = 0.25f;

    private Animator animator;
    private Camera mainCamera;
    private Transform rightHandAimPivot;

    private Vector3 originalPlayerScale;
    private Vector3 cameraVelocity;

    private Vector3 rightHandPivotOriginalPosition;
    private float currentUpwardOffset;
    private float upwardOffsetVelocity;
    private float currentHorizontalOffset;
    private float horizontalOffsetVelocity;

    private int attackLayerIndex = -1;

    private bool facingRight = true;
    private bool previousWalkingState;
    private bool baseAnimationInitialized;
    private bool isSwitchingSide = false;

    private PlayerActionBuffer nextActionBuffer = PlayerActionBuffer.None;

    private void Start()
    {
        if (targetPlayer == null)
        {
            Debug.LogError(
                "Target Player has not been assigned.",
                this
            );

            enabled = false;
            return;
        }

        rightHandAimPivot = targetPlayer.Find("RightHandPivot");

        if (rightHandAimPivot == null)
        {
            Debug.LogError(
                "RightHandPivot could not be found under Target Player.",
                targetPlayer
            );

            enabled = false;
            return;
        }

        rightHandPivotOriginalPosition = rightHandAimPivot.localPosition;

        animator = targetPlayer.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError(
                "Target Player does not have an Animator component.",
                targetPlayer
            );

            enabled = false;
            return;
        }

        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            mainCamera =
                FindFirstObjectByType<Camera>();
        }

        if (mainCamera == null)
        {
            Debug.LogError(
                "No camera could be found.",
                this
            );

            enabled = false;
            return;
        }

        originalPlayerScale = targetPlayer.localScale;

        attackLayerIndex = animator.GetLayerIndex("Attack");

        if (attackLayerIndex >= 0)
        {
            animator.SetLayerWeight(
                attackLayerIndex,
                1f
            );
        }
        else
        {
            Debug.LogWarning(
                "Animator layer named 'Attack' was not found.",
                animator
            );
        }

        CharacterAnimationEvents animationEvents = animator.GetComponent<CharacterAnimationEvents>();

        if (animationEvents == null)
        {
            animationEvents = animator.gameObject.AddComponent<CharacterAnimationEvents>();
        }

        animationEvents.Setup(this);

        // setup color
        SetupShadowOutline(playerColor);
    }

    private void Update()
    {
        bool isWalking = UpdateMovement();

        UpdateFacing();
        UpdateRightArmPivotAim();
        UpdateBaseAnimation(isWalking);
        UpdateAttack();
    }

    private bool UpdateMovement()
    {
        Vector2 movementInput = Vector2.zero;

        if (Input.GetKey(KeyCode.A))
        {
            movementInput.x -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            movementInput.x += 1f;
        }

        if (Input.GetKey(KeyCode.W))
        {
            movementInput.y += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            movementInput.y -= 1f;
        }

        // Prevent diagonal movement from being faster.
        if (movementInput.sqrMagnitude > 1f)
        {
            movementInput.Normalize();
        }

        targetPlayer.position +=
            new Vector3(
                movementInput.x,
                movementInput.y,
                0f
            ) * stats.movementSpeed * Time.deltaTime;

        return movementInput.sqrMagnitude > 0.001f;
    }

    private void UpdateFacing()
    {
        if (IsAttacking()) return;

        Vector3 mouseScreenPosition = Input.mousePosition;

        mouseScreenPosition.z = Mathf.Abs(
            targetPlayer.position.z -
            mainCamera.transform.position.z
        );

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        Vector2 directionFromPlayer = mouseWorldPosition - targetPlayer.position;

        // Keep the previous facing direction when aiming almost vertically.
        if (directionFromPlayer.x > facingDeadZone)
        {
            facingRight = true;
        }
        else if (directionFromPlayer.x < -facingDeadZone)
        {
            facingRight = false;
        }

        float facingSign;

        if (spriteFacesRight)
        {
            facingSign = facingRight ? 1f : -1f;
        }
        else
        {
            facingSign = facingRight ? -1f : 1f;
        }

        float targetScaleX = Mathf.Abs(originalPlayerScale.x) * facingSign;

        if (!Mathf.Approximately(targetPlayer.localScale.x, targetScaleX) && !isSwitchingSide)
        {
            isSwitchingSide = true;

            targetPlayer.DOScaleX(targetScaleX, 0.05f).OnComplete(() => isSwitchingSide = false);
        }
    }

    private void UpdateRightArmPivotAim()
    {
        if (IsAttacking()) return;

        Vector3 mouseScreenPosition = Input.mousePosition;

        Transform aimCoordinateSpace = rightHandAimPivot.parent;

        mouseScreenPosition.z = Mathf.Abs(
            targetPlayer.position.z -
            mainCamera.transform.position.z
        );

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        Vector3 localMousePosition = aimCoordinateSpace.InverseTransformPoint(mouseWorldPosition);

        /*
         * First calculate the general aim direction from the pivot's
         * original position. This prevents the offset from affecting
         * the amount of offset being calculated.
         */
        Vector2 baseAimDirection = new Vector2(
            localMousePosition.x -
            rightHandPivotOriginalPosition.x,

            localMousePosition.y -
            rightHandPivotOriginalPosition.y
        );

        if (baseAimDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 normalizedDirection =
            baseAimDirection.normalized;

        /*
         * 0 when aiming horizontally or downward.
         * 1 when aiming directly upward.
         */
        float upwardAmount = Mathf.Clamp01(normalizedDirection.y);

        // Makes the transition softer near horizontal directions.
        upwardAmount = Mathf.SmoothStep(0f, 1f, upwardAmount);

        float targetOffset = upwardAmount * upwardHandOffset;

        float targetHorizontalOffset =
            normalizedDirection.x *
            upwardAmount *
            upwardHorizontalOffset;

        currentUpwardOffset =
            Mathf.SmoothDamp(
                currentUpwardOffset,
                targetOffset,
                ref upwardOffsetVelocity,
                upwardOffsetSmoothTime
            );

        currentHorizontalOffset =
            Mathf.SmoothDamp(
                currentHorizontalOffset,
                targetHorizontalOffset,
                ref horizontalOffsetVelocity,
                upwardOffsetSmoothTime
            );

        rightHandAimPivot.localPosition =
            rightHandPivotOriginalPosition +
            new Vector3(
                currentHorizontalOffset,
                currentUpwardOffset,
                0f
            );

        /*
         * Recalculate the direction using the pivot's newly adjusted
         * position so the weapon still points precisely at the mouse.
         */
        Vector2 finalAimDirection = new Vector2(
            localMousePosition.x -
            rightHandAimPivot.localPosition.x,

            localMousePosition.y -
            rightHandAimPivot.localPosition.y
        );

        float localAimAngle =
            Mathf.Atan2(
                finalAimDirection.y,
                finalAimDirection.x
            ) * Mathf.Rad2Deg;

        rightHandAimPivot.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                localAimAngle + angleOffset
            );
    }

    private void UpdateBaseAnimation(bool isWalking)
    {
        // Do not restart Idle or Run every frame.
        if (
            baseAnimationInitialized &&
            previousWalkingState == isWalking
        )
        {
            return;
        }

        baseAnimationInitialized = true;
        previousWalkingState = isWalking;

        string stateName =
            isWalking ? "Run" : "Idle";

        animator.CrossFadeInFixedTime(
            stateName,
            0.05f,
            0
        );
    }

    private void UpdateAttack()
    {
        if (!Input.GetMouseButtonDown(0) && (nextActionBuffer != PlayerActionBuffer.Attack || isSwitchingSide))
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0) && nextActionBuffer == PlayerActionBuffer.Attack)
        {
            nextActionBuffer = PlayerActionBuffer.None;
        }

        if (attackLayerIndex < 0)
        {
            Debug.LogWarning(
                "Attack1 cannot be played because the " +
                "Attack animation layer was not found.",
                animator
            );

            return;
        }

        if (!IsAttacking())
        {
            animator.Play("Attack1", attackLayerIndex, 0f);
        }
        else
        {
            // save or override the input for later
            nextActionBuffer = PlayerActionBuffer.Attack;
        }
    }

    private bool IsAttacking()
    {
        return !animator.GetCurrentAnimatorStateInfo(attackLayerIndex).IsName("Nothing");
    }

    private void LateUpdate()
    {
        if (targetPlayer == null || mainCamera == null)
        {
            return;
        }

        Vector3 targetCameraPosition =
            new Vector3(
                targetPlayer.position.x,
                targetPlayer.position.y,
                mainCamera.transform.position.z
            );

        mainCamera.transform.position =
            Vector3.SmoothDamp(
                mainCamera.transform.position,
                targetCameraPosition,
                ref cameraVelocity,
                cameraSmoothTime
            );
    }

    private void SetupShadowOutline(PlayerColor playerColor)
    {
        Color color = Color.white;
        switch (playerColor)
        {
            case PlayerColor.Green:
                color = Color.green;
                break;
            case PlayerColor.Purple:
                color = Color.purple;
                break;
            case PlayerColor.Red:
                color = Color.red;
                break;
            case PlayerColor.Blue:
            default:
                color = Color.blue;
                break;
        }

        parts.shadow.material.SetColor("_OutlineColor", color);
    }
}