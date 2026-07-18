using System;
using System.Collections.Generic;
using LayerLab.ArtMakerUnity;
using Mirror;
using UnityEngine;

[Serializable]
public struct CharacterSlotData
{
    public PlayerColor slot;
    public string characterName;
    public CharacterPartSelection[] parts;
    public CharacterColorSelection[] colors;
    public CharacterVisibilitySelection[] visibility;
}

[Serializable]
public struct CharacterPartSelection
{
    public PartsType type;
    public int index;
    public string spriteName;
}

[Serializable]
public struct CharacterColorSelection
{
    public ColorTargetType target;
    public Color color;
}

[Serializable]
public struct CharacterVisibilitySelection
{
    public PartsType type;
    public bool visible;
}


public static class CharacterSlotDataNetworkSerialization
{
    private const int MaxSerializedPartEntries = 64;
    private const int MaxSerializedColorEntries = 16;
    private const int MaxSerializedVisibilityEntries = 64;

    public static void WriteCharacterSlotData(this NetworkWriter writer, CharacterSlotData value)
    {
        writer.WriteInt((int)value.slot);
        writer.WriteString(value.characterName);
        WritePartSelections(writer, value.parts);
        WriteColorSelections(writer, value.colors);
        WriteVisibilitySelections(writer, value.visibility);
    }

    public static CharacterSlotData ReadCharacterSlotData(this NetworkReader reader)
    {
        return new CharacterSlotData
        {
            slot = (PlayerColor)reader.ReadInt(),
            characterName = reader.ReadString(),
            parts = ReadPartSelections(reader),
            colors = ReadColorSelections(reader),
            visibility = ReadVisibilitySelections(reader)
        };
    }

    private static void WritePartSelections(NetworkWriter writer, CharacterPartSelection[] values)
    {
        int count = values == null ? 0 : values.Length;
        writer.WriteInt(count);

        for (int i = 0; i < count; i++)
        {
            writer.WriteInt((int)values[i].type);
            writer.WriteInt(values[i].index);
            writer.WriteString(values[i].spriteName);
        }
    }

    private static CharacterPartSelection[] ReadPartSelections(NetworkReader reader)
    {
        int count = Mathf.Max(0, reader.ReadInt());
        int storedCount = Mathf.Min(count, MaxSerializedPartEntries);
        CharacterPartSelection[] values = new CharacterPartSelection[storedCount];

        for (int i = 0; i < count; i++)
        {
            CharacterPartSelection value = new CharacterPartSelection
            {
                type = (PartsType)reader.ReadInt(),
                index = reader.ReadInt(),
                spriteName = reader.ReadString()
            };

            if (i < storedCount)
            {
                values[i] = value;
            }
        }

        return values;
    }

    private static void WriteColorSelections(NetworkWriter writer, CharacterColorSelection[] values)
    {
        int count = values == null ? 0 : values.Length;
        writer.WriteInt(count);

        for (int i = 0; i < count; i++)
        {
            writer.WriteInt((int)values[i].target);
            writer.WriteColor(values[i].color);
        }
    }

    private static CharacterColorSelection[] ReadColorSelections(NetworkReader reader)
    {
        int count = Mathf.Max(0, reader.ReadInt());
        int storedCount = Mathf.Min(count, MaxSerializedColorEntries);
        CharacterColorSelection[] values = new CharacterColorSelection[storedCount];

        for (int i = 0; i < count; i++)
        {
            CharacterColorSelection value = new CharacterColorSelection
            {
                target = (ColorTargetType)reader.ReadInt(),
                color = reader.ReadColor()
            };

            if (i < storedCount)
            {
                values[i] = value;
            }
        }

        return values;
    }

    private static void WriteVisibilitySelections(NetworkWriter writer, CharacterVisibilitySelection[] values)
    {
        int count = values == null ? 0 : values.Length;
        writer.WriteInt(count);

        for (int i = 0; i < count; i++)
        {
            writer.WriteInt((int)values[i].type);
            writer.WriteBool(values[i].visible);
        }
    }

