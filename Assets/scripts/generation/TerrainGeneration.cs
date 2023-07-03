// # define TIMEIT

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;



public enum DebugMats {
    HeightMap,
    WaterMask,
    DistanceField,
    Uvs,
    Regions,
    Biomes,
    Shore
}

// Custom PRNG class to avoid using UnityEngine.Random or System.Random which I
// guess you could use but is too much of a hastle to solve namespace conflicts.
public class PRNG {
    private float state;

    public PRNG(float seed = 0.0f) {
        this.state = seed;
    }

    public float next() {
        this.state = Mathf.Abs(
            Mathf.Sin(this.state * 12.9898f + 78.233f)) * 43758.5453f;
        return this.state - Mathf.Floor(this.state);
    }

    public float range(float min, float max) {
        return min + (max - min) * this.next();
    }
}

public class TerrainGeneration : BaseTile
{
    [Header("Shaders")]
    // shader to generate the heightmap
    public ComputeShader terrainShader;
    // shader to generate the mesh
    public ComputeShader meshGenerationShader;
    // shader to generate the distance fields and other math utilities
    public ComputeShader mathHelperShader;
    //shader to generate the biomes and other voronoi based stuff
    public ComputeShader samplersShader;
    // debug material shader
    public ComputeShader DebugMatShader;

    [Header("General settings")]
    // Number of vertices per side of the mesh. Must be a multiple of 64
    public int resolution = 64;
    // If zoomed in, a smaller range will be desplayed.
    public float zoom = 1.0f;
    // seed for the noise
    public float seed = 0.0f;
    // Tile offset (used when multiple terain chunks are used)
    public float[] offset = {0.0f, 0.0f};

    [Header("Island settings")]
    // The water mask resolution. Must be a multiple of 64. Thi will get
    // resampled to the terrain resolution and position.
    public int islandResolution = 256;
    // Scale of the noise added to the island diameter
    public float islandNoiseScale = 20.0f;
    // How round the island is. 1 is a perfect circle, 0 is super noisy
    public float islandRoundness = 0.5f;

