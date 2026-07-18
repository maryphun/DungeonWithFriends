using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameHUDController : MonoBehaviour
{
    private const string PortraitPrefabResourceName = "Portrait";

    public static GameHUDController Instance { get; private set; }

    [SerializeField] private GameObject portraitPrefab;
    [SerializeField] private RectTransform portraitStack;

    private readonly Dictionary<NetworkPlayerCharacter, PlayerPortrait> portraitByCharacter = new Dictionary<NetworkPlayerCharacter, PlayerPortrait>();

    public static GameHUDController EnsureExists()
    {
        if (Instance != null)
        {
            Instance.EnsureReferences();
            return Instance;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameHUDController controller = canvas.GetComponent<GameHUDController>();
        if (controller == null)
        {
            controller = canvas.gameObject.AddComponent<GameHUDController>();
        }

        controller.EnsureReferences();
        return controller;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureReferences();
    }

    private void OnEnable()
    {
        NetworkPlayerCharacter.ClientCharactersChanged += RefreshPortraits;
        RefreshPortraits();
    }

    private void OnDisable()
    {
        NetworkPlayerCharacter.ClientCharactersChanged -= RefreshPortraits;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RefreshPortraits()
    {
        EnsureReferences();

        if (portraitPrefab == null || portraitStack == null)
        {
            return;
        }

        List<NetworkPlayerCharacter> characters = new List<NetworkPlayerCharacter>(NetworkPlayerCharacter.ClientCharacters);
        characters.RemoveAll(character => character == null);
        characters.Sort(CompareCharacters);

        RemoveMissingPortraits(characters);

        for (int i = 0; i < characters.Count; i++)
        {
            NetworkPlayerCharacter character = characters[i];

            if (!portraitByCharacter.TryGetValue(character, out PlayerPortrait portrait) || portrait == null)
            {
                GameObject portraitObject = Instantiate(portraitPrefab, portraitStack);
                portraitObject.name = $"Portrait [{character.DisplayName}]";
                portrait = portraitObject.GetComponent<PlayerPortrait>();

                if (portrait == null)
                {
                    portrait = portraitObject.AddComponent<PlayerPortrait>();
                }

                portraitByCharacter[character] = portrait;
            }

            portrait.transform.SetSiblingIndex(i);
            portrait.Configure(character);
        }
    }

    private void EnsureReferences()
    {
        if (portraitPrefab == null)
        {
            portraitPrefab = Resources.Load<GameObject>(PortraitPrefabResourceName);
        }

        if (portraitStack == null)
        {
            portraitStack = CreatePortraitStack();
        }
    }

    private RectTransform CreatePortraitStack()
    {
        GameObject stackObject = new GameObject("Portrait Stack", typeof(RectTransform), typeof(VerticalLayoutGroup));
        stackObject.transform.SetParent(transform, false);

        RectTransform rectTransform = stackObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(20f, -20f);
        rectTransform.sizeDelta = new Vector2(180f, 620f);

        VerticalLayoutGroup layoutGroup = stackObject.GetComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.spacing = 8f;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;

        return rectTransform;
    }

    private void RemoveMissingPortraits(List<NetworkPlayerCharacter> characters)
    {
        List<NetworkPlayerCharacter> staleCharacters = new List<NetworkPlayerCharacter>();

        foreach (KeyValuePair<NetworkPlayerCharacter, PlayerPortrait> pair in portraitByCharacter)
        {
            if (pair.Key == null || !characters.Contains(pair.Key))
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }

                staleCharacters.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleCharacters.Count; i++)
        {
            portraitByCharacter.Remove(staleCharacters[i]);
        }
    }

    private static int CompareCharacters(NetworkPlayerCharacter left, NetworkPlayerCharacter right)
    {
        if (left == right)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        if (left.IsOwnedByLocalClient != right.IsOwnedByLocalClient)
        {
            return left.IsOwnedByLocalClient ? -1 : 1;
        }

        int leftSlot = DungeonNetworkManager.GetSlotIndex(left.PlayerColor);
        int rightSlot = DungeonNetworkManager.GetSlotIndex(right.PlayerColor);
        return leftSlot.CompareTo(rightSlot);
    }
}
