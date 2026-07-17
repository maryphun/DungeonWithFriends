using UnityEngine;
using UnityEngine.UI;

public class PlayerPortrait : MonoBehaviour
{
    [SerializeField] PlayerController targetPlayer;

    [Header("References")]
    [SerializeField] Image frame, fill;

    [Header("Body parts")]
    [SerializeField] PortraitBodyParts parts;

    [Header("Sprite Data")]
    [SerializeField] Sprite greenFrame;
    [SerializeField] Sprite purpleFrame, redFrame, blueFrame;
    [Space(10)]
    [SerializeField] Sprite greenFill;
    [SerializeField] Sprite purpleFill, redFill, blueFill;

    private void Start()
    {
        // change color
        Sprite spriteFrame, spriteFill;
        switch (targetPlayer.PlayerColor)
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

        frame.sprite = spriteFrame;
        fill.sprite = spriteFill;

        // copy player appearance
        // body
        var originBody = targetPlayer.Parts.body;
        parts.body.sprite = originBody.sprite;
        parts.body.color = originBody.color;

        // chest
        var originChest = targetPlayer.Parts.chest;
        parts.chest.sprite = originChest.sprite;
        parts.chest.color = originChest.color;
        parts.chest.gameObject.SetActive(originChest.gameObject.activeSelf);

        // head
        var originHead = targetPlayer.Parts.head;
        parts.head.sprite = originHead.sprite;
        parts.head.color = originHead.color;

        // eye
        var originEye = targetPlayer.Parts.eye;
        parts.eye.sprite = originEye.sprite;
        parts.eye.color = originEye.color;

        // hair
        var originHair = targetPlayer.Parts.hair;
        parts.hair.sprite = originHair.sprite;
        parts.hair.color = originHair.color;
        parts.hair.gameObject.SetActive(originHair.gameObject.activeSelf);

        // hair helmet
        var originHairHelmet = targetPlayer.Parts.hair_helmet;
        parts.hair_helmet.sprite = originHairHelmet.sprite;
        parts.hair_helmet.color = originHairHelmet.color;
        parts.hair_helmet.gameObject.SetActive(originHairHelmet.gameObject.activeSelf);

        // helmet
        var originHelmet = targetPlayer.Parts.helmet;
        parts.helmet.sprite = originHelmet.sprite;
        parts.helmet.color = originHelmet.color;
        parts.helmet.gameObject.SetActive(originHelmet.gameObject.activeSelf);

        // beard
        var originBeard = targetPlayer.Parts.beard;
        parts.beard.sprite = originBeard.sprite;
        parts.beard.color = originBeard.color;
        parts.beard.gameObject.SetActive(originBeard.gameObject.activeSelf);
    }
}
