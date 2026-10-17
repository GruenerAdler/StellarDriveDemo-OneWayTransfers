using System.Reflection;
using MelonLoader;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.FluidValve;
using UnityEngine;

namespace OneWayTransfers
{
    public static class OneWayGateUpdater
    {
        public const byte InputSocketId = 1;
        public const byte OutputSocketId = 0;


        private const float EquilibriumDelta = 0.0001f;
        private const float PressureFlowFactor = 200f;

        private static readonly FieldInfo ShipField = HarmonyLib.AccessTools.Field(typeof(FluidValvesUpdater), "_ship");
        private static readonly FieldInfo ConnectionsField = HarmonyLib.AccessTools.Field(typeof(FluidValvesUpdater), "_connections");

        public static void Install(HarmonyLib.Harmony harmony)
        {
            MethodInfo target = HarmonyLib.AccessTools.Method(typeof(FluidValvesUpdater), "Update");
            if (target == null || ShipField == null || ConnectionsField == null)
            {
                MelonLogger.Error("OneWayTransfers: FluidValvesUpdater.Update or its fields not found, one-way logic inactive.");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyLib.HarmonyMethod(typeof(OneWayGateUpdater), nameof(Postfix)));
        }

        public static void Postfix(FluidValvesUpdater __instance, float deltaTime)
        {
            IShipStateWrite ship = (IShipStateWrite)ShipField.GetValue(__instance);
            ConnectionsServerTracker connections = (ConnectionsServerTracker)ConnectionsField.GetValue(__instance);
            if (ship == null || connections == null)
            {
                return;
            }

            foreach (StatefulPart part in ship.GetAllStatefulParts<SignalSplitterState>())
            {
                if (part.Settings == null || part.Settings.id != OneWayMod.PartId)
                {
                    continue;
                }
                Transfer(ship, connections, part, deltaTime);
            }
        }

        private static void Transfer(IShipStateWrite ship, ConnectionsServerTracker connections, StatefulPart part, float deltaTime)
        {
            if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, InputSocketId), out FluidGroupServerState source)
                || !connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, OutputSocketId), out FluidGroupServerState target))
            {
                return;
            }
            if (source == target)
            {
                return;
            }
            if (source.FluidItemId != 0 && target.FluidItemId != 0 && source.FluidItemId != target.FluidItemId)
            {
                return;
            }

            // Pressure Math
            float pressureDifference = source.CurrentPressure - target.CurrentPressure;
            if (pressureDifference <= EquilibriumDelta)
            {
                return;
            }

            float toEqualize = ((target.Available + source.Available) / (target.FluidCapacity + source.FluidCapacity) - target.CurrentPressure) * target.FluidCapacity;
            float amount = Mathf.Min(pressureDifference * PressureFlowFactor * deltaTime, toEqualize);
            if (amount <= 0f)
            {
                return;
            }

            target.AddFluid(source.FluidItemId, amount);
            source.RemoveFluid(amount);
        }
    }
}