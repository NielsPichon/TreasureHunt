using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugPlaneTile : BaseTile
{
    public override void UpdateTile(
        int tileSize,
        Vector3 position,
        int mapSize
    ) {
        this.tileSize = tileSize;
        this.position = position;
        transform.position = position;
        transform.localScale = new Vector3(
            tileSize / 10.0f, 1, tileSize / 10.0f);
    }
}
