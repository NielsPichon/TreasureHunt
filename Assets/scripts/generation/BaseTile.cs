using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseTile : MonoBehaviour
{
    public int tileSize = 64;
    public Vector3 position;

    public abstract void UpdateTile(
        int tileSize, Vector3 position, int mapSize);
}
