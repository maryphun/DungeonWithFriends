using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

struct MatchmakingLobbyPlayerSlotData
{
    string playerName; // later would be replaced by SteamUserID
    PlayerColor playerColor; // assigned randomly during start
}

public class MatchmakingLobbyController : MonoBehaviour
{
    [Header("Ready Checks")]
    [SerializeField] private TMP_Text[] playerNameDisplay = new TMP_Text[4];

    [Header("Buttons")]
    [SerializeField] private GameObject[] transferHostBtn = new GameObject[3];
    [SerializeField] private GameObject[] kickPlayerBtn = new GameObject[3];
    [SerializeField] private GameObject startGame; // available for host only. only clickable if every non-host player are ready
    [SerializeField] private GameObject ready; // available for non-host only
    [SerializeField] private GameObject leaveGame; // host leaving stops this local lobby phase

    [Header("Ready Checks")]
    [SerializeField] private GameObject[] readyChecks = new GameObject[3];

    [Header("IP")]
    [SerializeField] private TMP_Text IPAddress; // display the ip address of this matchmakinglobby

    MatchmakingLobbyPlayerSlotData[] slotData = new MatchmakingLobbyPlayerSlotData[4]; // index 0 is always the host.
    public bool isHost; // determine locally if this player is host
    public int mySlotIndex; // determine locally the slot index of this user

    private Button startGameButton;
    private Button readyButton;
    private TMP_Text readyButtonLabel;

    private void OnEnable()
    {
        DungeonNetworkManager.LobbyChanged += RefreshLobby;
        NetworkSessionPlayer.ClientStateChanged += RefreshLobby;
    }

    private void Start()
    {
        CacheButtons();
        SetupScene(); // I assume we need to call this function if there is a change of host
    }

    private void OnDisable()
    {
        DungeonNetworkManager.LobbyChanged -= RefreshLobby;
        NetworkSessionPlayer.ClientStateChanged -= RefreshLobby;
    }

    public void SetupScene()
    {
        RefreshLobby();
    }

    public void OnClickButtonStartGame()
    {
        // executable by host only
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            manager.RequestStartCharacterCreation();
        }
    }

    public void OnClickButtonReady()
    {
        // executable by non-host only
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        NetworkSessionPlayer localPlayer = NetworkSessionPlayer.LocalPlayer;
        if (manager != null && localPlayer != null && !localPlayer.IsHost)
        {
            manager.SetLocalLobbyReady(!localPlayer.LobbyReady);
        }
    }

    public void OnClickButtonLeave()
    {
        // executable by everyone,
        // if host leave the game and is not the only player in the lobby,
        // automatically transfer to the next player and force it into the first slot
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            manager.LeaveSession();
        }
    }

    public void OnClickButtonKickPlayer(int slotIndex)
    {
        // executable by host only
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            manager.RequestKickSlot(slotIndex);
        }
    }

    public void OnClickButtonTransferHost(int slotIndex)
    {
        // executable by host only
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            manager.RequestTransferHost(slotIndex);
        }
    }

    public void OnClickCopyIPAddressToClippBoard()
    {
        if (IPAddress != null)
        {
            GUIUtility.systemCopyBuffer = IPAddress.text;
        }
    }

    private void CacheButtons()
    {
        if (startGame != null)
        {
            startGameButton = startGame.GetComponent<Button>();
        }

        if (ready != null)
        {
            readyButton = ready.GetComponent<Button>();
            readyButtonLabel = ready.GetComponentInChildren<TMP_Text>();
        }
    }

    private void RefreshLobby()
    {
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        NetworkSessionPlayer localPlayer = NetworkSessionPlayer.LocalPlayer;
        bool connected = NetworkServer.active || NetworkClient.active;

        isHost = NetworkServer.active;
        mySlotIndex = localPlayer != null && localPlayer.HasAssignedColorSlot ? DungeonNetworkManager.GetSlotIndex(localPlayer.ColorSlot) : -1;

        RefreshPlayerSlots();

        if (IPAddress != null)
        {
            IPAddress.text = manager != null ? manager.GetDisplayAddress() : string.Empty;
        }

        if (startGame != null)
        {
            startGame.SetActive(isHost);
        }

        if (startGameButton != null)
        {
            bool canStart = manager != null && manager.CanStartCharacterCreation(out _);
            startGameButton.interactable = canStart;
        }

        if (ready != null)
        {
            ready.SetActive(connected && localPlayer != null && !localPlayer.IsHost);
        }

        if (readyButton != null)
        {
            readyButton.interactable = localPlayer != null && !localPlayer.IsHost;
        }

        if (readyButtonLabel != null)
        {
            readyButtonLabel.text = localPlayer != null && localPlayer.LobbyReady ? "Unready" : "Ready";
        }

        if (leaveGame != null)
        {
            leaveGame.SetActive(connected);
        }

        RefreshHostButtons();
    }

    private void RefreshPlayerSlots()
    {
        for (int i = 0; i < playerNameDisplay.Length; i++)
        {
            if (playerNameDisplay[i] != null)
            {
                playerNameDisplay[i].text = "Empty";
            }
        }

        for (int i = 0; i < readyChecks.Length; i++)
        {
            if (readyChecks[i] != null)
            {
                readyChecks[i].SetActive(false);
            }
        }

        for (int i = 0; i < NetworkSessionPlayer.ClientPlayers.Count; i++)
        {
            NetworkSessionPlayer player = NetworkSessionPlayer.ClientPlayers[i];
            if (player == null || !player.HasAssignedColorSlot)
            {
                continue;
            }

            int slotIndex = DungeonNetworkManager.GetSlotIndex(player.ColorSlot);
            if (slotIndex < 0 || slotIndex >= playerNameDisplay.Length)
            {
                continue;
            }

            if (playerNameDisplay[slotIndex] != null)
            {
                string state = player.IsHost ? "Host" : player.LobbyReady ? "Ready" : "Not Ready";
                playerNameDisplay[slotIndex].text = $"{player.DisplayName} ({player.ColorSlot}) - {state}";
            }

            int readyCheckIndex = slotIndex - 1;
            if (readyCheckIndex >= 0 && readyCheckIndex < readyChecks.Length && readyChecks[readyCheckIndex] != null)
            {
                readyChecks[readyCheckIndex].SetActive(player.LobbyReady);
            }
        }
    }

    private void RefreshHostButtons()
    {
        for (int i = 0; i < transferHostBtn.Length; i++)
        {
            if (transferHostBtn[i] != null)
            {
                transferHostBtn[i].SetActive(false);
            }
        }

        for (int i = 0; i < kickPlayerBtn.Length; i++)
        {
            if (kickPlayerBtn[i] == null)
            {
                continue;
            }

            int slotIndex = i + 1;
            NetworkSessionPlayer player = FindClientPlayerInSlot(slotIndex);
            kickPlayerBtn[i].SetActive(isHost && player != null && !player.IsHost);
        }
    }

    private NetworkSessionPlayer FindClientPlayerInSlot(int slotIndex)
    {
        for (int i = 0; i < NetworkSessionPlayer.ClientPlayers.Count; i++)
        {
            NetworkSessionPlayer player = NetworkSessionPlayer.ClientPlayers[i];
            if (player == null || !player.HasAssignedColorSlot)
            {
                continue;
            }

            if (DungeonNetworkManager.GetSlotIndex(player.ColorSlot) == slotIndex)
            {
                return player;
            }
        }

        return null;
    }
}
