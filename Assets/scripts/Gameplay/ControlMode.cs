using System.Collections;
using System.Collections.Generic;
using UnityEngine;

enum ControlType
{
    Disabled,
    Crafting,
    Building,
    Roaming
}

public class ControlMode : MonoBehaviour
{
    public GameObject CraftMenuPrefab;
    private ControlType _controlType = ControlType.Roaming;
    private StarterAssets.StarterAssetsInputs _input;
    private StarterAssets.ThirdPersonController _movements;
    private PlayerInteractions _interactions;


    // Start is called before the first frame update
    void Start()
    {
        _input = GetComponent<StarterAssets.StarterAssetsInputs>();

        _movements = GetComponent<StarterAssets.ThirdPersonController>();
        _interactions = GetComponent<PlayerInteractions>();

        CraftMenuPrefab.GetComponent<CraftMenu>().input = _input;

        OnControlModeChange(ControlType.Roaming);
    }

    void ToggleCursor(bool enable) {
        if (enable) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        } else {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void ToggleCrafting(bool enable) {
        if (enable) {
            CraftMenuPrefab.GetComponent<CraftMenu>().CheckAvailability(
                _interactions.GetAvailableResources());
        }
        CraftMenuPrefab.SetActive(enable);
    }

    void ToggleMovements(bool enable) {
        _movements.ToggleMovementForbidden(!enable);
    }

    void ToggleInteractions(bool enable) {
        _interactions.enabled = enable;
    }

    void OnControlModeChange(ControlType type)
    {
        _controlType = type;
        switch (type)
        {
            case ControlType.Crafting:
                ToggleCursor(true);
                ToggleCrafting(true);
                ToggleMovements(false);
                ToggleInteractions(false);
                break;
            case ControlType.Building:
                ToggleCursor(false);
                ToggleCrafting(false);
                ToggleMovements(true);
                ToggleInteractions(false);
                break;
            case ControlType.Roaming:
                ToggleCursor(false);
                ToggleCrafting(false);
                ToggleMovements(true);
                ToggleInteractions(true);
                break;
            case ControlType.Disabled:
                ToggleCursor(true);
                ToggleCrafting(false);
                ToggleMovements(false);
                ToggleInteractions(false);
                break;
        }
    }

    void MaybeToggleBuildMode() {
        if (_input.build) {
            // if (_controlType == ControlType.Building) {
            //     OnControlModeChange(ControlType.Roaming);
            // } else {
            //     OnControlModeChange(ControlType.Building);
            // }
            _input.build = false;
        }
    }

    void MaybeToggleCraftMenu() {
        if (_input.craft) {
            if (_controlType == ControlType.Crafting) {
                OnControlModeChange(ControlType.Roaming);
            } else if (_movements.Grounded) {
                // only allow crafting when grounded
                OnControlModeChange(ControlType.Crafting);
            }
            _input.craft = false;
        }
    }

    void MaybeReturnToRoaming() {
        if (_input.cancel) {
            if (_controlType == ControlType.Crafting
                || _controlType == ControlType.Building) {
                OnControlModeChange(ControlType.Roaming);
            }
            _input.cancel = false;
        }
    }

    void MaybeValidate() {
        // If we don't manually reset the flag f not inside a menu, it would
        // stay true and when we would enter a menu, it could validate instantly
        if (_input.validate) {
            if (_controlType == ControlType.Crafting) {
                Item craftedItem = CraftMenuPrefab.GetComponent<CraftMenu>()
                    .GetSelectedItem();
                if (craftedItem != null) {
                    _interactions.Craft(craftedItem);
                    OnControlModeChange(ControlType.Roaming);
                }
            }
            _input.validate = false;
        }
    }

    void Update() {
        MaybeToggleBuildMode();
        MaybeToggleCraftMenu();
        MaybeReturnToRoaming();
        MaybeValidate();
    }
}
