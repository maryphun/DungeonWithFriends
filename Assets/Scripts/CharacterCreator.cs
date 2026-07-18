using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using LayerLab.ArtMakerUnity;
using System.Text.RegularExpressions;

public class CharacterCreator : MonoBehaviour
{
    enum UIStage
    {
        CharacterCreation,
        Name,
        WaitForOtherPlayers,
        SceneTransition,
    }

    [Header("Core")][SerializeField] private Player player;
    [SerializeField] private CameraControl cameraControl;

    [Header("UI Panels")]
    [SerializeField] private PanelPartsControl panelPartsControl;
    [SerializeField] private PanelPartsListControl panelPartsListControl;
    [SerializeField] private ColorPicker colorPicker;
    [SerializeField] private ColorPresetManager colorPresetManager;
    [SerializeField] private ColorFavoriteManager colorFavoriteManager;

    [SerializeField] private TMP_Text textTitle;
    [SerializeField] private TMP_Text characterName_Text;
    [SerializeField] private TMP_Text waitingforOtherPlayer_Text;
    [SerializeField] private TMP_Text numberofplayerReady_Text; // this is the actual text where it should show (currently ready player) / current count of player (for example: 2/4) during the wait time.
    [SerializeField] private CanvasGroup confirmButton;
    [SerializeField] private CanvasGroup inputFieldUI;
    [SerializeField] private TMP_InputField inputField;

    [Header("Preset")]
    [Tooltip("ScriptableObject used for preset slots 1-10 of PanelPartsControl. Falls back to the PanelPartsControl inspector value if left empty.")]
    [SerializeField] private PresetData equipmentPresetData;
    [Tooltip("Index of the preset slot (0-9) to auto-apply on game start. Set to -1 to disable auto-apply.")]
    [SerializeField] private int startupPresetSlot = 0;

    private UIStage currentUIStage;
    private bool submittedCharacter;
    private bool sceneTransitionMessageShown;

    /// <summary>
    /// The Player instance managed by this demo controller.
    /// </summary>
    public Player Player => player;

    private void OnEnable()
    {
        NetworkSessionPlayer.ClientStateChanged += OnNetworkSessionStateChanged;
    }

    private void Start()
    {
        player.Init();
        if (cameraControl != null) cameraControl.Init(player.transform);
        if (colorPicker != null) colorPicker.Init(player.PartsManager);
        if (colorPresetManager != null) colorPresetManager.Init(player.PartsManager);
        if (colorFavoriteManager != null) colorFavoriteManager.Init();
        if (panelPartsListControl != null) panelPartsListControl.Init(player.PartsManager);
        if (panelPartsControl != null)
            panelPartsControl.Init(player.PartsManager, panelPartsListControl, equipmentPresetData, startupPresetSlot);

        currentUIStage = UIStage.CharacterCreation;
        RefreshAssignedColorTitle();

        // hide UI for different statge
        SetWaitingUIVisible(false, false);
    }

