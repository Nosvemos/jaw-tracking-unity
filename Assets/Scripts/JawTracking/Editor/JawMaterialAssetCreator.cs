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
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = false;

            Shader customShader = Shader.Find("Custom/URPVertexColorLit");
            if (customShader == null)
            {
                customShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            if (customShader == null)
            {
                customShader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(customShader)
                {
                    name = materialName
                };
                isNew = true;
            }
            else if (material.shader != customShader && customShader != null)
            {
                material.shader = customShader;
                isNew = true;
            }

            if (isNew)
            {
                SetColorIfPresent(material, "_BaseColor", baseColor);
                SetColorIfPresent(material, "_Color", baseColor);
                SetFloatIfPresent(material, "_Metallic", metallic);
                SetFloatIfPresent(material, "_Smoothness", smoothness);
                SetFloatIfPresent(material, "_Glossiness", smoothness);

                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                {
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    EditorUtility.SetDirty(material);
                }
            }

            return isNew;
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
