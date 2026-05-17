using System.IO;
using UnityEditor;
using UnityEngine;

namespace JawTracking.Editor
{
    public static class JawMaterialAssetCreator
    {
        private const string UpperMaterialPath = "Assets/Materials/UpperJawMaterial.mat";
        private const string LowerMaterialPath = "Assets/Materials/LowerJawMaterial.mat";

        [InitializeOnLoadMethod]
        private static void EnsureDefaultMaterials()
        {
            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
            }

            bool changed = false;
            changed |= CreateMaterialIfMissing(
                UpperMaterialPath,
                "UpperJawMaterial",
                new Color(0.86f, 0.9f, 0.88f, 1f),
                0.12f,
                0.58f);

            changed |= CreateMaterialIfMissing(
                LowerMaterialPath,
                "LowerJawMaterial",
                new Color(0.42f, 0.82f, 0.88f, 1f),
                0.05f,
                0.46f);

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }

        private static bool CreateMaterialIfMissing(string path, string materialName, Color baseColor, float metallic, float smoothness)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return false;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = materialName
            };

            SetColorIfPresent(material, "_BaseColor", baseColor);
            SetColorIfPresent(material, "_Color", baseColor);
            SetFloatIfPresent(material, "_Metallic", metallic);
            SetFloatIfPresent(material, "_Smoothness", smoothness);
            SetFloatIfPresent(material, "_Glossiness", smoothness);

            AssetDatabase.CreateAsset(material, path);
            return true;
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
