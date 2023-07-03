using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public static class ResourceAppearance
{
    private static string rootDir = "Sprites/Crafting/";
    public static Dictionary<ResourceType, Sprite> sprites = (
        new Dictionary<ResourceType, Sprite>()
        {
            {ResourceType.Steal, Resources.Load<Sprite>(rootDir + "iron")},
            {ResourceType.Wood, Resources.Load<Sprite>(rootDir + "wood")},
            {ResourceType.Gold, Resources.Load<Sprite>(rootDir + "gold")},
            {ResourceType.PowerGems, Resources.Load<Sprite>(
                rootDir + "diamond")},
            {ResourceType.Food, Resources.Load<Sprite>(rootDir + "food")},
        }
    );
}
