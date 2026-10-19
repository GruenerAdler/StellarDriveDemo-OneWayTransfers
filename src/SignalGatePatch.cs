using System.Reflection;
using MelonLoader;
using SDModKit.Game;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Services;

namespace OneWayTransfers
{
    public static class SignalGatePatch
    {
        public const byte InputSocketId = 0;
        public const byte OutputSocketId = 1;

        private static readonly FieldInfo ShipsField = HarmonyLib.AccessTools.Field(typeof(SignalConnectionGroupValues), "_ships");

        public static void Install(HarmonyLib.Harmony harmony)
        {
            MethodInfo emittedSignal = HarmonyLib.AccessTools.Method(typeof(SignalConnectionGroupValues), "GetSocketSignal");
            if (emittedSignal == null || ShipsField == null)
            {
                MelonLogger.Error("OneWayTransfers: SignalConnectionGroupValues.GetSocketSignal or its fields not found, signal gate logic inactive.");
            }
            else
            {
                harmony.Patch(emittedSignal, postfix: new HarmonyLib.HarmonyMethod(typeof(SignalGatePatch), nameof(Postfix)));
            }

            MethodInfo ensureDict = HarmonyLib.AccessTools.Method(typeof(CustomParts), "BeforeEnsureDict");
            if (ensureDict == null)
            {
                MelonLogger.Warning("OneWayTransfers: CustomParts.BeforeEnsureDict not found, the signal processor donor may be detected late.");
            }
            else
            {
                harmony.Patch(ensureDict, prefix: new HarmonyLib.HarmonyMethod(typeof(SignalGatePatch), nameof(BeforeBuild)));
            }
        }

        public static void BeforeBuild()
        {
            SignalGate.ResolveDonor();
        }

        public static void Postfix(SignalConnectionGroupValues __instance, EntityPartSocket __0, ref double __result)
        {
            if (__0.Socket.SocketId != OutputSocketId)
            {
                return;
            }
            IShipsProvider ships = ShipsField.GetValue(__instance) as IShipsProvider;
            if (ships == null)
            {
                return;
            }
            if (!ships.TryGetShip(__0.EntityId, out var ship) || !ship.TryGetStatefulPart(__0.Socket.PartId, out var part))
            {
                return;
            }
            if (!(part.State is SignalProcessorState) || part.Settings == null || part.Settings.id != SignalGate.PartId)
            {
                return;
            }
            __result = __instance.GetSocketSignalValue(new EntityPartSocket(__0.EntityId, __0.Socket.PartId, InputSocketId));
        }
    }
}