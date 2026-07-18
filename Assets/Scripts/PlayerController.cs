using DG.Tweening;
using LayerLab.ArtMakerUnity;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform targetPlayer;
    public Transform TargetPlayer => targetPlayer;

    [SerializeField] private PlayerBodyParts parts;
    public PlayerBodyParts Parts => parts;

    public PartsManager PartsManager => partsManager;
    public string DisplayName => displayName;
    public bool IsLocalControlEnabled => localInputEnabled;
    public bool IsCurrentlyWalking => previousWalkingState;
    public bool IsFacingRight => facingRight;
    public Vector3 RightHandPivotLocalPosition => rightHandAimPivot != null ? rightHandAimPivot.localPosition : Vector3.zero;
    public Quaternion RightHandPivotLocalRotation => rightHandAimPivot != null ? rightHandAimPivot.localRotation : Quaternion.identity;

    [Header("Stats")]
    [SerializeField] public UnitStats stats;

    [Header("Settings")]
    [SerializeField] private PlayerColor playerColor = PlayerColor.Blue;
    public PlayerColor PlayerColor => playerColor;

    [Header("Control")]
    [SerializeField] private bool localInputEnabled = true;
    [SerializeField] private bool cameraFollowEnabled = true;

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

    private PartsManager partsManager;
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

    private string displayName = "Player";
    private bool facingRight = true;
    private bool previousWalkingState;
    private bool baseAnimationInitialized;
    private bool initialized;
    private bool isSwitchingSide;

    private PlayerActionBuffer nextActionBuffer = PlayerActionBuffer.None;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (!EnsureInitialized())
        {
            enabled = false;
        }
    }

    public void ConfigureForGameplay(PlayerColor assignedColor, string assignedName, bool allowLocalControl)
    {
        playerColor = assignedColor;
        displayName = string.IsNullOrWhiteSpace(assignedName) ? assignedColor.ToString() : assignedName;
        SetLocalControlEnabled(allowLocalControl);

        if (EnsureInitialized())
        {
            SetupShadowOutline(playerColor);
        }
    }

    public void SetLocalControlEnabled(bool allowLocalControl)
    {
        localInputEnabled = allowLocalControl;
        cameraFollowEnabled = allowLocalControl;

        if (allowLocalControl)
        {
            EnsureCameraAvailable();
        }
    }

    public bool EnsureInitialized()
    {
        if (initialized)
        {
            return !localInputEnabled && !cameraFollowEnabled || EnsureCameraAvailable();
        }

        ResolveReferences();

        if (targetPlayer == null)
        {
            Debug.LogError("Target Player has not been assigned.", this);
            return false;
        }

        rightHandAimPivot = targetPlayer.Find("RightHandPivot");

        if (rightHandAimPivot == null)
        {
            Debug.LogError("RightHandPivot could not be found under Target Player.", targetPlayer);
            return false;
        }

        rightHandPivotOriginalPosition = rightHandAimPivot.localPosition;
        animator = targetPlayer.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("Target Player does not have an Animator component.", targetPlayer);
            return false;
        }

        if ((localInputEnabled || cameraFollowEnabled) && !EnsureCameraAvailable())
        {
            return false;
        }

        originalPlayerScale = targetPlayer.localScale;
        facingRight = targetPlayer.localScale.x >= 0f;
        attackLayerIndex = animator.GetLayerIndex("Attack");

        if (attackLayerIndex >= 0)
        {
            animator.SetLayerWeight(attackLayerIndex, 1f);
        }
        else
        {
            Debug.LogWarning("Animator layer named 'Attack' was not found.", animator);
        }

        CharacterAnimationEvents animationEvents = animator.GetComponent<CharacterAnimationEvents>();

        if (animationEvents == null)
        {
            animationEvents = animator.gameObject.AddComponent<CharacterAnimationEvents>();
        }

        animationEvents.Setup(this);
        SetupShadowOutline(playerColor);
        initialized = true;
        return true;
    }

    public void ApplyCharacterData(CharacterSlotData data)
    {
        ResolveReferences();

        if (partsManager == null)
        {
            Debug.LogWarning("Cannot apply character data because this player has no PartsManager.", this);
            return;
        }

        partsManager.Init();
        partsManager.ApplyPresetItem(CharacterSlotDataUtility.ToPresetItem(data));
        ApplySpriteNameOverrides(data);
        ApplyVisibilitySelections(data);
        ApplyExclusiveEquipmentGroup(data, UICategory.HandRight);
        ApplyExclusiveEquipmentGroup(data, UICategory.HandLeft);
    }

    private void ApplySpriteNameOverrides(CharacterSlotData data)
    {
        if (data.parts == null)
        {
            return;
        }

        for (int i = 0; i < data.parts.Length; i++)
        {
            CharacterPartSelection selection = data.parts[i];
            if (!string.IsNullOrWhiteSpace(selection.spriteName))
            {
                partsManager.TryEquipPartBySpriteName(selection.type, selection.spriteName);
            }
        }
    }

    private void ApplyVisibilitySelections(CharacterSlotData data)
    {
        if (data.visibility == null)
        {
            return;
        }

        for (int i = 0; i < data.visibility.Length; i++)
        {
            PartsType type = data.visibility[i].type;
            if (type == PartsType.Arrow || type == PartsType.HelmetHair)
            {
                continue;
            }

            if (partsManager.CanToggle(type))
            {
                partsManager.ToggleParts(type, data.visibility[i].visible);
            }
        }
    }

    private void ApplyExclusiveEquipmentGroup(CharacterSlotData data, UICategory category)
    {
        PartsType[] groupTypes = UICategoryConfig.GetSubTypes(category);
        if (groupTypes.Length <= 1 || !TryGetVisibleGroupType(data, groupTypes, out PartsType visibleType))
        {
            return;
        }

        if (TryGetPartSelection(data, visibleType, out CharacterPartSelection visibleSelection))
        {
            if (string.IsNullOrWhiteSpace(visibleSelection.spriteName) ||
                !partsManager.TryEquipPartBySpriteName(visibleType, visibleSelection.spriteName))
            {
                if (visibleSelection.index >= 0)
                {
                    partsManager.EquipParts(visibleType, visibleSelection.index);
                }
            }
        }

        if (partsManager.CanToggle(visibleType))
        {
            partsManager.ToggleParts(visibleType, true);
        }

        for (int i = 0; i < groupTypes.Length; i++)
        {
            PartsType type = groupTypes[i];
            if (type == visibleType || !partsManager.CanToggle(type))
            {
                continue;
            }

            partsManager.ToggleParts(type, false);
        }
    }

    private static bool TryGetVisibleGroupType(CharacterSlotData data, PartsType[] groupTypes, out PartsType visibleType)
    {
        visibleType = default;
        if (data.visibility == null)
        {
            return false;
        }

        for (int i = 0; i < data.visibility.Length; i++)
        {
            if (!data.visibility[i].visible || !ContainsPartType(groupTypes, data.visibility[i].type))
            {
                continue;
            }

            visibleType = data.visibility[i].type;
            return true;
        }

        return false;
    }

    private static bool TryGetPartSelection(CharacterSlotData data, PartsType type, out CharacterPartSelection selection)
    {
        selection = default;
        if (data.parts == null)
        {
            return false;
        }

        for (int i = 0; i < data.parts.Length; i++)
        {
            if (data.parts[i].type != type)
            {
                continue;
            }

            selection = data.parts[i];
            return selection.index >= 0 || !string.IsNullOrWhiteSpace(selection.spriteName);
        }

        return false;
    }

    private static bool ContainsPartType(PartsType[] types, PartsType type)
    {
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == type)
            {
                return true;
            }
        }

        return false;
    }

    public void ApplyRemoteVisualState(bool walking, bool remoteFacingRight, Vector3 pivotLocalPosition, Quaternion pivotLocalRotation)
    {
        if (localInputEnabled || !EnsureInitialized())
        {
            return;
        }

        facingRight = remoteFacingRight;
        ApplyFacingScale(facingRight, false);
        rightHandAimPivot.localPosition = pivotLocalPosition;
        rightHandAimPivot.localRotation = pivotLocalRotation;
        UpdateBaseAnimation(walking);
    }

    private void Update()
    {
        if (!localInputEnabled || !EnsureInitialized())
        {
            return;
        }

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

        if (movementInput.sqrMagnitude > 1f)
        {
            movementInput.Normalize();
        }

        float movementSpeed = Mathf.Max(0f, stats.movementSpeed);
        targetPlayer.position += new Vector3(movementInput.x, movementInput.y, 0f) * movementSpeed * Time.deltaTime;

        return movementInput.sqrMagnitude > 0.001f;
    }

    private void UpdateFacing()
    {
        if (IsAttacking()) return;

        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = Mathf.Abs(targetPlayer.position.z - mainCamera.transform.position.z);

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 directionFromPlayer = mouseWorldPosition - targetPlayer.position;

        if (directionFromPlayer.x > facingDeadZone)
        {
            facingRight = true;
        }
        else if (directionFromPlayer.x < -facingDeadZone)
        {
            facingRight = false;
        }

        ApplyFacingScale(facingRight, true);
    }

    private void ApplyFacingScale(bool targetFacingRight, bool animate)
    {
        float facingSign = spriteFacesRight
            ? targetFacingRight ? 1f : -1f
            : targetFacingRight ? -1f : 1f;

        float targetScaleX = Mathf.Abs(originalPlayerScale.x) * facingSign;

        if (Mathf.Approximately(targetPlayer.localScale.x, targetScaleX))
        {
            return;
        }

        if (!animate)
        {
            Vector3 scale = targetPlayer.localScale;
            targetPlayer.localScale = new Vector3(targetScaleX, scale.y, scale.z);
            return;
        }

        if (!isSwitchingSide)
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

        mouseScreenPosition.z = Mathf.Abs(targetPlayer.position.z - mainCamera.transform.position.z);
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector3 localMousePosition = aimCoordinateSpace.InverseTransformPoint(mouseWorldPosition);

        Vector2 baseAimDirection = new Vector2(
            localMousePosition.x - rightHandPivotOriginalPosition.x,
            localMousePosition.y - rightHandPivotOriginalPosition.y
        );

        if (baseAimDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 normalizedDirection = baseAimDirection.normalized;
        float upwardAmount = Mathf.Clamp01(normalizedDirection.y);
        upwardAmount = Mathf.SmoothStep(0f, 1f, upwardAmount);

        float targetOffset = upwardAmount * upwardHandOffset;
        float targetHorizontalOffset = normalizedDirection.x * upwardAmount * upwardHorizontalOffset;

        currentUpwardOffset = Mathf.SmoothDamp(currentUpwardOffset, targetOffset, ref upwardOffsetVelocity, upwardOffsetSmoothTime);
        currentHorizontalOffset = Mathf.SmoothDamp(currentHorizontalOffset, targetHorizontalOffset, ref horizontalOffsetVelocity, upwardOffsetSmoothTime);

        rightHandAimPivot.localPosition = rightHandPivotOriginalPosition + new Vector3(currentHorizontalOffset, currentUpwardOffset, 0f);

        Vector2 finalAimDirection = new Vector2(
            localMousePosition.x - rightHandAimPivot.localPosition.x,
            localMousePosition.y - rightHandAimPivot.localPosition.y
        );

        float localAimAngle = Mathf.Atan2(finalAimDirection.y, finalAimDirection.x) * Mathf.Rad2Deg;
        rightHandAimPivot.localRotation = Quaternion.Euler(0f, 0f, localAimAngle + angleOffset);
    }

    private void UpdateBaseAnimation(bool isWalking)
    {
        if (baseAnimationInitialized && previousWalkingState == isWalking)
        {
            return;
        }

        baseAnimationInitialized = true;
        previousWalkingState = isWalking;

        string stateName = isWalking ? "Run" : "Idle";
        animator.CrossFadeInFixedTime(stateName, 0.05f, 0);
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
            Debug.LogWarning("Attack1 cannot be played because the Attack animation layer was not found.", animator);
            return;
        }

        if (!IsAttacking())
        {
            animator.Play("Attack1", attackLayerIndex, 0f);
        }
        else
        {
            nextActionBuffer = PlayerActionBuffer.Attack;
        }
    }

    private bool IsAttacking()
    {
        return attackLayerIndex >= 0 && !animator.GetCurrentAnimatorStateInfo(attackLayerIndex).IsName("Nothing");
    }

    private void LateUpdate()
    {
        if (!cameraFollowEnabled || targetPlayer == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        if (mainCamera == null)
        {
            return;
        }

        Vector3 targetCameraPosition = new Vector3(targetPlayer.position.x, targetPlayer.position.y, mainCamera.transform.position.z);
        mainCamera.transform.position = Vector3.SmoothDamp(mainCamera.transform.position, targetCameraPosition, ref cameraVelocity, cameraSmoothTime);
    }

    private bool EnsureCameraAvailable()
    {
        if (mainCamera != null)
        {
            return true;
        }

        mainCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (mainCamera != null)
        {
            return true;
        }

        Debug.LogError("No camera could be found.", this);
        return false;
    }

    private void SetupShadowOutline(PlayerColor outlineColor)
    {
        if (parts.shadow == null || parts.shadow.material == null)
        {
            return;
        }

        parts.shadow.material.SetColor("_OutlineColor", PlayerColorUtility.ToUnityColor(outlineColor));
    }

    private void ResolveReferences()
    {
        if (targetPlayer == null)
        {
            targetPlayer = transform;
        }

        partsManager = targetPlayer.GetComponent<PartsManager>();
        if (partsManager == null)
        {
            partsManager = targetPlayer.GetComponentInChildren<PartsManager>(true);
        }

        parts.body = parts.body != null ? parts.body : FindRenderer("Body");
        parts.chest = parts.chest != null ? parts.chest : FindRenderer("Chest");
        parts.head = parts.head != null ? parts.head : FindRenderer("Head");
        parts.eye = parts.eye != null ? parts.eye : FindRenderer("Eye");
        parts.hair = parts.hair != null ? parts.hair : FindRenderer("Hair");
        parts.hair_helmet = parts.hair_helmet != null ? parts.hair_helmet : FindRenderer("Hair_Helmet");
        parts.helmet = parts.helmet != null ? parts.helmet : FindRenderer("Helmet");
        parts.beard = parts.beard != null ? parts.beard : FindRenderer("Beard");
        parts.shadow = parts.shadow != null ? parts.shadow : FindRenderer("Shadow");
    }

    private SpriteRenderer FindRenderer(string childName)
    {
        if (targetPlayer == null)
        {
            return null;
        }

        Transform child = FindDeepChild(targetPlayer, childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
