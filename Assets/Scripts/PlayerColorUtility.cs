using UnityEngine;

public static class PlayerColorUtility
{
    public static Color ToUnityColor(PlayerColor playerColor)
    {
        switch (playerColor)
        {
            case PlayerColor.Green:
                return new Color(0.2f, 0.85f, 0.35f, 1f);
            case PlayerColor.Purple:
                return new Color(0.65f, 0.35f, 1f, 1f);
            case PlayerColor.Red:
                return new Color(1f, 0.25f, 0.2f, 1f);
            case PlayerColor.Blue:
            default:
                return new Color(0.25f, 0.55f, 1f, 1f);
        }
    }
}
