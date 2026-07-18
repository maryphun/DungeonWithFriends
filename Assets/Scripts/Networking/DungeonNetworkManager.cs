using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DungeonConnectionState
{
    Offline,
    StartingHost,
    Connecting,
    Connected,
    Disconnected,
    Failed
}

public class DungeonNetworkManager : NetworkManager
{
    public const int MaxSessionPlayers = 4;
    public const string MainMenuSceneName = "Main Menu";
    public const string MatchmakingSceneName = "MatchmakingLobby";
    public const string CharacterCreationSceneName = "CharacterCreation";
    public const string DefaultAddress = "localhost";

    private static readonly PlayerColor[] SlotOrder =
    {
        PlayerColor.Blue,
        PlayerColor.Red,
        PlayerColor.Green,
        PlayerColor.Purple
    };

    [Header("Dungeon Lobby")]
    [SerializeField] private GameObject sessionPlayerPrefab;
    [SerializeField] private int minimumPlayersToStart = 1;
    [SerializeField] private ushort runtimePort = 7777;

    private readonly List<NetworkSessionPlayer> serverSessionPlayers = new List<NetworkSessionPlayer>();
    private DungeonConnectionState connectionState = DungeonConnectionState.Offline;
    private bool clientJoinInProgress;

    public static event Action LobbyChanged;
    public static event Action<DungeonConnectionState, string> ConnectionStateChanged;

    public static DungeonNetworkManager Active => singleton as DungeonNetworkManager;
    public static IReadOnlyList<PlayerColor> LobbySlotOrder => SlotOrder;

    public DungeonConnectionState ConnectionState => connectionState;

    public static DungeonNetworkManager EnsureInstance()
    {
        DungeonNetworkManager existing = Active;
        if (existing != null)
        {
            existing.ConfigureDefaults();
            return existing;
        }

        if (singleton != null)
        {
            Debug.LogError("A NetworkManager already exists, but it is not DungeonNetworkManager.");
            return null;
        }

        GameObject networkManagerObject = new GameObject("Dungeon Network Manager");
        networkManagerObject.AddComponent<kcp2k.KcpTransport>();
        DungeonNetworkManager manager = networkManagerObject.AddComponent<DungeonNetworkManager>();
        manager.ConfigureDefaults();
        return manager;
    }

    public override void Awake()
    {
        ConfigureDefaults();
        base.Awake();
    }

