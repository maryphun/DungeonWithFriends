using Mirror.BouncyCastle.Asn1.BC;
using UnityEngine;

struct MatchmakingLobbyPlayerSlotData
{
    string playerName; // later would be replaced by SteamUserID
    PlayerColor playerColor; // assigned randomly during start
}

public class MatchmakingLobbyController : MonoBehaviour
{
    [Header("Ready Checks")]
    [SerializeField] private TMPro.TMP_Text[] playerNameDisplay = new TMPro.TMP_Text[4];

    [Header("Buttons")]
    [SerializeField] private GameObject[] transferHostBtn = new GameObject[3];
    [SerializeField] private GameObject[] kickPlayerBtn = new GameObject[3];
    [SerializeField] private GameObject startGame; // available for host only. only clickable if every non-host player are ready
    [SerializeField] private GameObject ready; // available for non-host only
    [SerializeField] private GameObject leaveGame; // if host leave the game and is not the only player in the lobby, automatically transfer to the next player and force it into the first slot

    [Header("Ready Checks")]
    [SerializeField] private GameObject[] readyChecks = new GameObject[3];

    MatchmakingLobbyPlayerSlotData[] slotData = new MatchmakingLobbyPlayerSlotData[4]; // index 0 is always the host.
    public bool isHost; // determine locally if this player is host
    public int mySlotIndex; // determine locally the slot index of this user

    private void Start()
    {
        SetupScene(); // I assume we need to call this function if there is a change of host

        foreach (GameObject readyCheck_Fill in readyChecks) { readyCheck_Fill.SetActive(false); } // set everything to false first.
        foreach (TMPro.TMP_Text playerName in playerNameDisplay) { playerName.text = "Empty"; }
    }

    public void SetupScene()
    {
        if (isHost)
        {
            // setup all buttons that's available for host
            foreach (GameObject btn in transferHostBtn) { btn.SetActive(true); }
            foreach (GameObject btn in kickPlayerBtn) { btn.SetActive(true); }
            startGame.SetActive(true);
            ready.SetActive(false);
            leaveGame.SetActive(true);
        }
        else
        {
            // setup all buttons that's available for non-host
            foreach (GameObject btn in transferHostBtn) { btn.SetActive(false); }
            foreach (GameObject btn in kickPlayerBtn) { btn.SetActive(false); }
            startGame.SetActive(false);
            ready.SetActive(true);
            leaveGame.SetActive(true);
        }
    }

    public void OnClickButtonStartGame()
    {
        // executable by host only

    }

    public void OnClickButtonReady()
    {
        // executable by non-host only

    }

    public void OnClickButtonLeave()
    {
        // executable by everyone,
        // if host leave the game and is not the only player in the lobby,
        // automatically transfer to the next player and force it into the first slot


    }

    public void OnClickButtonKickPlayer(int slotIndex)
    {
        // executable by host only

    }

    public void OnClickButtonTransferHost(int slotIndex)
    {
        // executable by host only

    }
}