    private static CharacterVisibilitySelection[] ReadVisibilitySelections(NetworkReader reader)
    {
        int count = Mathf.Max(0, reader.ReadInt());
        int storedCount = Mathf.Min(count, MaxSerializedVisibilityEntries);
        CharacterVisibilitySelection[] values = new CharacterVisibilitySelection[storedCount];

        for (int i = 0; i < count; i++)
        {
            CharacterVisibilitySelection value = new CharacterVisibilitySelection
            {
                type = (PartsType)reader.ReadInt(),
                visible = reader.ReadBool()
            };

            if (i < storedCount)
            {
                values[i] = value;
            }
        }

        return values;
    }
}


public static class CharacterSlotDataUtility
{
    public static CharacterSlotData FromPartsManager(PlayerColor slot, string characterName, PartsManager partsManager)
    {
        CharacterSlotData data = FromPreset(slot, characterName, partsManager != null ? partsManager.ToPresetItem() : null);
        if (partsManager == null || data.parts == null)
        {
            return data;
        }

        for (int i = 0; i < data.parts.Length; i++)
        {
            data.parts[i].spriteName = partsManager.GetActiveSpriteName(data.parts[i].type);
        }

        return data;
    }

    public static string ToJson(CharacterSlotData data)
    {
        return JsonUtility.ToJson(data);
    }

    public static bool TryFromJson(string json, out CharacterSlotData data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<CharacterSlotData>(json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not parse character data: {exception.Message}");
            return false;
        }
    }

    public static CharacterSlotData FromPreset(PlayerColor slot, string characterName, PresetData.PresetItem item)
    {
        CharacterSlotData data = new CharacterSlotData
        {
            slot = slot,
            characterName = characterName,
            parts = Array.Empty<CharacterPartSelection>(),
            colors = Array.Empty<CharacterColorSelection>(),
            visibility = Array.Empty<CharacterVisibilitySelection>()
        };

        if (item == null)
        {
            return data;
        }

        if (item.parts != null)
        {
            data.parts = new CharacterPartSelection[item.parts.Count];
            for (int i = 0; i < item.parts.Count; i++)
            {
                data.parts[i] = new CharacterPartSelection
                {
                    type = item.parts[i].type,
                    index = item.parts[i].index,
                    spriteName = string.Empty
                };
            }
        }

        if (item.colors != null)
        {
            data.colors = new CharacterColorSelection[item.colors.Count];
            for (int i = 0; i < item.colors.Count; i++)
            {
                data.colors[i] = new CharacterColorSelection
                {
                    target = item.colors[i].target,
                    color = item.colors[i].color
                };
            }
        }

        if (item.visibility != null)
        {
            data.visibility = new CharacterVisibilitySelection[item.visibility.Count];
            for (int i = 0; i < item.visibility.Count; i++)
            {
                data.visibility[i] = new CharacterVisibilitySelection
                {
                    type = item.visibility[i].type,
                    visible = item.visibility[i].visible
                };
            }
        }

        return data;
    }

    public static PresetData.PresetItem ToPresetItem(CharacterSlotData data)
    {
        PresetData.PresetItem item = new PresetData.PresetItem
        {
            isEmpty = false,
            parts = new List<PresetData.PartsEntry>(),
            colors = new List<PresetData.ColorEntry>(),
            visibility = new List<PresetData.VisibilityEntry>()
        };

        if (data.parts != null)
        {
            for (int i = 0; i < data.parts.Length; i++)
            {
                item.parts.Add(new PresetData.PartsEntry
                {
                    type = data.parts[i].type,
                    index = data.parts[i].index
                });
            }
        }

        if (data.colors != null)
        {
            for (int i = 0; i < data.colors.Length; i++)
            {
                item.colors.Add(new PresetData.ColorEntry
                {
                    target = data.colors[i].target,
                    color = data.colors[i].color
                });
            }
        }

        if (data.visibility != null)
        {
            for (int i = 0; i < data.visibility.Length; i++)
            {
                item.visibility.Add(new PresetData.VisibilityEntry
                {
                    type = data.visibility[i].type,
                    visible = data.visibility[i].visible
                });
            }
        }

        return item;
    }
}
