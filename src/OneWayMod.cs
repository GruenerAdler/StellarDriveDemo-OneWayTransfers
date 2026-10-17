using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using SDModKit.Api;
using SDModKit.Content;
using SDModKit.Game;
using SDModKit.Util;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Common.Model.Reactions;
using Ships.Parts.FluidValve;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OneWayTransfers
{
    public sealed class OneWayMod : SDMod
    {
        public const ushort PartId = 4711;
        private const string PartName = "OneWayFluidGate";

        public override string Id => "onewaytransfers";
        public override string Name => "One-Way Transfers";
        public override bool HostOnly => true;

        public override void OnRegistered()
        {
            CustomParts.Register(new CustomPartDefinition
            {
                Id = PartId,
                Name = PartName,
                Donor = "FluidValve",
                BuildMenuAfter = "FluidValve",
                AutoFinish = false,
                Configure = Configure
            });
        }

        public override void OnWorldEnter()
        {
            OneWayResearch.AddToValveNode();
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            settings.fullLabel = "One-Way Fluid Gate";
            settings.description = "Lets fluid flow in one direction only, from the input to the output, never back.";
            settings.localizedDescription = null;
            settings.internalStateClass = "SignalSplitterState";
            settings.customCreationClass = null;

            RemoveNonFluidSockets(prefab);
            ReplaceVisual(prefab);
        }

        private static void RemoveNonFluidSockets(GameObject prefab)
        {
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true).ToList())
            {
                if (socket.Type == CableType.Fluid)
                {
                    continue;
                }
                Object.DestroyImmediate(socket.gameObject);
            }

            // Sockets
            SocketObject[] sockets = prefab.GetComponentsInChildren<SocketObject>(true).OrderBy(s => s.Id).ToArray();
            FieldInfo idField = typeof(SocketObject).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
            if (idField == null)
            {
                MelonLogger.Error("OneWayTransfers: field SocketObject.id not found, socket ids not renumbered.");
                return;
            }
            for (int i = 0; i < sockets.Length; i++)
            {
                idField.SetValue(sockets[i], (byte)i);
            }
            if (sockets.Length != 2)
            {
                MelonLogger.Warning("OneWayTransfers: expected exactly 2 fluid sockets, found " + sockets.Length + ".");
            }
        }

        private static void ReplaceVisual(GameObject prefab)
        {
            FluidValveVisual oldVisual = prefab.GetComponentInChildren<FluidValveVisual>(true);
            Transform visualRoot = oldVisual != null ? oldVisual.transform : prefab.transform.Find("Visual");
            if (visualRoot == null)
            {
                MelonLogger.Error("OneWayTransfers: no visual root found on the donor prefab, model not replaced.");
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

            Assembly assembly = typeof(OneWayMod).Assembly;
            string[] resourceNames = assembly.GetManifestResourceNames();

            string meshName = FindMeshResource(resourceNames);
            Mesh mesh = meshName == null ? null : MeshData.Load(assembly, meshName);
            if (mesh == null)
            {
                MelonLogger.Error("OneWayTransfers: no usable .obj model found, keeping the donor model. "
                    + "Embedded resources in this dll: [" + string.Join(", ", resourceNames) + "]");
                return;
            }

            Material baseMaterial = ContentAssets.Material("PartMetal");
            if (baseMaterial == null)
            {
                MelonLogger.Error("OneWayTransfers: material PartMetal not found, keeping the donor model.");
                return;
            }

            string objText = EmbeddedAssets.Text(assembly, meshName);
            List<string> materialOrder = ReadMaterialOrder(objText);
            Dictionary<string, string> mtlTextures = ReadMaterialTextureFiles(assembly, resourceNames, objText);
            string[] pngs = FindPngResources(resourceNames);
            string modelBase = Stem(meshName);

            if (visualRoot == prefab.transform)
            {
                MelonLogger.Warning("OneWayTransfers: the visual component sits on the prefab root, donor model children are kept.");
            }
            else
            {
                foreach (Transform child in visualRoot.Cast<Transform>().ToList())
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            GameObject model = new GameObject("OneWayGate");
            model.transform.SetParent(visualRoot, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;

            int submeshes = Mathf.Max(1, mesh.subMeshCount);
            Material[] materials = new Material[submeshes];
            for (int i = 0; i < submeshes; i++)
            {
                string materialName = i < materialOrder.Count ? materialOrder[i] : string.Empty;
                Material material = new Material(baseMaterial);
                material.name = "OneWayGate_" + i;

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", Color.white);
                }

                string textureResource = PickTexture(materialName, mtlTextures, pngs, modelBase, resourceNames, out string reason);
                if (textureResource == null)
                {
                    MelonLogger.Warning("OneWayTransfers: submesh " + i + " (material '" + materialName + "') has no texture.");
                }
                else
                {
                    Texture2D albedo = EmbeddedAssets.Texture(assembly, textureResource);
                    if (albedo == null)
                    {
                        MelonLogger.Warning("OneWayTransfers: texture " + textureResource + " could not be loaded.");
                    }
                    else
                    {
                        if (material.HasProperty("_Albedo"))
                        {
                            material.SetTexture("_Albedo", albedo);
                        }
                        else
                        {
                            material.mainTexture = albedo;
                        }
                    }
                }
                materials[i] = material;
            }
            model.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }

        // Textures and Model
        private static string PickTexture(string material, Dictionary<string, string> mtlTextures, string[] pngs,
            string modelBase, string[] resourceNames, out string reason)
        {
            string file;
            if (mtlTextures.TryGetValue(material, out file))
            {
                string fromMtl = FindResource(resourceNames, file);
                if (fromMtl != null)
                {
                    reason = "from .mtl";
                    return fromMtl;
                }
            }
            if (!string.IsNullOrEmpty(material))
            {
                string byMaterial = pngs.FirstOrDefault(n => Stem(n).IndexOf(material, StringComparison.OrdinalIgnoreCase) >= 0);
                if (byMaterial != null)
                {
                    reason = "name matches material";
                    return byMaterial;
                }
            }
            string byModel = pngs.FirstOrDefault(n => Stem(n).IndexOf(modelBase, StringComparison.OrdinalIgnoreCase) >= 0);
            if (byModel != null)
            {
                reason = "name matches model";
                return byModel;
            }
            reason = "first texture";
            return pngs.FirstOrDefault();
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

        private static Dictionary<string, string> ReadMaterialTextureFiles(Assembly assembly, string[] resourceNames, string objText)
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
                resource = resourceNames.FirstOrDefault(n => n.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase));
            }
            if (resource == null)
            {
                MelonLogger.Warning("OneWayTransfers: no .mtl embedded, textures are chosen by name.");
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


        private static string FindMeshResource(string[] resourceNames)
        {
            return resourceNames
                .Where(n => n.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.IndexOf("gate", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }


        private static string[] FindPngResources(string[] resourceNames)
        {
            string[] all = resourceNames
                .Where(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] albedo = all.Where(n => n.IndexOf("albedo", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            return albedo.Length > 0 ? albedo : all;
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
    }
}