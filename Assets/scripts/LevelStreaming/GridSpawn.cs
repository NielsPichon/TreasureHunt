using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSpawn : MonoBehaviour
{
    public GameObject prefab;
    public int mapSize = 256;
    public int numDiv = 1;

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < numDiv; i++) {
            for (int j = 0; j < numDiv; j++) {
                GameObject tile = GameObject.Instantiate(prefab);
                tile.GetComponent<BaseTile>().UpdateTile(
                    mapSize / numDiv,
                    new Vector3(
                        i * mapSize / numDiv - mapSize / 2,
                        0,
                        j * mapSize / numDiv - mapSize / 2
                    ),
                    mapSize
                );
            }
        }
    }
}
