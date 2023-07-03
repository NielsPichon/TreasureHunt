using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class QuadNode
{
    private Vector3 position;
    private int size;
    private int depth;
    private List<QuadNode> children;
    private GameObject tile;


    public QuadNode(Vector3 position, int size, int depth) {
        this.position = position;
        this.size = size;
        this.depth = depth;
        this.children = new List<QuadNode>();
    }

    // Spawns a tile by either instantiating a new one or reusing one
    // from the pool
    public void Spawn(
        ref List<GameObject> tilePool,
        GameObject prefab,
        int mapSize
    ) {
        if (tilePool.Count > 0) {
            this.tile = tilePool[0];
            tilePool.RemoveAt(0);
        } else {
            this.tile = GameObject.Instantiate(prefab);
        }
        this.tile.SetActive(true);
        this.tile.GetComponent<BaseTile>().UpdateTile(
            this.size, this.position, mapSize);
    }

    bool IsInside(Vector3 target) {
        return (
            Mathf.Abs(target.x - this.position.x) < this.size
            && Mathf.Abs(target.z - this.position.z) < this.size
        );
    }

    void ReleaseTileIfNeeded(ref List<GameObject> tilePool) {
        if (this.tile != null) {
            this.tile.SetActive(false);
            tilePool.Add(this.tile);
            this.tile = null;
        }
    }

    void CreateChildren() {
        for (int i = -1; i < 2; i += 2) {
            for (int j = -1; j < 2; j += 2) {
                this.children.Add(
                    new QuadNode(
                        new Vector3(
                            this.position.x + i * this.size / 4,
                            this.position.y,
                            this.position.z + j * this.size / 4
                        ),
                        this.size / 2,
                        this.depth - 1
                    )
                );
            }
        }
    }

    void SubdivideIfNeeded(
        Vector3 target,
        ref List<GameObject> tilePool,
        ref List<QuadNode> needSpawning
    ) {
        // Subdivide if in bounds and not at max depth
        if (this.children.Count == 0 && depth > 0) {
            Debug.Log("Add children");
            CreateChildren();
            ReleaseTileIfNeeded(ref tilePool);
        } else if (this.depth == 0 && this.tile == null) {
            Debug.Log("Spawn self");
            // spawn a tile if at max depth and there is no tile because
            // this node cannot further subdivide
            needSpawning.Add(this);
        }
        // update children
        foreach (QuadNode child in this.children) {
            child.UpdateTree(target, ref tilePool, ref needSpawning);
        }
    }

    // Destroys children (if any) and mark self for spawning
    // tile if not already spawned
    void UnsubdivideIfNeeded(
        Vector3 target,
        ref List<GameObject> tilePool,
        ref List<QuadNode> needSpawning
    ) {
        if (this.children.Count > 0) {
            Debug.Log("Unsub remove children");
            foreach (QuadNode child in this.children) {
                child.DestroyNode(ref tilePool);
            }
            this.children.Clear();
        }

        if (this.tile == null) {
            Debug.Log("Unsub spawn self");
            needSpawning.Add(this);
        }
    }

    // Updates the quadtree by either subdividing or destroying.
    // If the node is subdivided, it will also update its children. A destroyed
    // node will add its tile object back to the pool of availble tiles or mark
    // itself for spawning. The idea of marking for spawn rather than spawning
    // immediately is to avoid spawning a tile and then destroying it in the
    // same frame, or spawning a tile and then destroying some others which
    // minimizes reuse.
    public void UpdateTree(
        Vector3 target,
        ref List<GameObject> tilePool,
        ref List<QuadNode> needSpawning
    ) {
        // influence radius is inside the node + half a node
        // (so 1 node away from the center), both horizontally and vertically.
        if (IsInside(target)) {
            if (this.children.Count == 0 && depth > 0) {Debug.Log("Subd");}
            SubdivideIfNeeded(target, ref tilePool, ref needSpawning);
        } else {
            if (this.children.Count > 0) {Debug.Log("Unsubd");}
            UnsubdivideIfNeeded(target, ref tilePool, ref needSpawning);
        }
    }

    // Manually destroy children and release tile to pool
    public void DestroyNode(ref List<GameObject> tilePool) {
        foreach (QuadNode child in this.children) {
            child.DestroyNode(ref tilePool);
        }
        this.children.Clear();
        ReleaseTileIfNeeded(ref tilePool);
    }

    public string Print(string prefix = "") {
        string result = (
            prefix + "Node: " + this.position + " size " + this.size
            + " depth " + this.depth + "\n");
        foreach (QuadNode child in this.children) {
            result += child.Print(prefix + "\t");
        }
        return result;
    }
}

public class QuadTreeSpawner : MonoBehaviour
{
    // object to target for deciding on the subdivision
    public GameObject target;
    // prefab to spawn7
    public GameObject prefab;
    // total size of the map
    public int mapSize = 256;
    // maximum recursion depth.
    public int maxDepth = 4;

    private QuadNode root;
    // pool of unused tiles
    private List<GameObject> tilePool = new List<GameObject>();
    // buffer of which nodes need spawning. This is to delay spawning untill all
    // unsuded tiles have been added to the tile pool. This also allows
    // multithreading spawning.
    private List<QuadNode> needSpawning = new List<QuadNode>();

    void SpawnTiles() {
        foreach(QuadNode node in needSpawning) {
            node.Spawn(ref tilePool, prefab, this.mapSize);
        }
        needSpawning.Clear();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (prefab.GetComponent<BaseTile>() == null) {
            Debug.LogError("Prefab must have a BaseTile component");
        }

        tilePool = new List<GameObject>();
        needSpawning = new List<QuadNode>();

        root = new QuadNode(new Vector3(0, 0, 0), mapSize, maxDepth);
        root.UpdateTree(
            target.transform.position, ref tilePool, ref needSpawning);
        SpawnTiles();
    }

    // Update is called once per frame
    void Update()
    {
        // Update the quadtree
        root.UpdateTree(
            target.transform.position, ref tilePool, ref needSpawning);
        // spawn new tiles (or move and rescale existing ones from the pool)
        SpawnTiles();
    }
}
