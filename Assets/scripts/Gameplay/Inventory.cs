using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Inventory", menuName = "", order = 1)]
public class Inventory : ScriptableObject
{
    Dictionary<ResourceType, int> _resources;
    Item _handWeapon;
    Item _firearm;
    List<Item> _questItems = new List<Item>();
    List<Item> _soups = new List<Item>();
    private const int maxSoups = 10;

    public Inventory()
    {
        // we start with zero resource
        _resources = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in
                 System.Enum.GetValues(typeof(ResourceType)))
        {
            _resources.Add(type, 0);
        }
    }

    private void UpdateResourceCount(
        ResourceType type, int amount, GameObject ui = null)
    {
        _resources[type] = amount;
        if (ui != null)
        {
            var resourcePanel = ui.GetComponent<ResourcePanel>();
            if (resourcePanel != null)
                resourcePanel.SetResource(type, amount);
        }
    }

    public Dictionary<ResourceType, int> GetResources()
    {
        return _resources;
    }

    public void AddResource(
        ResourceType type, int amount, GameObject ui = null)
    {
        UpdateResourceCount(type, _resources[type] + amount, ui);
    }

    public void RemoveResource(
        ResourceType type, int amount, GameObject ui = null)
    {
        UpdateResourceCount(type, _resources[type] - amount, ui);
    }

    public bool HasEnoughResource(ResourceType type, int amount)
    {
        return _resources[type] >= amount;
    }

    public void DebugPrint() {
        foreach (ResourceType type in
                 System.Enum.GetValues(typeof(ResourceType)))
        {
            Debug.Log(type.ToString() + ": " + _resources[type]);
        }
    }

    public void CollectItem(Item item) {
        switch (item.type) {
            case ItemType.HandWeapon:
                SetHandWeapon(item);
                break;
            case ItemType.Firearm:
                SetFirearm(item);
                break;
            case ItemType.QuestItem:
                SetQuestItem(item);
                break;
            case ItemType.Consumable:
                AddSoup(item);
                break;
        }
    }

    void SetHandWeapon(Item item)
    {
        if (_handWeapon !=null) {
            // Drop the current hand weapon
        }
        _handWeapon = item;
    }

    void SetFirearm(Item item)
    {
        if (_firearm !=null) {
            // Drop the current firearm
        }
        _firearm = item;
    }

    void SetQuestItem(Item item)
    {
        _questItems.Add(item);
    }

    void AddSoup(Item item)
    {
        if (_soups.Count < maxSoups) {
            _soups.Add(item);
        } else {
            // drop the new soup
        }
    }

    public void Craft(Item item, GameObject ui = null) {
        foreach (var cost in item.cost)
        {
            RemoveResource(cost.type, cost.amount, ui);
        }
        CollectItem(item);
    }
}
