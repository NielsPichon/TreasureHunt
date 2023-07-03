using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    HandWeapon,
    Firearm,
    HeavyWeapon,
    Tool,
    QuestItem,
    Consumable,
}

[System.Serializable]
public struct Cost {
    public ResourceType type;
    public int amount;
}


[CreateAssetMenu(fileName = "Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string description;
    public Sprite sprite;
    public List<Cost> cost;
    public ItemType type;
}
