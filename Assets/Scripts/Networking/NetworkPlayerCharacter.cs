using System;
using System.Collections.Generic;
using LayerLab.ArtMakerUnity;
using Mirror;
using UnityEngine;

public class NetworkPlayerCharacter : NetworkBehaviour
{
    private static readonly List<NetworkPlayerCharacter> ClientCharactersList = new List<NetworkPlayerCharacter>();

    [Header("References")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PartsManager partsManager;

    [Header("Visual Sync")]
    [SerializeField] private float visualSyncInterval = 0.05f;

    [SyncVar(hook = nameof(OnPlayerColorChanged))]
    private PlayerColor playerColor = PlayerColor.Blue;

    [SyncVar(hook = nameof(OnDisplayNameChanged))]
    private string displayName = "Player";

    [SyncVar(hook = nameof(OnCharacterDataJsonChanged))]
    private string characterDataJson = string.Empty;

    private CharacterSlotData characterData;

    [SyncVar(hook = nameof(OnWalkingChanged))]
    private bool walking;

    [SyncVar(hook = nameof(OnFacingRightChanged))]
    private bool facingRight = true;

    [SyncVar(hook = nameof(OnRightHandPivotLocalPositionChanged))]
    private Vector3 rightHandPivotLocalPosition;

    [SyncVar(hook = nameof(OnRightHandPivotLocalRotationChanged))]
    private Quaternion rightHandPivotLocalRotation = Quaternion.identity;

    private float nextVisualSyncTime;
    private bool hasAppliedCharacterData;

    public static event Action ClientCharactersChanged;
    public event Action AppearanceChanged;

    public static IReadOnlyList<NetworkPlayerCharacter> ClientCharacters => ClientCharactersList;
    public PlayerController Controller => controller;
    public PlayerColor PlayerColor => playerColor;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? playerColor.ToString() : displayName;
    public string CharacterDataJson => characterDataJson;
    public CharacterSlotData CharacterData
    {
        get
        {
            if (HasCharacterData(characterData))
            {
                return characterData;
            }

            if (CharacterSlotDataUtility.TryFromJson(characterDataJson, out CharacterSlotData parsedData))
            {
                characterData = parsedData;
                return characterData;
            }

            return default;
        }
    }
    public bool IsOwnedByLocalClient => isOwned;

    private void Awake()
    {
        ResolveReferences();
        controller?.SetLocalControlEnabled(false);
    }

    public override void OnStartClient()
    {
        if (!ClientCharactersList.Contains(this))
        {
            ClientCharactersList.Add(this);
        }

        ApplyGameplayConfiguration();
        RaiseClientCharactersChanged();
    }

    public override void OnStopClient()
    {
        ClientCharactersList.Remove(this);
        RaiseClientCharactersChanged();
    }

    public override void OnStartAuthority()
    {
        ApplyGameplayConfiguration();
        RaiseClientCharactersChanged();
    }

    public override void OnStopAuthority()
    {
        ApplyGameplayConfiguration();
        RaiseClientCharactersChanged();
    }

    private void Update()
    {
        if (!isOwned || controller == null || !controller.EnsureInitialized())
        {
            return;
        }

        if (Time.time < nextVisualSyncTime)
        {
            return;
        }

        nextVisualSyncTime = Time.time + Mathf.Max(0.01f, visualSyncInterval);

        bool nextWalking = controller.IsCurrentlyWalking;
        bool nextFacingRight = controller.IsFacingRight;
        Vector3 nextPivotPosition = controller.RightHandPivotLocalPosition;
        Quaternion nextPivotRotation = controller.RightHandPivotLocalRotation;

        if (!HasVisualStateChanged(nextWalking, nextFacingRight, nextPivotPosition, nextPivotRotation))
        {
            return;
        }

        if (isServer)
        {
            ServerSetVisualState(nextWalking, nextFacingRight, nextPivotPosition, nextPivotRotation);
        }
        else
        {
            CmdSetVisualState(nextWalking, nextFacingRight, nextPivotPosition, nextPivotRotation);
        }
    }

    [Server]
    public void ServerInitialize(NetworkSessionPlayer sessionPlayer, Vector3 spawnPosition)
    {
        if (sessionPlayer == null)
        {
            return;
        }

        transform.position = spawnPosition;
        playerColor = sessionPlayer.ColorSlot;
        displayName = sessionPlayer.DisplayName;
        characterData = sessionPlayer.CharacterData;
        characterDataJson = !string.IsNullOrWhiteSpace(sessionPlayer.CharacterDataJson)
            ? sessionPlayer.CharacterDataJson
            : CharacterSlotDataUtility.ToJson(characterData);
        gameObject.name = $"PlayerCharacter [{displayName}]";

        ApplyGameplayConfiguration();

        if (controller != null && controller.EnsureInitialized())
        {
            ServerSetVisualState(
                controller.IsCurrentlyWalking,
                controller.IsFacingRight,
                controller.RightHandPivotLocalPosition,
                controller.RightHandPivotLocalRotation
            );
        }
    }

    [Command]
    public void CmdSetVisualState(bool nextWalking, bool nextFacingRight, Vector3 nextPivotPosition, Quaternion nextPivotRotation)
    {
        ServerSetVisualState(nextWalking, nextFacingRight, nextPivotPosition, nextPivotRotation);
    }

    [Server]
    private void ServerSetVisualState(bool nextWalking, bool nextFacingRight, Vector3 nextPivotPosition, Quaternion nextPivotRotation)
    {
        walking = nextWalking;
        facingRight = nextFacingRight;
        rightHandPivotLocalPosition = nextPivotPosition;
        rightHandPivotLocalRotation = nextPivotRotation;
    }

    private bool HasVisualStateChanged(bool nextWalking, bool nextFacingRight, Vector3 nextPivotPosition, Quaternion nextPivotRotation)
    {
        return walking != nextWalking ||
               facingRight != nextFacingRight ||
               Vector3.SqrMagnitude(rightHandPivotLocalPosition - nextPivotPosition) > 0.0001f ||
               Quaternion.Angle(rightHandPivotLocalRotation, nextPivotRotation) > 0.1f;
    }

    private void OnPlayerColorChanged(PlayerColor oldValue, PlayerColor newValue)
    {
        ApplyGameplayConfiguration();
    }

    private void OnDisplayNameChanged(string oldValue, string newValue)
    {
        ApplyGameplayConfiguration();
    }

    private void OnCharacterDataJsonChanged(string oldValue, string newValue)
    {
        if (!CharacterSlotDataUtility.TryFromJson(newValue, out characterData))
        {
            characterData = default;
        }

        hasAppliedCharacterData = false;
        ApplyGameplayConfiguration();
    }

    private void OnWalkingChanged(bool oldValue, bool newValue)
    {
        ApplyRemoteVisualState();
    }

    private void OnFacingRightChanged(bool oldValue, bool newValue)
    {
        ApplyRemoteVisualState();
    }

    private void OnRightHandPivotLocalPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        ApplyRemoteVisualState();
    }

