using UnityEngine;

[CreateAssetMenu(fileName = "BiomeConfig", menuName = "Configs/BiomeConfig", order = 1)]
public class BiomeConfig : ScriptableObject
{
    [Header("Noise Settings")]
    // Noise scale. The higher the value, the faster the noise will change
    public float scale = 5.0f;
    // number of noise octaves
    public  int octaves = 8;
    // lacunarity of the noise, that is how fast the frequency
    // increases with octaves
    public  float lacunarity = 2.0f;
    // how fast the amplitude decreases with octaves.
    public  float persistence = 0.5f;
    // Shaping function for the noise
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 0, 1, 1);
}
