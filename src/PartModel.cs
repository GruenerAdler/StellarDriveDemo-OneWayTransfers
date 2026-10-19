using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using SDModKit.Content;
using SDModKit.Game;
using SDModKit.Util;
using Ships.Parts.Common;
using Ships.Parts.Common.Model.Reactions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OneWayTransfers
{
    // Change Model from cloned original
    internal static class PartModel
    {
        public static void Replace(GameObject prefab, string donorVisualType, string modelToken, string[] otherTokens, string objectName)
        {
            Component oldVisual = FindComponent(prefab, donorVisualType);
            Transform visualRoot = oldVisual != null ? oldVisual.transform : prefab.transform.Find("Visual");
            if (visualRoot == null)
            {
                MelonLogger.Error("OneWayTransfers: no visual root found on the donor prefab of " + objectName + ", model not replaced.");
                return;
            }

            if (oldVisual != null)
            {
                Object.DestroyImmediate(oldVisual);
            }
            if (visualRoot.GetComponent<IShipPartVisual>() == null)
            {
                visualRoot.gameObject.AddComponent<DefaultShipPartVisuals>();
            }

            Assembly assembly = typeof(PartModel).Assembly;
            string[] resourceNames = assembly.GetManifestResourceNames();

            string meshName = FindMeshResource(resourceNames, modelToken, otherTokens);
            Mesh mesh = meshName == null ? null : MeshData.Load(assembly, meshName);
            if (mesh == null)
            {
                MelonLogger.Error("OneWayTransfers: no usable .obj model found for " + objectName + ", keeping the donor model. "
                    + "Embedded resources in this dll: [" + string.Join(", ", resourceNames) + "]");
                return;
            }

            Material baseMaterial = ContentAssets.Material("PartMetal");
            if (baseMaterial == null)
            {
                MelonLogger.Error("OneWayTransfers: material PartMetal not found for " + objectName + ", keeping the donor model.");
                return;
            }

            string modelBase = Stem(meshName);
            string objText = EmbeddedAssets.Text(assembly, meshName);
            List<string> materialOrder = ReadMaterialOrder(objText);
            Dictionary<string, string> mtlTextures = ReadMaterialTextureFiles(assembly, resourceNames, objText, modelBase, modelToken);
            string[] pngs = FindPngResources(resourceNames, modelBase, modelToken, otherTokens);

            if (visualRoot == prefab.transform)
            {
                MelonLogger.Warning("OneWayTransfers: the visual component of " + objectName + " sits on the prefab root, donor model children are kept.");
            }
            else
            {
                foreach (Transform child in visualRoot.Cast<Transform>().ToList())
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            GameObject model = new GameObject(objectName);
            model.transform.SetParent(visualRoot, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;

            int submeshes = Mathf.Max(1, mesh.subMeshCount);
            Material[] materials = new Material[submeshes];
            for (int i = 0; i < submeshes; i++)
            {
                string materialName = i < materialOrder.Count ? materialOrder[i] : string.Empty;
                Material material = new Material(baseMaterial);
                material.name = objectName + "_" + i;
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", Color.white);
                }
                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", Color.white);
                }

                string textureResource = PickTexture(materialName, mtlTextures, pngs, modelBase, resourceNames);
                if (textureResource == null)
                {
                    MelonLogger.Warning("OneWayTransfers: " + objectName + " submesh " + i + " (material '" + materialName + "') has no texture.");
                }
                else
                {
                    Texture2D albedo = EmbeddedAssets.Texture(assembly, textureResource);
                    if (albedo == null)
                    {
                        MelonLogger.Warning("OneWayTransfers: texture " + textureResource + " could not be loaded.");
                    }
                    else if (material.HasProperty("_Albedo"))
                    {
                        material.SetTexture("_Albedo", albedo);
                    }
                    else
                    {
                        material.mainTexture = albedo;
                    }
                }
                materials[i] = material;
            }
            model.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private static Component FindComponent(GameObject root, string typeName)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                for (Type type = component == null ? null : component.GetType(); type != null; type = type.BaseType)
                {
                    if (type.Name == typeName)
                    {
                        return component;
                    }
                }
            }
            return null;
        }

        private static string PickTexture(string material, Dictionary<string, string> mtlTextures, string[] pngs,
            string modelBase, string[] resourceNames)
        {
            string file;
            if (mtlTextures.TryGetValue(material, out file))
            {
                string fromMtl = FindResource(resourceNames, file);
                if (fromMtl != null)
                {
                    return fromMtl;
                }
            }
            if (!string.IsNullOrEmpty(material))
            {
                string byMaterial = pngs.FirstOrDefault(n => Has(Stem(n), material));
                if (byMaterial != null)
                {
                    return byMaterial;
                }
            }
            string byModel = pngs.FirstOrDefault(n => Has(Stem(n), modelBase));
            return byModel ?? pngs.FirstOrDefault();
        }

        private static List<string> ReadMaterialOrder(string objText)
        {
            List<string> order = new List<string>();
            bool hasCurrent = false;
            foreach (string raw in (objText ?? string.Empty).Split('\n'))
            {
                string[] parts = raw.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    continue;
                }
                if (parts[0] == "usemtl")
                {
                    string name = parts.Length > 1 ? parts[1] : string.Empty;
                    if (!order.Contains(name))
                    {
                        order.Add(name);
                    }
                    hasCurrent = true;
                }
                else if (parts[0] == "f" && !hasCurrent)
                {
                    if (!order.Contains(string.Empty))
                    {
                        order.Add(string.Empty);
                    }
                    hasCurrent = true;
                }
            }
            return order;
        }

        private static Dictionary<string, string> ReadMaterialTextureFiles(Assembly assembly, string[] resourceNames,
            string objText, string modelBase, string modelToken)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            string mtlFile = null;
            foreach (string raw in (objText ?? string.Empty).Split('\n'))
            {
                string[] parts = raw.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && parts[0] == "mtllib")
                {
                    mtlFile = Path.GetFileName(parts[1].Replace('\\', '/'));
                    break;
                }
            }

            string resource = mtlFile != null ? FindResource(resourceNames, mtlFile) : null;
            if (resource == null)
            {
                resource = resourceNames.FirstOrDefault(n => n.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase)
                    && (Has(Stem(n), modelBase) || Has(Stem(n), modelToken)));
            }
            if (resource == null)
            {
                return result;
            }

            string mtlText = EmbeddedAssets.Text(assembly, resource);
            if (string.IsNullOrEmpty(mtlText))
            {
                return result;
            }

            string current = null;
            foreach (string raw in mtlText.Split('\n'))
            {
                string[] parts = raw.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }
                if (parts[0] == "newmtl")
                {
                    current = parts[1];
                }
                else if (current != null && parts[0] == "map_Kd")
                {
                    result[current] = Path.GetFileName(parts[parts.Length - 1].Replace('\\', '/'));
                }
            }
            return result;
        }


        private static string FindMeshResource(string[] resourceNames, string modelToken, string[] otherTokens)
        {
            string[] objs = resourceNames
                .Where(n => n.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string own = objs.FirstOrDefault(n => Has(Stem(n), modelToken));
            if (own != null)
            {
                return own;
            }
            return objs.FirstOrDefault(n => !otherTokens.Any(token => Has(Stem(n), token)));
        }


        private static string[] FindPngResources(string[] resourceNames, string modelBase, string modelToken, string[] otherTokens)
        {
            string[] all = resourceNames
                .Where(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] related = all.Where(n => Has(Stem(n), modelBase) || Has(Stem(n), modelToken)).ToArray();
            if (related.Length > 0)
            {
                return related;
            }
            return all.Where(n => Has(Stem(n), "albedo") && !otherTokens.Any(token => Has(Stem(n), token))).ToArray();
        }

        private static string FindResource(string[] resourceNames, string file)
        {
            string exact = resourceNames.FirstOrDefault(n => string.Equals(n, file, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }
            string suffix = "." + file;
            return resourceNames.FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        private static string Stem(string resourceName)
        {
            int extension = resourceName.LastIndexOf('.');
            string withoutExtension = extension > 0 ? resourceName.Substring(0, extension) : resourceName;
            int dot = withoutExtension.LastIndexOf('.');
            return dot >= 0 ? withoutExtension.Substring(dot + 1) : withoutExtension;
        }

        private static bool Has(string text, string token)
        {
            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}