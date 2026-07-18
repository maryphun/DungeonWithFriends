using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerPortrait : MonoBehaviour
{
    [SerializeField] private PlayerController targetPlayer;
    public PlayerController TargetPlayer => targetPlayer;
    public NetworkPlayerCharacter TargetCharacter => targetCharacter;

    [Header("References")]
    [SerializeField] private Image frame;
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text playerNameText;

    [Header("Body parts")]
    [SerializeField] private PortraitBodyParts parts;

    [Header("Sprite Data")]
    [SerializeField] private Sprite greenFrame;
    [SerializeField] private Sprite purpleFrame;
    [SerializeField] private Sprite redFrame;
    [SerializeField] private Sprite blueFrame;
    [Space(10)]
    [SerializeField] private Sprite greenFill;
    [SerializeField] private Sprite purpleFill;
    [SerializeField] private Sprite redFill;
    [SerializeField] private Sprite blueFill;

    private NetworkPlayerCharacter targetCharacter;
    private string displayName = "Player";
    private PlayerColor playerColor = PlayerColor.Blue;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTarget();
    }

    public void Configure(NetworkPlayerCharacter character)
    {
        if (targetCharacter == character)
        {
            Refresh();
            return;
        }

        UnsubscribeFromTarget();
        targetCharacter = character;

        if (targetCharacter != null)
        {
            targetCharacter.AppearanceChanged += Refresh;
            targetPlayer = targetCharacter.Controller;
            displayName = targetCharacter.DisplayName;
            playerColor = targetCharacter.PlayerColor;
        }

        Refresh();
    }

    public void Configure(PlayerController player, string portraitName, PlayerColor portraitColor)
    {
        UnsubscribeFromTarget();
        targetCharacter = null;
        targetPlayer = player;
        displayName = string.IsNullOrWhiteSpace(portraitName) ? portraitColor.ToString() : portraitName;
        playerColor = portraitColor;
        Refresh();
    }

    public void Refresh()
    {
        ResolveReferences();

        if (targetCharacter != null)
        {
            targetPlayer = targetCharacter.Controller;
            displayName = targetCharacter.DisplayName;
            playerColor = targetCharacter.PlayerColor;
        }
        else if (targetPlayer != null)
        {
            displayName = targetPlayer.DisplayName;
            playerColor = targetPlayer.PlayerColor;
        }

        ApplyColorSprites(playerColor);

        if (playerNameText != null)
        {
            playerNameText.text = displayName;
            playerNameText.color = PlayerColorUtility.ToUnityColor(playerColor);
        }

        if (targetPlayer == null)
        {
            return;
        }

        CopyAppearanceFrom(targetPlayer.Parts);
    }

    private void UnsubscribeFromTarget()
    {
        if (targetCharacter != null)
        {
            targetCharacter.AppearanceChanged -= Refresh;
        }
    }

    private void ApplyColorSprites(PlayerColor color)
    {
        Sprite spriteFrame;
        Sprite spriteFill;

        switch (color)
        {
            case PlayerColor.Green:
                spriteFrame = greenFrame;
                spriteFill = greenFill;
                break;
            case PlayerColor.Purple:
                spriteFrame = purpleFrame;
                spriteFill = purpleFill;
                break;
            case PlayerColor.Red:
                spriteFrame = redFrame;
                spriteFill = redFill;
                break;
            case PlayerColor.Blue:
            default:
                spriteFrame = blueFrame;
                spriteFill = blueFill;
                break;
        }

        if (frame != null && spriteFrame != null)
        {
            frame.sprite = spriteFrame;
        }

        if (fill != null && spriteFill != null)
        {
            fill.sprite = spriteFill;
        }
    }

    private void CopyAppearanceFrom(PlayerBodyParts source)
    {
        CopyRenderer(source.body, parts.body, true);
        CopyRenderer(source.chest, parts.chest, source.chest != null && source.chest.gameObject.activeSelf);
        CopyRenderer(source.head, parts.head, true);
        CopyRenderer(source.eye, parts.eye, true);
        CopyRenderer(source.hair, parts.hair, source.hair != null && source.hair.gameObject.activeSelf);
        CopyRenderer(source.hair_helmet, parts.hair_helmet, source.hair_helmet != null && source.hair_helmet.gameObject.activeSelf);
        CopyRenderer(source.helmet, parts.helmet, source.helmet != null && source.helmet.gameObject.activeSelf);
        CopyRenderer(source.beard, parts.beard, source.beard != null && source.beard.gameObject.activeSelf);
    }

    private static void CopyRenderer(SpriteRenderer source, Image target, bool active)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(active && source != null && source.sprite != null);

        if (source == null)
        {
            return;
        }

        target.sprite = source.sprite;
        target.color = source.color;
    }

    private void ResolveReferences()
    {
        if (frame == null)
        {
            Transform frameTransform = transform.Find("frame");
            frame = frameTransform != null ? frameTransform.GetComponent<Image>() : null;
        }

        if (fill == null)
        {
            fill = GetComponent<Image>();
        }

        if (playerNameText == null)
        {
            playerNameText = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
