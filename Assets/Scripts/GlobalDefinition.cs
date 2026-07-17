using UnityEngine.UI;
using UnityEngine;
using System;

public enum PlayerActionBuffer
{
    None,
    Attack,
}

public enum PlayerColor
{
    Green,
    Purple,
    Red,
    Blue,
}

[Serializable]
public struct UnitStats
{
    [SerializeField] public int hp;
    [SerializeField] public int current_hp;
    [SerializeField] public int mp;
    [SerializeField] public int current_mp;

    [SerializeField] public int attackDamage;
    [SerializeField] public int armor;

    [SerializeField] public float movementSpeed;
}

[Serializable]
public struct PortraitBodyParts
{
    [SerializeField] public Image body;
    [SerializeField] public Image chest;
    [SerializeField] public Image head;
    [SerializeField] public Image eye;
    [SerializeField] public Image hair;
    [SerializeField] public Image hair_helmet;
    [SerializeField] public Image helmet;
    [SerializeField] public Image beard;
}

[Serializable]
public struct PlayerBodyParts
{
    [SerializeField] public SpriteRenderer body;
    [SerializeField] public SpriteRenderer chest;
    [SerializeField] public SpriteRenderer head;
    [SerializeField] public SpriteRenderer eye;
    [SerializeField] public SpriteRenderer hair;
    [SerializeField] public SpriteRenderer hair_helmet;
    [SerializeField] public SpriteRenderer helmet;
    [SerializeField] public SpriteRenderer beard;

    [SerializeField] public SpriteRenderer shadow;
}
