using System;
using System.Collections.Generic;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Defines which sprites are available to player-facing character creation.
    /// Parts types without an entry are treated as fully available.
    /// </summary>
    [CreateAssetMenu(fileName = "PartsAvailabilityData", menuName = "LayerLab/ArtMakerUnity/PartsAvailabilityData")]
    public class PartsAvailabilityData : ScriptableObject
    {
        [SerializeField] private List<PartsPool> pools = new();

        [Serializable]
        public class PartsPool
        {
            public PartsType type;

            [Tooltip("When enabled, every loaded sprite for this type is available.")]
            public bool allowAll = true;

            [Tooltip("Used when Allow All is disabled. Drag the unlocked sprites or thumbnails for this PartsType here.")]
            public List<Sprite> availableSprites = new();

        }

        public int[] GetAvailableIndices(PartsType type, PartsCategory category)
        {
            int totalCount = GetCategoryCount(category);
            if (totalCount <= 0) return Array.Empty<int>();

            PartsPool pool = FindPool(type);
            if (pool == null || pool.allowAll)
                return BuildAllIndices(totalCount);

            var result = BuildIndicesFromSprites(pool, category, totalCount);

            result.Sort();
            return result.ToArray();
        }

        public bool IsIndexAvailable(PartsType type, int index, PartsCategory category)
        {
            int totalCount = GetCategoryCount(category);
            if (index < 0 || index >= totalCount) return false;

            PartsPool pool = FindPool(type);
            if (pool == null || pool.allowAll)
                return true;

            var availableIndices = BuildIndicesFromSprites(pool, category, totalCount);
            return availableIndices.Contains(index);
        }

        private PartsPool FindPool(PartsType type)
        {
            if (pools == null) return null;

            foreach (PartsPool pool in pools)
            {
                if (pool != null && pool.type == type)
                    return pool;
            }

            return null;
        }

        private static int[] BuildAllIndices(int totalCount)
        {
            var indices = new int[totalCount];
            for (int i = 0; i < totalCount; i++)
                indices[i] = i;
            return indices;
        }

        private static List<int> BuildIndicesFromSprites(PartsPool pool, PartsCategory category, int totalCount)
        {
            var result = new List<int>();
            if (pool.availableSprites == null || category == null) return result;

            foreach (Sprite sprite in pool.availableSprites)
            {
                if (sprite == null) continue;
                int index = FindSpriteIndex(category, sprite);
                if (index < 0 || index >= totalCount) continue;
                if (!result.Contains(index))
                    result.Add(index);
            }

            return result;
        }

        private static int FindSpriteIndex(PartsCategory category, Sprite sprite)
        {
            if (category.thumbnails != null)
            {
                for (int i = 0; i < category.thumbnails.Length; i++)
                {
                    if (category.thumbnails[i] == sprite)
                        return i;
                }
            }

            if (category.renderers == null) return -1;

            foreach (PartRenderer partRenderer in category.renderers)
            {
                if (partRenderer?.sprites == null) continue;

                for (int i = 0; i < partRenderer.sprites.Length; i++)
                {
                    if (partRenderer.sprites[i] == sprite)
                        return i;
                }
            }

            return -1;
        }

        private static int GetCategoryCount(PartsCategory category)
        {
            if (category == null) return 0;
            return category.SpriteCount > 0 ? category.SpriteCount : category.ThumbnailCount;
        }
    }
}
