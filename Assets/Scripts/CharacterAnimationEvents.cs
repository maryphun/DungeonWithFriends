using UnityEngine;

public sealed class CharacterAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;

    public void Setup(PlayerController playerController)
    {
        this.playerController = playerController;
    }

    public void AttackHit()
    {
        
    }

    public void AttackFinished()
    {
        
    }
}