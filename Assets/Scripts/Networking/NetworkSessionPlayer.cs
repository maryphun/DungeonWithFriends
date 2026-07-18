using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class NetworkSessionPlayer : NetworkBehaviour
{
    private static readonly List<NetworkSessionPlayer> ClientPlayersList = new List<NetworkSessionPlayer>();

    [SyncVar(hook = nameof(OnHasAssignedColorSlotChanged))]
    private bool hasAssignedColorSlot;

    [SyncVar(hook = nameof(OnColorSlotChanged))]
    private PlayerColor colorSlot;

    [SyncVar(hook = nameof(OnLobbyReadyChanged))]
    private bool lobbyReady;

    [SyncVar(hook = nameof(OnCharacterCreationReadyChanged))]
    private bool characterCreationReady;

    [SyncVar(hook = nameof(OnIsHostChanged))]
    private bool isHost;

    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    private string playerName = string.Empty;

    [SyncVar(hook = nameof(OnConnectionIdChanged))]
    private int connectionId = -1;

    public static event Action ClientStateChanged;

    public static IReadOnlyList<NetworkSessionPlayer> ClientPlayers => ClientPlayersList;
    public static NetworkSessionPlayer LocalPlayer { get; private set; }

    public bool HasAssignedColorSlot => hasAssignedColorSlot;
    public PlayerColor ColorSlot => colorSlot;
    public bool LobbyReady => lobbyReady;
    public bool CharacterCreationReady => characterCreationReady;
    public bool IsHost => isHost;
    public string PlayerName => playerName;
    public int ConnectionId => connectionId;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                return playerName;
            }

            return hasAssignedColorSlot ? colorSlot.ToString() : "Player";
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public override void OnStartClient()
    {
        if (!ClientPlayersList.Contains(this))
        {
            ClientPlayersList.Add(this);
        }

        NotifyClientStateChanged();
    }

    public override void OnStopClient()
    {
        ClientPlayersList.Remove(this);

        if (LocalPlayer == this)
        {
            LocalPlayer = null;
        }

        NotifyClientStateChanged();
    }

    public override void OnStartLocalPlayer()
    {
        LocalPlayer = this;
        NotifyClientStateChanged();
    }

    [Server]
    public void ServerInitialize(int playerConnectionId, PlayerColor assignedColorSlot, bool hostSlot, bool readyByDefault, string displayName)
    {
        connectionId = playerConnectionId;
        colorSlot = assignedColorSlot;
        hasAssignedColorSlot = true;
        isHost = hostSlot;
        lobbyReady = readyByDefault;
        characterCreationReady = false;
        playerName = displayName;
    }

    [Server]
    public void ServerSetLobbyReady(bool ready)
    {
        lobbyReady = isHost || ready;
    }

    [Server]
    public void ServerResetCharacterCreationReady()
    {
        characterCreationReady = false;
    }

    [Command]
    public void CmdSetLobbyReady(bool ready)
    {
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager == null)
        {
            return;
        }

        manager.ServerSetLobbyReady(this, ready);
    }

    private void OnHasAssignedColorSlotChanged(bool oldValue, bool newValue)
    {
        NotifyClientStateChanged();
    }

    private void OnColorSlotChanged(PlayerColor oldValue, PlayerColor newValue)
    {
        NotifyClientStateChanged();
    }

    private void OnLobbyReadyChanged(bool oldValue, bool newValue)
    {
        NotifyClientStateChanged();
    }

    private void OnCharacterCreationReadyChanged(bool oldValue, bool newValue)
    {
        NotifyClientStateChanged();
    }

    private void OnIsHostChanged(bool oldValue, bool newValue)
    {
        NotifyClientStateChanged();
    }

    private void OnPlayerNameChanged(string oldValue, string newValue)
    {
        NotifyClientStateChanged();
    }

    private void OnConnectionIdChanged(int oldValue, int newValue)
    {
        NotifyClientStateChanged();
    }

    private static void NotifyClientStateChanged()
    {
        ClientStateChanged?.Invoke();
    }
}
