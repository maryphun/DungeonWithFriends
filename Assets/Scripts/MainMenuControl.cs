using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class MainMenuControl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup joinLobbyUI;
    [SerializeField] private CanvasGroup loadingUI;
    [SerializeField] private TMP_InputField inputField_IPAddress;
    [SerializeField] private Button connectLobbyBtn;

    private DungeonNetworkManager networkManager;
    private bool waitingForConnection;

    private void OnEnable()
    {
        networkManager = DungeonNetworkManager.EnsureInstance();
        DungeonNetworkManager.ConnectionStateChanged += OnConnectionStateChanged;
    }

    private void Start()
    {
        // setup
        HideCanvasGroup(joinLobbyUI, false);
        HideCanvasGroup(loadingUI, false);

        if (inputField_IPAddress != null)
        {
            inputField_IPAddress.onValueChanged.AddListener(OnIPAdressInput);
            OnIPAdressInput(inputField_IPAddress.text);
        }

        if (networkManager != null)
        {
            networkManager.LeaveSession();
        }
    }

    private void OnDisable()
    {
        if (inputField_IPAddress != null)
        {
            inputField_IPAddress.onValueChanged.RemoveListener(OnIPAdressInput);
        }

        DungeonNetworkManager.ConnectionStateChanged -= OnConnectionStateChanged;
    }

    public void OnClickHostLobby()
    {
        networkManager = DungeonNetworkManager.EnsureInstance();
        if (networkManager == null)
        {
            return;
        }

        networkManager.StartLocalHost();
    }

    public void OnClickJoinLobby()
    {
        // show UI to let player insert ip address to join. this will be replaced by steam.net matchmaking later.
        if (inputField_IPAddress != null)
        {
            inputField_IPAddress.text = string.Empty; // clear IP
        }

        ShowCanvasGroup(joinLobbyUI, true);
    }

    public void OnClickConnectLobby()
    {
        networkManager = DungeonNetworkManager.EnsureInstance();
        if (networkManager == null)
        {
            return;
        }

        // while connecting, swap loading UI
        HideCanvasGroup(joinLobbyUI, true);
        ShowCanvasGroup(loadingUI, false);

        waitingForConnection = true;
        string targetIP = inputField_IPAddress != null ? inputField_IPAddress.text : string.Empty;
        networkManager.JoinLocalGame(targetIP);

        // if succeed call OnSuccessConnectLobby, if failed call OnFailedConnectLobby
    }

    public void OnClickCancelJoinLobby()
    {
        HideCanvasGroup(joinLobbyUI, true);
    }

    public void OnClickCancelLoadingLobby()
    {
        // treated as failure on UI
        waitingForConnection = false;
        if (networkManager != null)
        {
            networkManager.LeaveSession();
        }

        OnFailedConnectLobby();

        // insert mirror networking code here to cancel connection
    }

    public void OnIPAdressInput(string input)
    {
        if (connectLobbyBtn != null)
        {
            connectLobbyBtn.interactable = true;
        }
    }

    public void OnSuccessConnectLobby()
    {
        // insert mirror networking code here and load into MatchmakingLobby scene.
        waitingForConnection = false;
        if (loadingUI != null)
        {
            loadingUI.interactable = false;
        }
    }

    public void OnFailedConnectLobby()
    {
        // swap back loading UI
        waitingForConnection = false;
        ShowCanvasGroup(joinLobbyUI, true);
        HideCanvasGroup(loadingUI, true);
    }

    private void OnConnectionStateChanged(DungeonConnectionState state, string message)
    {
        if (!waitingForConnection)
        {
            return;
        }

        if (state == DungeonConnectionState.Connected)
        {
            OnSuccessConnectLobby();
        }
        else if (state == DungeonConnectionState.Failed || state == DungeonConnectionState.Disconnected)
        {
            OnFailedConnectLobby();
        }
    }

    private void ShowCanvasGroup(CanvasGroup canvasGroup, bool fade)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (fade)
        {
            canvasGroup.DOFade(1.0f, 0.75f);
        }
        else
        {
            canvasGroup.alpha = 1.0f;
        }
    }

    private void HideCanvasGroup(CanvasGroup canvasGroup, bool fade)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (fade)
        {
            canvasGroup.DOFade(0.0f, 0.75f);
        }
        else
        {
            canvasGroup.alpha = 0.0f;
        }
    }

    public void OnClickQuit()
    {
        Application.Quit();
    }
}