    public void StartLocalHost()
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            return;
        }

        ConfigureDefaults();
        SetConnectionState(DungeonConnectionState.StartingHost, "Starting host.");
        StartHost();
    }

    public void JoinLocalGame(string address)
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            return;
        }

        ConfigureDefaults();
        networkAddress = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Trim();
        clientJoinInProgress = true;
        SetConnectionState(DungeonConnectionState.Connecting, $"Connecting to {networkAddress}.");
        StartClient();
    }

    public void LeaveSession()
    {
        clientJoinInProgress = false;

        if (NetworkServer.active && NetworkClient.isConnected)
        {
            StopHost();
            return;
        }

        if (NetworkClient.active)
        {
            StopClient();
            return;
        }

        if (NetworkServer.active)
        {
            StopServer();
            return;
        }

        SetConnectionState(DungeonConnectionState.Offline, "Offline.");

        if (SceneManager.GetActiveScene().name != MainMenuSceneName)
        {
            SceneManager.LoadScene(MainMenuSceneName);
        }
    }

    public void SetLocalLobbyReady(bool ready)
    {
        NetworkSessionPlayer localPlayer = NetworkSessionPlayer.LocalPlayer;
        if (localPlayer == null)
        {
            return;
        }

        localPlayer.CmdSetLobbyReady(ready);
    }

    public void RequestStartCharacterCreation()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("Only the host can start character creation.");
            return;
        }

        ServerTryStartCharacterCreation();
    }

    public void RequestKickSlot(int slotIndex)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("Only the host can kick lobby players.");
            return;
        }

        NetworkSessionPlayer player = FindServerPlayerInSlot(slotIndex);
        if (player == null || player.IsHost || player.connectionToClient == null)
        {
            return;
        }

        player.connectionToClient.Disconnect();
    }

    public void RequestTransferHost(int slotIndex)
    {
        Debug.LogWarning("Host transfer is intentionally disabled for this local lobby phase.");
    }

    public bool CanStartCharacterCreation(out string reason)
    {
        if (!NetworkServer.active)
        {
            reason = "Only the host can start the game.";
            return false;
        }

        if (serverSessionPlayers.Count < minimumPlayersToStart)
        {
            reason = $"Need at least {minimumPlayersToStart} player(s).";
            return false;
        }

        for (int i = 0; i < serverSessionPlayers.Count; i++)
        {
            NetworkSessionPlayer player = serverSessionPlayers[i];
            if (player == null || !player.HasAssignedColorSlot)
            {
                reason = "Every connected player needs a slot.";
                return false;
            }

            if (!player.LobbyReady)
            {
                reason = "Every connected player must be ready.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    [Server]
    public void ServerSetLobbyReady(NetworkSessionPlayer player, bool ready)
    {
        if (player == null || !serverSessionPlayers.Contains(player))
        {
            return;
        }

        player.ServerSetLobbyReady(ready);
        RaiseLobbyChanged();
    }

    public string GetDisplayAddress()
    {
        if (NetworkServer.active)
        {
            string localAddress = GetLocalIPv4Address();
            return string.IsNullOrWhiteSpace(localAddress) ? DefaultAddress : localAddress;
        }

        return string.IsNullOrWhiteSpace(networkAddress) ? DefaultAddress : networkAddress;
    }

    public static int GetSlotIndex(PlayerColor color)
    {
        for (int i = 0; i < SlotOrder.Length; i++)
        {
            if (SlotOrder[i] == color)
            {
                return i;
            }
        }

        return -1;
    }

    public override void OnStartHost()
    {
        base.OnStartHost();
        SetConnectionState(DungeonConnectionState.Connected, "Host started.");
        RaiseLobbyChanged();
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        SetConnectionState(DungeonConnectionState.Disconnected, "Host stopped.");
        RaiseLobbyChanged();
    }

    public override void OnStopServer()
    {
        serverSessionPlayers.Clear();
        base.OnStopServer();
        RaiseLobbyChanged();
    }

    public override void OnClientConnect()
    {
        clientJoinInProgress = false;
        SetConnectionState(DungeonConnectionState.Connected, "Connected.");
        base.OnClientConnect();
    }

    public override void OnClientDisconnect()
    {
        bool failedToJoin = clientJoinInProgress;
        clientJoinInProgress = false;

        base.OnClientDisconnect();
        SetConnectionState(failedToJoin ? DungeonConnectionState.Failed : DungeonConnectionState.Disconnected, failedToJoin ? "Failed to connect." : "Disconnected.");
        RaiseLobbyChanged();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (serverSessionPlayers.Count >= MaxSessionPlayers)
        {
            Debug.LogWarning("Lobby is full.");
            conn.Disconnect();
            return;
        }

        PlayerColor? colorSlot = GetFirstAvailableColorSlot();
        if (!colorSlot.HasValue)
        {
            Debug.LogWarning("No lobby slots are available.");
            conn.Disconnect();
            return;
        }

        if (playerPrefab == null)
        {
            ConfigureDefaults();
        }

        if (playerPrefab == null)
        {
            Debug.LogError("NetworkSessionPlayer prefab is missing from Resources.");
            conn.Disconnect();
            return;
        }

        GameObject playerObject = Instantiate(playerPrefab);
        playerObject.name = $"NetworkSessionPlayer [{colorSlot.Value}]";

        NetworkSessionPlayer sessionPlayer = playerObject.GetComponent<NetworkSessionPlayer>();
        if (sessionPlayer == null)
        {
            Debug.LogError("NetworkSessionPlayer prefab does not have a NetworkSessionPlayer component.");
            Destroy(playerObject);
            conn.Disconnect();
            return;
        }

        bool hostSlot = serverSessionPlayers.Count == 0;
        string displayName = hostSlot ? "Host" : $"Player {serverSessionPlayers.Count + 1}";
        sessionPlayer.ServerInitialize(conn.connectionId, colorSlot.Value, hostSlot, hostSlot, displayName);

        if (NetworkServer.AddPlayerForConnection(conn, playerObject))
        {
            serverSessionPlayers.Add(sessionPlayer);
            RaiseLobbyChanged();
        }
        else
        {
            Destroy(playerObject);
            conn.Disconnect();
        }
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        if (conn.identity != null)
        {
            NetworkSessionPlayer sessionPlayer = conn.identity.GetComponent<NetworkSessionPlayer>();
            if (sessionPlayer != null)
            {
                serverSessionPlayers.Remove(sessionPlayer);
            }
        }

        base.OnServerDisconnect(conn);
        RaiseLobbyChanged();
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        RaiseLobbyChanged();
    }

    private void ConfigureDefaults()
    {
        if (transport == null)
        {
            transport = GetComponent<Transport>();
        }

        if (transport is kcp2k.KcpTransport kcpTransport)
        {
            kcpTransport.Port = runtimePort;
        }

        if (sessionPlayerPrefab == null)
        {
            sessionPlayerPrefab = Resources.Load<GameObject>("NetworkSessionPlayer");
        }

        if (sessionPlayerPrefab != null)
        {
            playerPrefab = sessionPlayerPrefab;
        }

        dontDestroyOnLoad = true;
        runInBackground = true;
        autoCreatePlayer = true;
        maxConnections = MaxSessionPlayers;
        offlineScene = MainMenuSceneName;
        onlineScene = MatchmakingSceneName;
    }

    private bool ServerTryStartCharacterCreation()
    {
        if (!CanStartCharacterCreation(out string reason))
        {
            Debug.LogWarning(reason);
            RaiseLobbyChanged();
            return false;
        }

        for (int i = 0; i < serverSessionPlayers.Count; i++)
        {
            if (serverSessionPlayers[i] != null)
            {
                serverSessionPlayers[i].ServerResetCharacterCreationReady();
            }
        }

        ServerChangeScene(CharacterCreationSceneName);
        return true;
    }

    private PlayerColor? GetFirstAvailableColorSlot()
    {
        for (int i = 0; i < SlotOrder.Length; i++)
        {
            PlayerColor slot = SlotOrder[i];
            bool taken = false;

            for (int playerIndex = 0; playerIndex < serverSessionPlayers.Count; playerIndex++)
            {
                NetworkSessionPlayer player = serverSessionPlayers[playerIndex];
                if (player != null && player.HasAssignedColorSlot && player.ColorSlot == slot)
                {
                    taken = true;
                    break;
                }
            }

            if (!taken)
            {
                return slot;
            }
        }

        return null;
    }

    private NetworkSessionPlayer FindServerPlayerInSlot(int slotIndex)
    {
        for (int i = 0; i < serverSessionPlayers.Count; i++)
        {
            NetworkSessionPlayer player = serverSessionPlayers[i];
            if (player == null || !player.HasAssignedColorSlot)
            {
                continue;
            }

            if (GetSlotIndex(player.ColorSlot) == slotIndex)
            {
                return player;
            }
        }

        return null;
    }

    private static string GetLocalIPv4Address()
    {
        try
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress[] addresses = host.AddressList;

            for (int i = 0; i < addresses.Length; i++)
            {
                IPAddress address = addresses[i];
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                {
                    return address.ToString();
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not determine local IP address: {exception.Message}");
        }

        return string.Empty;
    }

    private void SetConnectionState(DungeonConnectionState state, string message)
    {
        connectionState = state;
        ConnectionStateChanged?.Invoke(state, message);
    }

    private static void RaiseLobbyChanged()
    {
        LobbyChanged?.Invoke();
    }
}
