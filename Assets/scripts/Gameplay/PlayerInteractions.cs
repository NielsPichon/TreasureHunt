using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractions : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Reference to the UI for inventory")]
    public GameObject inventoryUI;
    [Tooltip("Prefab for progress bar")]
    public GameObject progressBarPrefab;
    [Header("Interactions")]
    [Tooltip("Distance from which player can interact with objects")]
    public float interactDistance = 2f;
    [Tooltip("Height of the raycast source from the ground")]
    public float raySourceHeight = 1.5f;
    [Tooltip("Pick-up cone angle in degrees")]
    public float coneAngle = 60f;
    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;

    private StarterAssets.ThirdPersonController _thirdPersonController;
    private Inventory _inventory;
    private StarterAssets.StarterAssetsInputs _input;
    private Interactable _closestInteractable;
    private float _remainingCollectionTime = 0.0f;
    private GameObject _progressBar;

    // Start is called before the first frame update
    void Start()
    {
        _inventory = new Inventory();
        _input = GetComponent<StarterAssets.StarterAssetsInputs>();
        _thirdPersonController = (
            GetComponent<StarterAssets.ThirdPersonController>());
    }

    public Dictionary<ResourceType, int> GetAvailableResources() {
        return _inventory.GetResources();
    }

    // Update is called once per frame. Retrieves the closest interactable
    void RayCastInteractables() {
        int layerMask = 1 << LayerMask.NameToLayer("Interactable");

        var closestHit = new RaycastHit();
        closestHit.distance = Mathf.Infinity;

        RaycastHit hit;
        Vector3 raySource = (
            transform.position + transform.up * raySourceHeight);
        float maxDist = interactDistance;
        Vector3 rayDirection = CinemachineCameraTarget.transform.forward;

        if (Physics.Raycast(
            raySource,
            rayDirection,
            out hit,
            maxDist,
            layerMask)
        ) {
            // Debug.DrawRay(
            //     raySource,
            //     rayDirection * hit.distance,
            //     Color.yellow,
            //     0.5f
            // );
            if (hit.distance < closestHit.distance) {
                closestHit = hit;
            }
        }
        else
        {
            // Debug.DrawRay(
            //     raySource,
            //     rayDirection * maxDist,
            //     Color.red,
            //     0.5f
            // );
        }

        if (closestHit.distance < Mathf.Infinity) {
            var interactable = (
                closestHit.collider.gameObject.GetComponent<Interactable>());
            if (interactable != null) {
                interactable.ToggleHighlight(true);
                if (interactable != _closestInteractable) {
                    if (_closestInteractable != null) {
                        _closestInteractable.ToggleHighlight(false);
                    }
                    _closestInteractable = interactable;
                }
            }
        } else {
            if (_closestInteractable != null) {
                _closestInteractable.ToggleHighlight(false);
                _closestInteractable = null;
            }
        }
    }

    void InterruptCollection() {
        _remainingCollectionTime = 0.0f;
        if (_closestInteractable != null) {
            _closestInteractable.StopInteraction();
        }
        _input.interact = false;
        if (_progressBar != null) {
            Destroy(_progressBar);
        }
    }

    void TryInteract() {
        // if collecting, continue collecting until mining time is complete
        if (_remainingCollectionTime > 0.0f) {
            _remainingCollectionTime -= Time.deltaTime;
            if (_remainingCollectionTime <= 0.0f) {
                InterruptCollection();
                var resource = (
                    _closestInteractable.gameObject.GetComponent<Resource>());
                if (resource != null) {
                    Debug.Log("Adding " + resource.amount
                                + " " + resource.type + " to inventory");
                    _inventory.AddResource(
                        resource.type, resource.amount, ui: inventoryUI);
                }
            }
            return;
        }
        // otherwise if interact is pressed, start interacting
        else if (_input.interact) {
            if (!_thirdPersonController.Grounded) {
                _input.interact = false;
                return;
            }
            if (_closestInteractable != null) {
                _closestInteractable.StartInteraction();
                var resource = (
                    _closestInteractable.gameObject.GetComponent<Resource>());
                if (resource != null) {
                    _remainingCollectionTime = resource.miningTime;
                    _progressBar = Instantiate(progressBarPrefab);
                    _progressBar.transform.position = (
                        transform.position + transform.up * 2f);
                    _progressBar.transform.SetParent(transform);
                    _progressBar.GetComponentInChildren<ProgressBar>()
                        .SetMaxTime(resource.miningTime);
                }
            }
        }
    }

    public void Craft(Item item) {
        _inventory.Craft(item, ui: inventoryUI);
    }

    void MaybeInteruptCollection() {
        // any movement will interrupt collection
        if (_input.InteruptInteraction())
        {
            InterruptCollection();
        }
    }

    void DebugInventory() {
        if (Input.GetKeyDown(KeyCode.E)) {
            _inventory.AddResource(ResourceType.Steal, 1, ui: inventoryUI);
        } else if (Input.GetKeyDown(KeyCode.A)) {
            if (_inventory.HasEnoughResource(ResourceType.Steal, 1)) {
                _inventory.RemoveResource(
                    ResourceType.Steal, 1, ui: inventoryUI);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_remainingCollectionTime <= 0.0f) {
            // only update "closest" interactable if not collecting to avoid
            // overriding the collected resource
            RayCastInteractables();
        }
        TryInteract();
        MaybeInteruptCollection();
        // DebugInventory();
    }
}
