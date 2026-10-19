using System.Linq;
using System.Reflection;
using MelonLoader;
using SDModKit.Api;
using SDModKit.Game;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OneWayTransfers
{
    public sealed class FluidGate : SDMod
    {
        public const ushort PartId = 4711;
        private const string PartName = "OneWayFluidGate";

        public override string Id => "onewayfluidgate";
        public override string Name => "One-Way Fluid Gate";
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
            FluidGateResearch.Apply();
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            settings.fullLabel = "One-Way Fluid Gate";
            settings.description = "Lets fluid flow in one direction only, from the input to the output, never back.";
            settings.localizedDescription = null;
            settings.internalStateClass = "SignalSplitterState";
            settings.customCreationClass = null;

            RemoveNonFluidSockets(prefab);
            PartModel.Replace(prefab, "FluidValveVisual", "fluid", new[] { "signal" }, PartName);
        }

        private static void RemoveNonFluidSockets(GameObject prefab)
        {
            foreach (SocketObject socket in prefab.GetComponentsInChildren<SocketObject>(true).ToList())
            {
                if (socket.Type != CableType.Fluid)
                {
                    Object.DestroyImmediate(socket.gameObject);
                }
            }

            SocketObject[] sockets = prefab.GetComponentsInChildren<SocketObject>(true).OrderBy(s => s.Id).ToArray();
            FieldInfo idField = typeof(SocketObject).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
            if (idField == null)
            {
                MelonLogger.Error("OneWayTransfers: field SocketObject.id not found, socket ids of the fluid gate not renumbered.");
                return;
            }
            for (int i = 0; i < sockets.Length; i++)
            {
                idField.SetValue(sockets[i], (byte)i);
            }
            if (sockets.Length != 2)
            {
                MelonLogger.Warning("OneWayTransfers: expected exactly 2 fluid sockets on the fluid gate, found " + sockets.Length + ".");
            }
        }
    }
}