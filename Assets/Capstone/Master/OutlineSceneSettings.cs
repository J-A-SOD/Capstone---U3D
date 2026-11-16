using UnityEngine;

public class OutlineSceneSettings : MonoBehaviour
{
    [System.Serializable]
    public class FloatSetting
    {
        public string propertyName;   // the property's reference name, e.g. _Thickness
        public float value;
    }

    [System.Serializable]
    public class ColorSetting
    {
        public string propertyName;   // e.g. _OutlineColor
        [ColorUsage(true, true)] public Color value;   // HDR colour picker, so values above 1 can glow with bloom
    }

    [SerializeField] private Material outlineMaterial;      // the material your outline effect uses
    [SerializeField] private FloatSetting[] floatSettings;  // numbers: thickness, strength, threshold...
    [SerializeField] private ColorSetting[] colorSettings;  // colours

    void Awake()
    {
        if (outlineMaterial == null)
        {
            Debug.LogWarning("OutlineSceneSettings: no outline material assigned.");
            return;
        }

        foreach (FloatSetting f in floatSettings)
        {
            if (outlineMaterial.HasProperty(f.propertyName))
                outlineMaterial.SetFloat(f.propertyName, f.value);
            else
                Debug.LogWarning("OutlineSceneSettings: property '" + f.propertyName + "' not found on the material.");
        }

        foreach (ColorSetting c in colorSettings)
        {
            if (outlineMaterial.HasProperty(c.propertyName))
                outlineMaterial.SetColor(c.propertyName, c.value);
            else
                Debug.LogWarning("OutlineSceneSettings: property '" + c.propertyName + "' not found on the material.");
        }
    }
}