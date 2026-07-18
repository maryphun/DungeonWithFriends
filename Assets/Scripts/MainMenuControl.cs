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

    private void Start()
    {
        // setup
        joinLobbyUI.alpha = 0.0f;
        joinLobbyUI.blocksRaycasts = false;
        joinLobbyUI.interactable = false;

        loadingUI.alpha = 0.0f;
        loadingUI.blocksRaycasts = false;
        loadingUI.interactable = false;

        inputField_IPAddress.onValueChanged.AddListener(OnIPAdressInput);
    }

    private void OnDisable()
    {
        inputField_IPAddress.onValueChanged.RemoveAllListeners();
    }

    public void OnClickHostLobby()
    {
        // insert mirror networking code here and load into MatchmakingLobby scene.
    }

    public void OnClickJoinLobby()
    {
        // show UI to let player insert ip address to join. this will be replaced by steam.net matchmaking later.
        inputField_IPAddress.text = string.Empty; // clear IP

        joinLobbyUI.DOFade(1.0f, 0.75f).OnComplete(() =>
        {
            joinLobbyUI.blocksRaycasts = true;
            joinLobbyUI.interactable = true;
        });

    }

    public void OnClickConnectLobby()
    {
        // insert mirror networking code here and load into MatchmakingLobby scene.
        string targetIP = inputField_IPAddress.text;


        // while connecting, swap loading UI
        joinLobbyUI.DOFade(0.0f, 0.75f);
        joinLobbyUI.blocksRaycasts = false;
        joinLobbyUI.interactable = false;

        loadingUI.alpha = 1.0f;
        loadingUI.blocksRaycasts = true;
        loadingUI.interactable = true;

        // if succeed call OnSuccessConnectLobby, if failed call OnFailedConnectLobby
    }

    public void OnClickCancelJoinLobby()
    {
        joinLobbyUI.DOFade(0.0f, 0.75f);
        joinLobbyUI.blocksRaycasts = false;
        joinLobbyUI.interactable = false;
    }

    public void OnClickCancelLoadingLobby()
    {
        // treated as failure on UI
        OnFailedConnectLobby();

        // insert mirror networking code here to cancel connection

    }

    public void OnIPAdressInput(string input)
    {
        connectLobbyBtn.interactable = (input.Length > 0);
    }

    public void OnSuccessConnectLobby()
    {
        // insert mirror networking code here and load into MatchmakingLobby scene.
        loadingUI.interactable = false;
    }

    public void OnFailedConnectLobby()
    {
        // swap back loading UI
        joinLobbyUI.DOFade(1.0f, 0.75f).OnComplete(()=>
        {
            joinLobbyUI.blocksRaycasts = true;
            joinLobbyUI.interactable = true;
        });

        loadingUI.DOFade(0.0f, 0.75f);
        loadingUI.blocksRaycasts = false;
        loadingUI.interactable = false;
    }

    public void OnClickQuit()
    {
        Application.Quit();
    }
}