    private void OnRightHandPivotLocalRotationChanged(Quaternion oldValue, Quaternion newValue)
    {
        ApplyRemoteVisualState();
    }

    private void ApplyGameplayConfiguration()
    {
        ResolveReferences();

        if (controller == null)
        {
            return;
        }

        controller.ConfigureForGameplay(playerColor, DisplayName, isOwned);
        ApplyCharacterDataIfReady();
        ApplyRemoteVisualState();
        AppearanceChanged?.Invoke();
        RaiseClientCharactersChanged();
    }

    private void ApplyCharacterDataIfReady()
    {
        if (controller == null || hasAppliedCharacterData)
        {
            return;
        }

        if (!HasCharacterData(characterData) && CharacterSlotDataUtility.TryFromJson(characterDataJson, out CharacterSlotData parsedData))
        {
            characterData = parsedData;
        }

        if (!HasCharacterData(characterData))
        {
            return;
        }

        controller.ApplyCharacterData(characterData);
        hasAppliedCharacterData = true;
    }

    private void ApplyRemoteVisualState()
    {
        if (isOwned || controller == null)
        {
            return;
        }

        controller.ApplyRemoteVisualState(
            walking,
            facingRight,
            rightHandPivotLocalPosition,
            rightHandPivotLocalRotation
        );
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponent<PlayerController>();
        }

        if (partsManager == null)
        {
            partsManager = GetComponent<PartsManager>();
        }
    }

    private static bool HasCharacterData(CharacterSlotData data)
    {
        return !string.IsNullOrWhiteSpace(data.characterName) ||
               data.parts != null && data.parts.Length > 0 ||
               data.colors != null && data.colors.Length > 0 ||
               data.visibility != null && data.visibility.Length > 0;
    }

    private static void RaiseClientCharactersChanged()
    {
        ClientCharactersChanged?.Invoke();
    }
}
