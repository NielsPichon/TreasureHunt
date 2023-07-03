using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CraftMenu : MonoBehaviour
{
    public List<Item> items;
    public GameObject itemEntryPrefab;

    [HideInInspector]
    public StarterAssets.StarterAssetsInputs input;

    private List<GameObject> _itemEntries;
    private int _activeEntry = 0;
    private const float _threshold = 0.01f;
    private bool _noActiveEntry = false;

    // Start is called before the first frame update
    void Start()
    {
        _itemEntries = new List<GameObject>();
        Generate();
    }

    void Generate() {
        // remove existing children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        int counter = 0;
        foreach (var item in items)
        {
            GameObject itemEntry = Instantiate(itemEntryPrefab, transform);
            itemEntry.transform.SetParent(transform);
            var menuItem = itemEntry.GetComponent<CraftMenuItem>();
            menuItem.itemType = item;
            menuItem.Generate();
            menuItem.index = counter;
            menuItem.SetHoverCallback(UpdateSelected);
            _itemEntries.Add(itemEntry);
            counter++;
        }

        _activeEntry = -1;
    }

    public Item GetSelectedItem() {
        if (_noActiveEntry || _activeEntry < 0
            || !_itemEntries[_activeEntry]
                .GetComponent<CraftMenuItem>().available) {
            return null;
        }
        return items[_activeEntry];
    }

    void UpdateSelected(int index) {
        if (_noActiveEntry) {
            return;
        }

        // wrap around
        if (index < 0) {
            index = _itemEntries.Count + index;
        } else if (index >= _itemEntries.Count) {
            index = index % _itemEntries.Count;
        }

        if (index != _activeEntry) {
            if (_activeEntry >= 0) {
                _itemEntries[_activeEntry]
                    .GetComponent<CraftMenuItem>()
                    .SetSelected(false);
            }
            _activeEntry = index;
            _itemEntries[_activeEntry]
                .GetComponent<CraftMenuItem>()
                .SetSelected(true);
        }
    }

    public void CheckAvailability(Dictionary<ResourceType, int> resources) {
        _noActiveEntry = true;
        int counter = 0;
        foreach (var item in items)
        {
            bool available = true;
            foreach (var cost in item.cost)
            {
                if (!resources.ContainsKey(cost.type)
                    || resources[cost.type] < cost.amount
                ) {
                    available = false;
                    break;
                }
            }
            if (available) {
                _noActiveEntry = false;
                var menuItem =_itemEntries[counter]
                    .GetComponent<CraftMenuItem>();
                menuItem.SetAvailable(true);
                menuItem.SetHoverCallback(UpdateSelected);

            } else {
                _itemEntries[counter].GetComponent<CraftMenuItem>()
                    .SetAvailable(false);
            }

            counter++;
        }
        // reset active selection to first entry
        foreach (var item in _itemEntries)
        {
            item.GetComponent<CraftMenuItem>().SetSelected(false);
            _activeEntry = -1;
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (input == null) {
            return;
        }

        if (Mathf.Abs(input.move.y) > _threshold && !_noActiveEntry) {
            UpdateSelected(_activeEntry - (int)Mathf.Sign(input.move.y));
            input.move.y = 0;
        }
    }
}
