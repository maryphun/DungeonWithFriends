using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using LayerLab.ArtMakerUnity;
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
    public const string GameSceneName = "Game";
    public const string DefaultAddress = "localhost";

    private const int MaxCharacterNameLength = 16;
    private const int MaxPartEntries = 32;
    private const int MaxColorEntries = 8;
    private const int MaxVisibilityEntries = 32;
    private const float GameSceneTransitionDelay = 1.5f;

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
    private Coroutine gameSceneTransitionCoroutine;

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

    public int GetSessionPlayerCount()
    {
        if (NetworkServer.active)
        {
            return serverSessionPlayers.Count;
        }

        return NetworkSessionPlayer.ClientPlayers.Count;
    }

    public int GetCharacterCreationReadyCount()
    {
        int readyCount = 0;

        if (NetworkServer.active)
        {
            for (int i = 0; i < serverSessionPlayers.Count; i++)
            {
                if (serverSessionPlayers[i] != null && serverSessionPlayers[i].CharacterCreationReady)
                {
                    readyCount++;
                }
            }

            return readyCount;
        }

        for (int i = 0; i < NetworkSessionPlayer.ClientPlayers.Count; i++)
        {
            NetworkSessionPlayer player = NetworkSessionPlayer.ClientPlayers[i];
            if (player != null && player.CharacterCreationReady)
            {
                readyCount++;
            }
        }

        return readyCount;
    }

    public bool IsCharacterCreationComplete()
    {
        int playerCount = GetSessionPlayerCount();
        return playerCount > 0 && GetCharacterCreationReadyCount() >= playerCount;
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

    [Server]
    public void ServerSubmitCharacter(NetworkSessionPlayer player, CharacterSlotData data)
    {
        if (!ValidateCharacterSubmission(player, data, out string sanitizedName, out string reason))
        {
            Debug.LogWarning(reason);
            return;
        }

        CharacterSlotData acceptedData = NormalizeCharacterSubmission(player, data, sanitizedName);
        player.ServerSubmitCharacter(acceptedData, sanitizedName);
        RaiseLobbyChanged();
        ServerTryStartGameWhenCharactersReady();
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
        if (gameSceneTransitionCoroutine != null)
        {
            StopCoroutine(gameSceneTransitionCoroutine);
            gameSceneTransitionCoroutine = null;
        }

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

        if (NetworkServer.active && SceneManager.GetActiveScene().name == CharacterCreationSceneName && serverSessionPlayers.Count > 0)
        {
            ServerTryStartGameWhenCharactersReady();
        }
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

        if (gameSceneTransitionCoroutine != null)
        {
            StopCoroutine(gameSceneTransitionCoroutine);
            gameSceneTransitionCoroutine = null;
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

    private bool CanStartGameAfterCharacterCreation(out string reason)
    {
        if (!NetworkServer.active)
        {
            reason = "Only the server can start the game scene.";
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

            if (!player.CharacterCreationReady)
            {
                reason = "Every connected player must finish character creation.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private bool ServerTryStartGameWhenCharactersReady()
    {
        if (!CanStartGameAfterCharacterCreation(out _))
        {
            return false;
        }

        if (gameSceneTransitionCoroutine == null)
        {
            gameSceneTransitionCoroutine = StartCoroutine(ServerStartGameAfterDelay());
        }

        return true;
    }

    private IEnumerator ServerStartGameAfterDelay()
    {
        yield return new WaitForSeconds(GameSceneTransitionDelay);
        gameSceneTransitionCoroutine = null;

        if (!CanStartGameAfterCharacterCreation(out string reason))
        {
            Debug.LogWarning(reason);
            RaiseLobbyChanged();
            yield break;
        }

        ServerChangeScene(GameSceneName);
    }

    private bool ValidateCharacterSubmission(NetworkSessionPlayer player, CharacterSlotData data, out string sanitizedName, out string reason)
    {
        sanitizedName = SanitizeCharacterName(data.characterName);

        if (player == null || !serverSessionPlayers.Contains(player))
        {
            reason = "Unknown player submitted character data.";
            return false;
        }

        if (!player.HasAssignedColorSlot)
        {
            reason = "Player has no assigned color slot.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            reason = "Character name is empty.";
            return false;
        }

        if (data.parts != null && data.parts.Length > MaxPartEntries)
        {
            reason = "Character submitted too many part entries.";
            return false;
        }

        if (data.colors != null && data.colors.Length > MaxColorEntries)
        {
            reason = "Character submitted too many color entries.";
            return false;
        }

        if (data.visibility != null && data.visibility.Length > MaxVisibilityEntries)
        {
            reason = "Character submitted too many visibility entries.";
            return false;
        }

        if (!ValidatePartEntries(data.parts, out reason))
        {
            return false;
        }

        if (!ValidateColorEntries(data.colors, out reason))
        {
            return false;
        }

        if (!ValidateVisibilityEntries(data.visibility, out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidatePartEntries(CharacterPartSelection[] parts, out string reason)
    {
        if (parts == null)
        {
            reason = string.Empty;
            return true;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (!Enum.IsDefined(typeof(PartsType), parts[i].type))
            {
                reason = "Character submitted an invalid part type.";
                return false;
            }

            if (parts[i].index < -1 || parts[i].index > 10000)
            {
                reason = "Character submitted an invalid part index.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateColorEntries(CharacterColorSelection[] colors, out string reason)
    {
        if (colors == null)
        {
            reason = string.Empty;
            return true;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            if (!Enum.IsDefined(typeof(ColorTargetType), colors[i].target))
            {
                reason = "Character submitted an invalid color target.";
                return false;
            }

            if (!IsFiniteColor(colors[i].color))
            {
                reason = "Character submitted an invalid color.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValidateVisibilityEntries(CharacterVisibilitySelection[] visibility, out string reason)
    {
        if (visibility == null)
        {
            reason = string.Empty;
            return true;
        }

        for (int i = 0; i < visibility.Length; i++)
        {
            if (!Enum.IsDefined(typeof(PartsType), visibility[i].type))
            {
                reason = "Character submitted an invalid visibility type.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static CharacterSlotData NormalizeCharacterSubmission(NetworkSessionPlayer player, CharacterSlotData data, string sanitizedName)
    {
        data.slot = player.ColorSlot;
        data.characterName = sanitizedName;

        if (data.parts == null)
        {
            data.parts = Array.Empty<CharacterPartSelection>();
        }

        if (data.colors == null)
        {
            data.colors = Array.Empty<CharacterColorSelection>();
        }

        if (data.visibility == null)
        {
            data.visibility = Array.Empty<CharacterVisibilitySelection>();
        }

        return data;
    }

    private static string SanitizeCharacterName(string rawName)
    {
        string sanitized = Regex.Replace(rawName ?? string.Empty, "[^a-zA-Z]", string.Empty);
        if (sanitized.Length > MaxCharacterNameLength)
        {
            sanitized = sanitized.Substring(0, MaxCharacterNameLength);
        }

        return sanitized;
    }

    private static bool IsFiniteColor(Color color)
    {
        return IsFinite(color.r) && IsFinite(color.g) && IsFinite(color.b) && IsFinite(color.a);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
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
