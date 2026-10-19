using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Research.Interface.Values;
using SDModKit.Game;
using Ships.Interface.Settings;
using UnityEngine;

namespace OneWayTransfers
{
    public static class SignalGateResearch
    {
        public static void Apply()
        {
            CustomParts.EnsureAll();
            PartSettings gate = CustomParts.Get(SignalGate.PartId);
            if (gate == null)
            {
                MelonLogger.Warning("OneWayTransfers: signal gate part is not built (signal processor not found?), research left unchanged.");
                return;
            }

            PartSettings processor = SignalGate.FindProcessor();
            if (processor == null)
            {
                MelonLogger.Warning("OneWayTransfers: signal processor not found, research of the signal gate left unchanged.");
                return;
            }

            ResearchSettingsList researchList = Resources.FindObjectsOfTypeAll<ResearchSettingsList>().FirstOrDefault();
            if (researchList == null || researchList.list == null)
            {
                MelonLogger.Warning("OneWayTransfers: research list not found, research of the signal gate left unchanged.");
                return;
            }

            ResearchSettings node = researchList.list.FirstOrDefault(r =>
                r != null && r.unlockedParts != null && r.unlockedParts.Any(p => p == processor));
            if (node == null)
            {
                MelonLogger.Warning("OneWayTransfers: no tech unlocks the signal processor, the signal gate has no research entry.");
                return;
            }

            List<PartSettings> parts = node.unlockedParts
                .Where(p => p != null && (p.id != SignalGate.PartId || p == gate))
                .ToList();
            if (parts.Any(p => p == gate))
            {
                return;
            }

            parts.Add(gate);
            node.unlockedParts = parts.ToArray();
        }
    }
}