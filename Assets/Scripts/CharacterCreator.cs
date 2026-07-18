using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Linq;
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
    [SerializeField] private TMP_Text numberofplayerReady_Text; // this is the actual text where it should show (currently ready player) / max count of player (for example: 2/4) during the wait time.
    [SerializeField] private CanvasGroup confirmButton;
    [SerializeField] private CanvasGroup inputFieldUI;
    [SerializeField] private TMP_InputField inputField;

    [Header("Preset")]
    [Tooltip("ScriptableObject used for preset slots 1-10 of PanelPartsControl. Falls back to the PanelPartsControl inspector value if left empty.")]
    [SerializeField] private PresetData equipmentPresetData;
    [Tooltip("Index of the preset slot (0-9) to auto-apply on game start. Set to -1 to disable auto-apply.")]
    [SerializeField] private int startupPresetSlot = 0;

    private UIStage currentUIStage;

    /// <summary>
    /// The Player instance managed by this demo controller.
    /// </summary>
    public Player Player => player;

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

        textTitle.text = "Create your character";
        currentUIStage = UIStage.CharacterCreation;

        // hide UI for different statge
        waitingforOtherPlayer_Text.gameObject.SetActive(false);
        numberofplayerReady_Text.gameObject.SetActive(false);
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

        // insert mirror code here and display the number of player that are ready
        numberofplayerReady_Text.text = "/";

        // check if every player are ready.
        if (false) // insert mirror code here to check
        {
            currentUIStage = UIStage.SceneTransition;
        }
    }

    private void SceneTransitionUpdate()
    {
        if (currentUIStage != UIStage.SceneTransition) return;

        // everyone wait 1.5 seconds here then transit to the next scene, "Game".
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
                    inputField.onValueChanged.AddListener(OnChangeValueInputFieldName);
                }
            );

            characterName_Text.text = string.Empty;
            currentUIStage = UIStage.Name;
        }
        else if ( currentUIStage == UIStage.Name )
        {
            currentUIStage = UIStage.WaitForOtherPlayers;
            player.PartsManager.GetComponent<Animator>().Play("Jump");

            confirmButton.gameObject.SetActive(false);
            inputField.onValueChanged.RemoveAllListeners();
            inputFieldUI.interactable = false;
            inputFieldUI.DOFade(0.0f, 1.0f);
            textTitle.DOFade(0.0f, 1.0f);
            characterName_Text.text = inputField.text;
            characterName_Text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.white);
            characterName_Text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.5f);

            // player stuck here until all other player are ready. Show a UI that update instantly about the number of player that are ready
            waitingforOtherPlayer_Text.gameObject.SetActive(true);
            waitingforOtherPlayer_Text.alpha = 0.0f;
            waitingforOtherPlayer_Text.DOFade(1.0f, 1.0f);
            numberofplayerReady_Text.gameObject.SetActive(true);
            numberofplayerReady_Text.alpha = 0.0f;
            numberofplayerReady_Text.DOFade(1.0f, 1.0f);
            numberofplayerReady_Text.text = "0/4"; // update this UI on FixedUpdate()
        }
    }

    public void OnChangeValueInputFieldName(string input)
    {
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
}