    // how fast the influence of the water drops off
    public float distanceFieldEffect = 0.3f;
    // how much the distance field is smoothed
    public int edtIterations = 32;
    // Curve which generates the shore mask
    public AnimationCurve shoreMaskCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("River settings")]
    // how large the river is (in 0-1)
    public float riverWidth = 0.01f;
    // how much the river center is jittered w.r.t the center of the map
    public float riverCenterJitter = 0.2f;
    // scale of the noise added to the river path
    public float riverNoiseScale = 10.0f;
    // amount of noise applied to the river path
    public float riverNoiseAmount = 0.2f;

    [Header("Biome settings")]
    // point radius for the biom pseudo voronoi grid
    public float biomeCenterRadius = 0.2f;
    // how much the biome centers are jittered, as a factor of the radius
    public float biomeGridDistortion = 2.0f;
    // Biome edge noise frequency
    public float samplerNoiseFreq = 30.0f;
    // Biome edge noise amplitude
    public float samplerNoiseFactor = 0.2f;
    // Biome configs. NOTE: The beach biome should be included as it's own thing
    public BiomeConfig[] biomes;
    // Beach/shore biome
    public BiomeConfig beachBiome;

    [Header("Mesh settings")]
    // max height of the terrain
    public float maxHeight = 20.0f;
    // height of each terrain level
    public float levelHeight = 1.0f;

    [Header("Debug")]
    // Allows debugging as a standalone prefab
    public bool debugGenerateOnStart = false;
    // whether to apply the shaping function
    public bool applyShapping = false;
    // whether to apply the water mask
    public bool addMask = true;
    // whether the masking should be applied to all octaves. If false, octaves
    // that contribute for less than 1% of the total height will not be weighted
    // by the distance field. This proved to be very akward when walking
    // on such terrain.
    public bool maskAllOctaves = true;
    // whether to level the terrain
    public bool levelTerrain = false;
    // whether we only want to look at the debug mat or actually
    // generate the mesh
    public bool debugMatOnly = false;
    // Show distance field
    public DebugMats debugMat = DebugMats.HeightMap;

    private static int THREAD_GROUP_SIZE = 64;
    // we used an instanced System.Random instead of UnityEngine.Random because
    // this way each tile will have its own PRNG. Otherwise, 2 tiles generated
    // simultaneously would basically have different results.
    private PRNG prng;
    // We store the water distance field not to have to recompute
    // it every single time
    private float[] distanceFieldBuffer;
    // Center of each biome
    private Vector2[] biomeCenters;


    struct Vertex {
        public Vector3 position;
        public Vector2 uv;
        public Vector3 normal;

        public static int Size() {
            return (sizeof(float) * 3 + sizeof(float) * 2 + sizeof(float) * 3);
        }
    }


    void GenerateHeightMap(
            out ComputeBuffer heightMap, ref BiomeConfig biome) {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif

        heightMap = new ComputeBuffer(
            resolution * resolution, sizeof(float));
        terrainShader.SetInt("size", resolution);
        terrainShader.SetInt("octaves", biome.octaves);
        terrainShader.SetFloat("scale", biome.scale);
        terrainShader.SetFloat("lacunarity", biome.lacunarity);
        terrainShader.SetFloat("persistence", biome.persistence);
        terrainShader.SetFloat("seed", seed);
        terrainShader.SetFloat("maxHeight", maxHeight);

        if (addMask && !maskAllOctaves) {
            ComputeBuffer distanceField;
            distanceField = new ComputeBuffer(
                islandResolution * islandResolution, sizeof(float));
            distanceField.SetData(distanceFieldBuffer);

            terrainShader.SetFloat("distanceFieldEffect", distanceFieldEffect);

            int kernel = terrainShader.FindKernel("MaskedHeightMap");
            terrainShader.SetBuffer(kernel, "distanceField", distanceField);
            terrainShader.SetBuffer(kernel, "heightMap", heightMap);
            terrainShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

            if (debugMat == DebugMats.DistanceField) {
                GenDebugMat(ref distanceField, islandResolution, 1.0f);
            }
            distanceField.Dispose();
        } else {
            int kernel = terrainShader.FindKernel("HeightMap");
            terrainShader.SetBuffer(kernel, "heightMap", heightMap);
            terrainShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
        }

        if (applyShapping) {
            float[] shapingFunction;
            ConvertShapingCurveToArray(
                ref biome.heightCurve, out shapingFunction);
            var shapingBuffer = new ComputeBuffer(
                shapingFunction.Length, sizeof(float));
            shapingBuffer.SetData(shapingFunction);
            int kernel = terrainShader.FindKernel("ApplyShapingFunction");
            terrainShader.SetBuffer(kernel, "heightMap", heightMap);
            terrainShader.SetBuffer(kernel, "shapingFunction", shapingBuffer);
            terrainShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
            shapingBuffer.Dispose();
        }

        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("Base noise gen took " + (end - start) + " seconds");
        #endif
    }

    void InitBiomes() {
        int numPoints = (int)Mathf.Floor(1.0f / biomeCenterRadius);
        ComputeBuffer points = new ComputeBuffer(
            numPoints * numPoints, sizeof(float) * 2);

        samplersShader.SetInt("numPoints", numPoints);
        samplersShader.SetFloat("seed", seed);
        samplersShader.SetFloat("gridDistortion", biomeGridDistortion);

        var kernel = samplersShader.FindKernel("DisplacedGridSampler");
        samplersShader.SetBuffer(kernel, "points", points);
        samplersShader.Dispatch(
            kernel,
            (int)Mathf.Ceil(numPoints * numPoints / (float)THREAD_GROUP_SIZE),
            1,
            1
        );

        biomeCenters = new Vector2[numPoints * numPoints];
        points.GetData(biomeCenters);

        points.Dispose();
    }

    void GenerateBiomes(
            out ComputeBuffer biomesMasks) {
        ComputeBuffer points = new ComputeBuffer(
            biomeCenters.Length, sizeof(float) * 2);
        points.SetData(biomeCenters);

        ComputeBuffer closestPoint = new ComputeBuffer(
            resolution * resolution, sizeof(int));
        ComputeBuffer regions = new ComputeBuffer(
            resolution * resolution, sizeof(int));
        regions.SetData(
            Enumerable.Repeat(0, resolution * resolution).ToArray());
        ComputeBuffer distToCenter = new ComputeBuffer(
            resolution * resolution, sizeof(float));

        int numPoints = (int)Mathf.Floor(1.0f / biomeCenterRadius);
        samplersShader.SetInt("numPoints", numPoints);
        samplersShader.SetFloat("seed", seed);
        samplersShader.SetFloat("samplerNoiseFreq", samplerNoiseFreq);
        samplersShader.SetFloat("samplerNoiseFactor", samplerNoiseFactor);
        samplersShader.SetFloat("zoom", zoom);
        samplersShader.SetInt("size", resolution);
        samplersShader.SetVector(
            "offset", new Vector4(offset[0], offset[1], 0, 0));
        samplersShader.SetInt("numProperties", biomes.Length);


        var kernel = samplersShader.FindKernel("ClosestPointOnDisplacedGrid");
        samplersShader.SetBuffer(kernel, "points", points);
        samplersShader.SetBuffer(kernel, "closestPoint", closestPoint);
        samplersShader.SetBuffer(kernel, "distanceToCenter", distToCenter);
        samplersShader.SetBuffer(kernel, "regions", regions);
        samplersShader.Dispatch(
            kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

        closestPoint.Dispose();
        points.Dispose();

        if (debugMat == DebugMats.Regions) {
            GenDebugBiomeMat(ref regions);
        }

        biomesMasks = new ComputeBuffer(
            resolution * resolution * biomes.Length, sizeof(float));
        kernel = samplersShader.FindKernel("MasksFromRegions");
        samplersShader.SetBuffer(kernel, "regions", regions);
        samplersShader.SetBuffer(kernel, "mask", biomesMasks);
        samplersShader.Dispatch(
            kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

        regions.Dispose();
        distToCenter.Dispose();
    }

    void Islandify(ref ComputeBuffer waterMask) {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif
        var kernel = terrainShader.FindKernel("MakeIslandMask");
        terrainShader.SetFloat("islandNoiseScale", islandNoiseScale);
        terrainShader.SetFloat("islandRoundness", islandRoundness);
        terrainShader.SetInt("waterMaskSize", islandResolution);
        terrainShader.SetBuffer(kernel, "waterMask", waterMask);
        terrainShader.Dispatch(
            kernel,
            islandResolution * islandResolution / THREAD_GROUP_SIZE,
            1,
            1
        );
        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("Island gen took " + (end - start) + " seconds");
        #endif
    }

    void GenerateRiver(ref ComputeBuffer waterMask) {
        #if TIMEIT
            var startTime = Time.realtimeSinceStartup;
        #endif
        // jitter center
        var center = new Vector4(0.5f, 0.5f);
        center.x += prng.range(-riverCenterJitter, riverCenterJitter);
        center.y += prng.range(-riverCenterJitter, riverCenterJitter);

        // choose random start and end sides
        bool endSide = prng.next() > 0.5f;
        float fracEnd = prng.range(0.25f, 0.75f);

        float fracStart = prng.range(0.25f, 0.75f);
        var start = new Vector4(0.0f, fracStart);
        var end = new Vector4(
            endSide ? fracEnd : 1.0f,
            endSide ? 0.0f : fracEnd
        );

        terrainShader.SetFloat("riverWidth", riverWidth);
        terrainShader.SetVector("center", center);
        terrainShader.SetVector("start", start);
        terrainShader.SetVector("end", end);
        terrainShader.SetFloat("riverNoiseScale", riverNoiseScale);
        terrainShader.SetFloat("riverNoiseAmount", riverNoiseAmount);

        var kernel = terrainShader.FindKernel("GenerateRiver");
        terrainShader.SetBuffer(kernel, "waterMask", waterMask);
        terrainShader.Dispatch(
            kernel,
            islandResolution * islandResolution / THREAD_GROUP_SIZE,
            1,
            1
        );
        #if TIMEIT
            var endTime = Time.realtimeSinceStartup;
            Debug.Log("River gen took " + (endTime - startTime) + " seconds");
        #endif
    }

    void LevelHeightMap(ref ComputeBuffer heightMap) {
        terrainShader.SetFloat("levelHeight", levelHeight);

        var kernel = terrainShader.FindKernel("Leveling");
        terrainShader.SetBuffer(kernel, "heightMap", heightMap);
        terrainShader.Dispatch(
            kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
    }

    void GenerateMesh(ref ComputeBuffer heightMap,
                      out ComputeBuffer vertexDataBuffer,
                      out Vertex[] vertexData,
                      out ComputeBuffer trisBuffer) {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif

        // init heightmap buffers
        int numVtx = (resolution - 2) * (resolution - 2);
        vertexData = new Vertex[numVtx];
        for (int i = 0; i < numVtx; i++) {
            vertexData[i] = new Vertex();
        }
        vertexDataBuffer = new ComputeBuffer(
            numVtx, Vertex.Size());
        vertexDataBuffer.SetData(vertexData);

        trisBuffer = new ComputeBuffer(
            (resolution - 2 - 1) * (resolution - 2 - 1) * 6, sizeof(int));

        meshGenerationShader.SetInt("size", resolution);
        meshGenerationShader.SetInt("width", tileSize);

        var meshKernel = meshGenerationShader.FindKernel("GenerateMesh");
        meshGenerationShader.SetBuffer(meshKernel, "heightMap", heightMap);
        meshGenerationShader.SetBuffer(meshKernel, "vertices", vertexDataBuffer);
        meshGenerationShader.SetBuffer(meshKernel, "tris", trisBuffer);
        meshGenerationShader.Dispatch(
            meshKernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

        // TODO: implement if leveling
        // var smoothKernel = meshGenerationShader.FindKernel("SmoothEdges");
        // meshGenerationShader.SetBuffer(smoothKernel, "vertices", vertexData);
        // meshGenerationShader.Dispatch(
        //     smoothKernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("Mesh gen took " + (end - start) + " seconds");
        #endif
    }

    void UpdateMesh(Vertex[] vertexData, int[] tris) {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif
        var mesh = GetComponent<MeshFilter>().mesh;
        mesh.SetVertices(vertexData.Select(v => v.position).ToList());
        mesh.SetUVs(0, vertexData.Select(v => v.uv).ToList());
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(vertexData.Select(v => v.normal).ToList());
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("Mesh update took " + (end - start) + " seconds");
        #endif
    }

    void GenDebugMat(
        ref ComputeBuffer heightMapBuffer,
        int res,
        float height
    ) {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif
        RenderTexture debugTex = new RenderTexture(res, res, 24);
        debugTex.enableRandomWrite = true;
        debugTex.Create();

        DebugMatShader.SetInt("size", res);
        DebugMatShader.SetFloat("maxHeight", height);
        var mapKernel = DebugMatShader.FindKernel("HeightMap");
        DebugMatShader.SetTexture(mapKernel, "Result", debugTex);
        DebugMatShader.SetBuffer(mapKernel, "heightMap", heightMapBuffer);
        DebugMatShader.Dispatch(mapKernel, debugTex.width / 8,
                                debugTex.height / 8, 1);

        GetComponent<Renderer>().material.mainTexture = debugTex;
        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("Material gen took " + (end - start) + " seconds");
        #endif
    }

    void GenDebugBiomeMat(ref ComputeBuffer regions, bool isFloat=false) {
        RenderTexture debugTex = new RenderTexture(resolution, resolution, 24);
        debugTex.enableRandomWrite = true;
        debugTex.Create();

        string kernelName = isFloat ? "RandomRegionFloat" : "RandomRegion";
        string bufferName = isFloat ? "regionFloat" : "region";

        var mapKernel = DebugMatShader.FindKernel(kernelName);
        DebugMatShader.SetTexture(mapKernel, "Result", debugTex);
        DebugMatShader.SetBuffer(mapKernel, bufferName, regions);
        DebugMatShader.SetInt("size", resolution);
        DebugMatShader.Dispatch(mapKernel, debugTex.width / 8,
                                debugTex.height / 8, 1);
        GetComponent<Renderer>().material.mainTexture = debugTex;
    }

    void GenUVsDebugMat() {
        RenderTexture debugTex = new RenderTexture(resolution, resolution, 24);
        debugTex.enableRandomWrite = true;
        debugTex.Create();

        var mapKernel = DebugMatShader.FindKernel("UVSpace");
        DebugMatShader.SetTexture(mapKernel, "Result", debugTex);
        DebugMatShader.Dispatch(mapKernel, debugTex.width / 8,
                                debugTex.height / 8, 1);
        GetComponent<Renderer>().material.mainTexture = debugTex;
    }

    void NormalizeBuffer(ref ComputeBuffer inputBuffer, int bufferSize) {

        float[] input = new float[bufferSize];
        inputBuffer.GetData(input);

        // TODO: Get the max value from the GPU

        // var reduceKernel = mathHelperShader.FindKernel("MaxReduce");
        // mathHelperShader.SetBuffer(reduceKernel, "inputBuffer", inputBuffer);

        // var outputBuffer = new ComputeBuffer(
        //     bufferSize / THREAD_GROUP_SIZE, sizeof(float));
        // mathHelperShader.SetBuffer(
        //     reduceKernel, "outputBuffer", outputBuffer);

        // int numGroups = bufferSize;
        // while (numGroups / THREAD_GROUP_SIZE >= 1) {
        //     mathHelperShader.Dispatch(
        //         reduceKernel, numGroups / THREAD_GROUP_SIZE, 1, 1);
        //     numGroups /= THREAD_GROUP_SIZE;

        //     mathHelperShader.SetBuffer(
        //         reduceKernel, "inputBuffer", outputBuffer);
        // }


        var outputBuffer = new ComputeBuffer(1, sizeof(float));
        outputBuffer.SetData(new float[] { input.Max() });

        var normalizeKernel = mathHelperShader.FindKernel("Normalize");
        mathHelperShader.SetBuffer(normalizeKernel, "inputBuffer", inputBuffer);
        mathHelperShader.SetBuffer(normalizeKernel, "maxBuffer", outputBuffer);
        mathHelperShader.Dispatch(
            normalizeKernel, bufferSize / THREAD_GROUP_SIZE, 1, 1);

        outputBuffer.Dispose();
    }

    void ComputeDistanceField(ref ComputeBuffer waterMask,
                              ref ComputeBuffer distanceField) {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif


        var kernel = mathHelperShader.FindKernel("ComputeEDT");
        mathHelperShader.SetInt("size", islandResolution);
        mathHelperShader.SetBuffer(kernel, "mask", waterMask);
        mathHelperShader.SetBuffer(kernel, "distanceFieldOut", distanceField);

        float[] distanceInit = Enumerable.Repeat(
            float.PositiveInfinity, islandResolution * islandResolution)
            .ToArray();
        ComputeBuffer tmpBuffer = new ComputeBuffer(
            islandResolution * islandResolution, sizeof(float));
        tmpBuffer.SetData(distanceInit);



        // do a few iterations of EDT
        for (int i = 0; i < edtIterations; i++) {
            if (i % 2 == 0) {
                mathHelperShader.SetBuffer(
                    kernel, "distanceFieldIn", tmpBuffer);
                mathHelperShader.SetBuffer(
                    kernel, "distanceFieldOut", distanceField);
            } else {
                mathHelperShader.SetBuffer(
                    kernel, "distanceFieldIn", distanceField);
                mathHelperShader.SetBuffer(
                    kernel, "distanceFieldOut", tmpBuffer);
            }
            mathHelperShader.Dispatch(
                kernel,
                islandResolution * islandResolution / THREAD_GROUP_SIZE,
                1,
                1
            );
        }
        if (edtIterations % 2 == 0) {
            distanceField.Dispose();
            distanceField = tmpBuffer;
        } else {
            tmpBuffer.Dispose();
        }


        NormalizeBuffer(ref distanceField, islandResolution * islandResolution);
        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("EDT + Norm took " + (end - start) + " seconds");
        #endif
    }

    void ApplyDistanceField(ref ComputeBuffer heightMap, bool binary=false) {
        ComputeBuffer distanceField = new ComputeBuffer(
            islandResolution * islandResolution, sizeof(float));
        distanceField.SetData(distanceFieldBuffer);

        terrainShader.SetFloat("distanceFieldEffect", distanceFieldEffect);
        var kernel = terrainShader.FindKernel(
            binary ? "ApplyWaterMask" : "ApplyDistanceField");
        terrainShader.SetBuffer(kernel, "heightMap", heightMap);
        terrainShader.SetBuffer(kernel, "distanceField", distanceField);
        terrainShader.Dispatch(
            kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

        if (debugMat == DebugMats.DistanceField) {
            GenDebugMat(ref distanceField, islandResolution, 1.0f);
        }

        distanceField.Dispose();
    }

    void ConvertShapingCurveToArray(
            ref AnimationCurve heightCurve, out float[] shapingFunction) {
        shapingFunction = new float[100];
        for (int i = 0; i < 100; i++) {
            shapingFunction[i] = heightCurve.Evaluate(
                (float)i / (float)100.0f);
        }
    }


    void BlurMasks(ref ComputeBuffer mask) {
        var kernel = mathHelperShader.FindKernel("GaussianBlur1D");
        mathHelperShader.SetInt("size", resolution);

        ComputeBuffer outputBuffer = new ComputeBuffer(
            resolution * resolution * biomes.Length, sizeof(float));

        for (int i = 1; i < biomes.Length; i++) {
            mathHelperShader.SetInt("axis", i);

            mathHelperShader.SetBuffer(kernel, "inputBuffer", mask);
            mathHelperShader.SetBuffer(kernel, "outputBuffer", outputBuffer);
            mathHelperShader.SetInt("axis", 0);
            mathHelperShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

            mathHelperShader.SetBuffer(kernel, "inputBuffer", outputBuffer);
            mathHelperShader.SetBuffer(kernel, "outputBuffer", mask);
            mathHelperShader.SetInt("axis", 1);
            mathHelperShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
        }
    }

    void CombineHeights(
            ref ComputeBuffer[] heightMaps,
            ref ComputeBuffer biomeMasks,
            ref ComputeBuffer shoreHeightMap,
            ref ComputeBuffer shoreMask,
            out ComputeBuffer heightMapBuffer) {
        // BlurMasks(ref biomeMasks);

        var kernel = mathHelperShader.FindKernel("MixBuffersInplace");
        mathHelperShader.SetInt("size", resolution);
        heightMapBuffer = heightMaps[0];

        for (int i = 1; i < heightMaps.Length; i++) {
            mathHelperShader.SetInt("offset", i);
            mathHelperShader.SetBuffer(kernel, "bufferA", heightMapBuffer);
            mathHelperShader.SetBuffer(kernel, "bufferB", heightMaps[i]);
            mathHelperShader.SetBuffer(kernel, "mask", biomeMasks);
            mathHelperShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
        }

        mathHelperShader.SetInt("offset", 0);
        mathHelperShader.SetBuffer(kernel, "bufferA", heightMapBuffer);
        mathHelperShader.SetBuffer(kernel, "bufferB", shoreHeightMap);
        mathHelperShader.SetBuffer(kernel, "mask", shoreMask);
        mathHelperShader.Dispatch(
            kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

        ApplyDistanceField(ref heightMapBuffer, true);

        if (debugMat == DebugMats.Biomes) {
            ComputeBuffer bufferA = new ComputeBuffer(
                resolution * resolution, sizeof(float));
            bufferA.SetData(Enumerable.Repeat(0.0f, resolution * resolution)
                                .ToArray());
            ComputeBuffer bufferB = new ComputeBuffer(
                resolution * resolution, sizeof(float));
            for (int i = 1; i < heightMaps.Length; i++) {
                bufferB.SetData(Enumerable.Repeat(
                    (float)i, resolution * resolution).ToArray());
                mathHelperShader.SetInt("offset", i);
                mathHelperShader.SetBuffer(kernel, "bufferA", bufferA);
                mathHelperShader.SetBuffer(kernel, "bufferB", bufferB);
                mathHelperShader.SetBuffer(kernel, "mask", biomeMasks);
                mathHelperShader.Dispatch(
                    kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);
            }

            bufferB.SetData(Enumerable.Repeat(
                (float)heightMaps.Length, resolution * resolution).ToArray());

            mathHelperShader.SetInt("offset", 0);
            mathHelperShader.SetBuffer(kernel, "bufferA", bufferA);
            mathHelperShader.SetBuffer(kernel, "bufferB", bufferB);
            mathHelperShader.SetBuffer(kernel, "mask", shoreMask);
            mathHelperShader.Dispatch(
                kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

            GenDebugBiomeMat(ref bufferA, true);

            bufferA.Dispose();
            bufferB.Dispose();
        }
    }

    void GenerateBiomeHeightMap(
            out ComputeBuffer biomeHeightMap, ref BiomeConfig biome) {
        GenerateHeightMap(out biomeHeightMap, ref biome);
        if (addMask && maskAllOctaves) {
            ApplyDistanceField(ref biomeHeightMap);
        }
    }

    void GenerateShoreMask(out ComputeBuffer shoreMask) {
        ComputeBuffer distanceField = new ComputeBuffer(
            islandResolution * islandResolution, sizeof(float));
        distanceField.SetData(distanceFieldBuffer);

        shoreMask = new ComputeBuffer(
            resolution * resolution, sizeof(float));

        ComputeBuffer shapingFunction = new ComputeBuffer(
            100, sizeof(float));
        float[] shapeData;
        ConvertShapingCurveToArray(
                ref shoreMaskCurve, out shapeData);
        shapingFunction.SetData(shapeData);

        var kernel = terrainShader.FindKernel("GenerateShoreMask");
        terrainShader.SetBuffer(kernel, "distanceField", distanceField);
        terrainShader.SetBuffer(kernel, "shoreMask", shoreMask);
        terrainShader.SetBuffer(kernel, "shapingFunction", shapingFunction);
        terrainShader.Dispatch(
            kernel, resolution * resolution / THREAD_GROUP_SIZE, 1, 1);

        if (debugMat == DebugMats.Shore) {
            GenDebugMat(ref shoreMask, resolution, 1.0f);
        }

        distanceField.Dispose();
        shapingFunction.Dispose();
    }

    public void GenerateTerrain() {
        #if TIMEIT
            var start = Time.realtimeSinceStartup;
        #endif
        // reset the prng
        prng = new PRNG(seed);
        // set the position and range in the shaders
        terrainShader.SetVector("offset", new Vector4(
            offset[0], offset[1], 0, 0));
        terrainShader.SetFloat("zoom", zoom);

        ComputeBuffer biomesMasks;
        GenerateBiomes(out biomesMasks);

        // init heightmap buffer and compute the base heightmap
        ComputeBuffer[] heightMaps = new ComputeBuffer[biomes.Length];
        for (int i = 0; i < biomes.Length; i++) {
            GenerateBiomeHeightMap(out heightMaps[i], ref biomes[i]);
        }

        // do the same for the shore/beach
        ComputeBuffer shoreHeightMap;
        GenerateBiomeHeightMap(out shoreHeightMap, ref beachBiome);
        ComputeBuffer shoreMask;
        GenerateShoreMask(out shoreMask);

        // combine the heightmaps of all the biomes
        ComputeBuffer heightMapBuffer;
        CombineHeights(
            ref heightMaps,
            ref biomesMasks,
            ref shoreHeightMap,
            ref shoreMask,
            out heightMapBuffer);

        biomesMasks.Dispose();
        shoreHeightMap.Dispose();
        // for (int i = 1; i < biomes.Length; i++) {
        //     heightMaps[i].Dispose();
        // }


        // level the heightmap
        if (levelTerrain) {
            LevelHeightMap(ref heightMapBuffer);
        }

        if (!debugMatOnly) {
            // Generate the mesh from the heightmap
            ComputeBuffer vertexBuffer;
            ComputeBuffer trisBuffer;
            Vertex[] vertexData;
            GenerateMesh(ref heightMapBuffer,
                         out vertexBuffer,
                         out vertexData,
                         out trisBuffer);

            // get the data back
            vertexBuffer.GetData(vertexData);
            int[] tris = new int[
                (resolution - 2 - 1) * (resolution - 2 - 1) * 6];
            trisBuffer.GetData(tris);

            // generate the mesh
            UpdateMesh(vertexData, tris);

            // dispose of buffers
            vertexBuffer.Dispose();
            trisBuffer.Dispose();
        }
        if (debugMat == DebugMats.HeightMap) {
            GenDebugMat(ref heightMapBuffer, resolution, maxHeight);
        } else if (debugMat == DebugMats.Uvs) {
            GenUVsDebugMat();
        }


        // dispose of remaining buffers
        heightMapBuffer.Dispose();
        #if TIMEIT
            var end = Time.realtimeSinceStartup;
            Debug.Log("Terrain generation took " + (end - start) + " seconds");
        #endif
    }

    public override void UpdateTile(int tileSize, Vector3 position, int mapSize)
    {
        this.tileSize = tileSize;
        transform.position = position;
        this.offset[0] = (position.x - tileSize / 2) / (float)mapSize + 0.5f;
        this.offset[1] = (position.z - tileSize / 2) / (float)mapSize + 0.5f;
        this.zoom = mapSize / (float)tileSize;
        GenerateTerrain();
    }

    void InitDistanceField() {
        // init water mask buffer and compute the base island mask
        ComputeBuffer waterMask = new ComputeBuffer(
            islandResolution * islandResolution, sizeof(float));
        Islandify(ref waterMask);
        GenerateRiver(ref waterMask);
        if (debugMat == DebugMats.WaterMask) {
            GenDebugMat(ref waterMask, islandResolution, 1.0f);
        }


        // compute the water distance field and apply it to the base map
        ComputeBuffer distanceField = new ComputeBuffer(
            islandResolution * islandResolution, sizeof(float));
        ComputeDistanceField(ref waterMask, ref distanceField);
        waterMask.Dispose();

        distanceFieldBuffer = new float[islandResolution * islandResolution];
        distanceField.GetData(distanceFieldBuffer);
        distanceField.Dispose();
    }

    void Awake() {
        if (biomes.Length == 0) {
            Debug.LogError("No biomes set in terrain generator");
        }
        prng = new PRNG(seed);
        InitDistanceField();
        InitBiomes();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (debugGenerateOnStart) {
            GenerateTerrain();
        }
    }
}
