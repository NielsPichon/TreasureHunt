using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[CustomEditor(typeof(CraftMenuItem))]
public class CraftMenuItemEditor : Editor
{
    override public void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate"))
        {
            (target as CraftMenuItem).Generate();
        }
    }
}

[ExecuteInEditMode]
public class CraftMenuItem : MonoBehaviour
{
    [Tooltip("The item type")]
    public Item itemType;
    [Tooltip("The prefab for the cost resource")]
    public GameObject costResourcePrefab;

    [NonSerialized]
    public int index;
    [NonSerialized]
    public bool available = true;

    private bool _isSelected = false;
    private Action<int> _hoverCallback;


    // Start is called before the first frame update
    void Awake()
    {
        Generate();
    }

    public void SetAvailable(bool isAvailable) {
        available = isAvailable;
        if (isAvailable) {
            GetComponent<Image>().color = Color.white;
            SetHoverCallback(_hoverCallback);
        } else {
            GetComponent<Image>().color = Color.gray;
            GetComponent<EventTrigger>().triggers[0].callback
                .RemoveAllListeners();
        }
    }

    public void SetHoverCallback(Action<int> callback) {
        if (callback == null) {
            return;
        }
        var trigger = GetComponent<EventTrigger>();
        _hoverCallback = callback;
        trigger.triggers[0].callback.AddListener((data) => {
            callback(index);
        });
    }

    public void Generate() {
        var objectPres = transform.GetChild(0);
        objectPres.GetComponentInChildren<TMPro.TextMeshProUGUI>()
            .text = itemType.description;
        objectPres.GetComponentInChildren<Image>().sprite = itemType.sprite;

        var costPanel = transform.GetChild(1);
        // remove existing children
        for (int i = costPanel.transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(costPanel.transform.GetChild(i).gameObject);
        }
        foreach (var resourceCost in itemType.cost)
        {
            GameObject resource = Instantiate(costResourcePrefab, costPanel);
            var sprite = ResourceAppearance.sprites[resourceCost.type];
            resource.GetComponentInChildren<Image>().sprite = sprite;
            resource.GetComponentInChildren<TMPro.TextMeshProUGUI>()
                .text = resourceCost.amount.ToString();
        }
    }

    public void SetSelected(bool isSelected) {
        _isSelected = isSelected;
        if (!isSelected) {
            // deselect
            var color = available ? Color.white : Color.gray;
            GetComponent<Image>().color = color;
        } else {
            if (!available) {
                _hoverCallback(index + 1);
                return;
            }
            // select
            GetComponent<Image>().color = Color.green;
        }
    }
}