    private void OnDisable()
    {
        NetworkSessionPlayer.ClientStateChanged -= OnNetworkSessionStateChanged;

        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnChangeValueInputFieldName);
        }
    }

    /// <summary>
    /// Randomizes all character parts and colors (Skin, Hair, Eye), then refreshes the UI.
    /// </summary>
    public void RandomizeCharacter()
    {
        var pm = player.PartsManager;
        pm.RandomizeAll();
        colorPresetManager.SetRandomColor(ColorTargetType.Skin);
        colorPresetManager.SetRandomColor(ColorTargetType.Hair);
        colorPresetManager.SetRandomColor(ColorTargetType.Beard);

        // Refresh the parts list of the currently selected category
        if (panelPartsControl != null)
            panelPartsControl.RefreshCurrentSlot();
    }

    private void Update()
    {
        if (currentUIStage != UIStage.CharacterCreation) return;
        if (IsTextInputFocused()) return;
        if (Input.GetKeyDown(KeyCode.R))
            OnClickResetAll();
    }

    private void LateUpdate()
    {
        switch (currentUIStage)
        {
            case UIStage.WaitForOtherPlayers:
                WaitForOtherPlayersUpdate();
                break;
            case UIStage.SceneTransition:
                SceneTransitionUpdate();
                break;
            default:
                break;
        }
    }

    private void WaitForOtherPlayersUpdate()
    {
        if (currentUIStage != UIStage.WaitForOtherPlayers) return;

        if (AreAllKnownPlayersCharacterReady())
        {
            EnterSceneTransitionStage();
        }
    }

    private void SceneTransitionUpdate()
    {
        if (currentUIStage != UIStage.SceneTransition || sceneTransitionMessageShown) return;

        sceneTransitionMessageShown = true;

        if (waitingforOtherPlayer_Text != null)
        {
            waitingforOtherPlayer_Text.text = "Starting game...";
        }
    }

    /// <summary>
    /// Returns true when a TMP_InputField or legacy InputField currently has keyboard focus,
    /// so hotkeys are suppressed while the user is typing.
    /// </summary>
    private static bool IsTextInputFocused()
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var go = es.currentSelectedGameObject;
        if (go == null) return false;
        return go.GetComponent<TMP_InputField>() != null
            || go.GetComponent<UnityEngine.UI.InputField>() != null;
    }

    /// <summary>
    /// Resets the character to the initial state (PartsManager.Init() output).
    /// Bound to the R key by default. Does not change PanelPartsControl preset index.
    /// </summary>
    public void OnClickResetAll()
    {
        if (player == null) return;
        var pm = player.PartsManager;
        if (pm == null) return;
        pm.ResetAll();
        if (panelPartsControl != null)
            panelPartsControl.RefreshCurrentSlot();
    }

    public void OnClickConfirm()
    {
        if (currentUIStage == UIStage.CharacterCreation)
        {
            panelPartsControl.gameObject.SetActive(false);
            panelPartsListControl.gameObject.SetActive(false);

            confirmButton.interactable = false;
            confirmButton.DOFade(0.0f, 0.5f);

            DOTween.To(() => cameraControl.offset.x, x => cameraControl.offset = new Vector3(x, cameraControl.offset.y, cameraControl.offset.z), 0.0f, 1.5f).SetEase(Ease.InOutSine);
            DOTween.To(() => cameraControl.cameraSize, newValue => cameraControl.cameraSize = newValue, 4.5f, 1.5f).SetEase(Ease.InOutSine);

            textTitle.text = string.Empty;
            textTitle.DOText("What is your name?", 1.5f, false);

            inputFieldUI.gameObject.SetActive(true);
            inputFieldUI.DOFade(1.0f, 1.5f).OnComplete(() =>
                {
                    inputField.interactable = true;
                    inputField.Select();
                    inputField.ActivateInputField();
                    inputField.onValueChanged.RemoveListener(OnChangeValueInputFieldName);
                    inputField.onValueChanged.AddListener(OnChangeValueInputFieldName);
                }
            );

            characterName_Text.text = string.Empty;
            currentUIStage = UIStage.Name;
        }
        else if (currentUIStage == UIStage.Name)
        {
            EnterWaitForOtherPlayersStage();
        }
    }

    public void OnChangeValueInputFieldName(string input)
    {
        if (inputField == null || confirmButton == null)
        {
            return;
        }

        string filtered = Regex.Replace(input, "[^a-zA-Z]", "");

        if (filtered != input)
        {
            inputField.text = filtered;
        }

        if (inputField.text.Length > 0)
        {
            confirmButton.interactable = true;
            confirmButton.DOFade(1.0f, 0.5f);
        }
        else
        {
            confirmButton.interactable = false;
            confirmButton.DOFade(0.0f, 0.5f);
        }
    }

    private void EnterWaitForOtherPlayersStage()
    {
        currentUIStage = UIStage.WaitForOtherPlayers;

        if (player != null && player.PartsManager != null)
        {
            Animator animator = player.PartsManager.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("Jump");
            }
        }

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(false);
        }

        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnChangeValueInputFieldName);
        }

        if (inputFieldUI != null)
        {
            inputFieldUI.interactable = false;
            inputFieldUI.DOFade(0.0f, 1.0f);
        }

        if (textTitle != null)
        {
            textTitle.DOFade(0.0f, 1.0f);
        }

        if (characterName_Text != null)
        {
            characterName_Text.text = inputField != null ? inputField.text : string.Empty;
            characterName_Text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
            characterName_Text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.5f);
        }

        // player stuck here until all other player are ready. Show a UI that update instantly about the number of player that are ready
        SetWaitingUIVisible(true, true);
        SubmitCharacterSelection();
        RefreshCharacterCreationWaitingUI();
    }

    private void SubmitCharacterSelection()
    {
        if (submittedCharacter)
        {
            return;
        }

        NetworkSessionPlayer localPlayer = NetworkSessionPlayer.LocalPlayer;
        if (localPlayer == null || !localPlayer.HasAssignedColorSlot)
        {
            Debug.LogWarning("Cannot submit character because no local network session player is assigned.");
            return;
        }

        if (player == null || player.PartsManager == null)
        {
            Debug.LogWarning("Cannot submit character because the character PartsManager is missing.");
            return;
        }

        string characterName = inputField != null ? inputField.text : string.Empty;
        CharacterSlotData data = CharacterSlotDataUtility.FromPreset(localPlayer.ColorSlot, characterName, player.PartsManager.ToPresetItem());
        localPlayer.CmdSubmitCharacter(data);
        submittedCharacter = true;
    }

    private void SetWaitingUIVisible(bool visible, bool fade)
    {
        SetTextVisible(waitingforOtherPlayer_Text, visible, fade);
        SetTextVisible(numberofplayerReady_Text, visible, fade);

        if (visible && numberofplayerReady_Text != null)
        {
            numberofplayerReady_Text.text = "0/0";
        }
    }

    private static void SetTextVisible(TMP_Text text, bool visible, bool fade)
    {
        if (text == null)
        {
            return;
        }

        text.gameObject.SetActive(visible);

        if (!visible)
        {
            text.alpha = 0.0f;
            return;
        }

        if (fade)
        {
            text.alpha = 0.0f;
            text.DOFade(1.0f, 1.0f);
        }
        else
        {
            text.alpha = 1.0f;
        }
    }


    private void OnNetworkSessionStateChanged()
    {
        if (currentUIStage == UIStage.CharacterCreation)
        {
            RefreshAssignedColorTitle();
            return;
        }

        if (currentUIStage == UIStage.WaitForOtherPlayers || currentUIStage == UIStage.SceneTransition)
        {
            RefreshCharacterCreationWaitingUI();
        }
    }

    private void RefreshAssignedColorTitle()
    {
        if (textTitle == null || currentUIStage != UIStage.CharacterCreation)
        {
            return;
        }

        NetworkSessionPlayer localPlayer = NetworkSessionPlayer.LocalPlayer;
        textTitle.text = localPlayer != null && localPlayer.HasAssignedColorSlot
            ? $"Create your {localPlayer.ColorSlot} character"
            : "Create your character";
    }

    private void RefreshCharacterCreationWaitingUI()
    {
        int playerCount = GetKnownPlayerCount();
        int readyCount = GetKnownReadyCount();

        if (numberofplayerReady_Text != null)
        {
            numberofplayerReady_Text.text = $"{readyCount}/{playerCount}";
        }

        if (currentUIStage == UIStage.WaitForOtherPlayers && playerCount > 0 && readyCount >= playerCount)
        {
            EnterSceneTransitionStage();
        }
    }

    private int GetKnownPlayerCount()
    {
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            int sessionCount = manager.GetSessionPlayerCount();
            if (sessionCount > 0)
            {
                return sessionCount;
            }
        }

        return NetworkSessionPlayer.ClientPlayers.Count;
    }

    private int GetKnownReadyCount()
    {
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            return manager.GetCharacterCreationReadyCount();
        }

        int readyCount = 0;
        for (int i = 0; i < NetworkSessionPlayer.ClientPlayers.Count; i++)
        {
            NetworkSessionPlayer sessionPlayer = NetworkSessionPlayer.ClientPlayers[i];
            if (sessionPlayer != null && sessionPlayer.CharacterCreationReady)
            {
                readyCount++;
            }
        }

        return readyCount;
    }

    private bool AreAllKnownPlayersCharacterReady()
    {
        DungeonNetworkManager manager = DungeonNetworkManager.Active;
        if (manager != null)
        {
            return manager.IsCharacterCreationComplete();
        }

        int playerCount = NetworkSessionPlayer.ClientPlayers.Count;
        return playerCount > 0 && GetKnownReadyCount() >= playerCount;
    }

    private void EnterSceneTransitionStage()
    {
        if (currentUIStage == UIStage.SceneTransition)
        {
            return;
        }

        currentUIStage = UIStage.SceneTransition;
        sceneTransitionMessageShown = false;
        SceneTransitionUpdate();
    }

}