using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;


[CustomEditor(typeof(ResourcePanel))]
public class ResourcePanelEditor : Editor
{
    override public void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Populate"))
        {
            (target as ResourcePanel).PopulatePannel();
        }
    }
}


[ExecuteInEditMode]
public class ResourcePanel : MonoBehaviour
{
    public GameObject resourcePrefab;
    Dictionary<ResourceType, GameObject> resourceWidgets;

    public void PopulatePannel() {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        resourceWidgets = new Dictionary<ResourceType, GameObject>();
        foreach (ResourceType type in
                 System.Enum.GetValues(typeof(ResourceType)))
        {
            if (resourceWidgets.ContainsKey(type))
            {
                continue;
            }
            GameObject resource = Instantiate(resourcePrefab, transform);
            var sprite = ResourceAppearance.sprites[type];
            resource.GetComponent<Image>().sprite = sprite;
            resourceWidgets.Add(type, resource);
        }
    }

    // Start is called before the first frame update
    void Awake()
    {
        PopulatePannel();
    }

    public void SetResource(ResourceType type, int amount)
    {
        var textMesh = (
            resourceWidgets[type]
            .GetComponentInChildren<TMPro.TextMeshProUGUI>()
        );
        if (textMesh != null) {
            textMesh.text = amount.ToString();
        }
    }
}
