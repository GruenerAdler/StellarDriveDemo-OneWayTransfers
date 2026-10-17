using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Research.Interface.Values;
using SDModKit.Game;
using Ships.Interface.Settings;
using UnityEngine;

namespace OneWayTransfers
{
    public static class OneWayResearch
    {
        private const string DonorAssetName = "FluidValve";

        // Put where FluidValve is
        public static void AddToValveNode()
        {
            CustomParts.EnsureAll();
            PartSettings gate = CustomParts.Get(OneWayMod.PartId);
            if (gate == null)
            {
                MelonLogger.Warning("OneWayTransfers: gate part is not built, research left unchanged.");
                return;
            }

            PartSettings valve = Resources.FindObjectsOfTypeAll<PartSettings>()
                .FirstOrDefault(p => p != null && p.name == DonorAssetName && p.id != OneWayMod.PartId);
            if (valve == null)
            {
                MelonLogger.Warning("OneWayTransfers: part " + DonorAssetName + " not found, research left unchanged.");
                return;
            }

            ResearchSettingsList researchList = Resources.FindObjectsOfTypeAll<ResearchSettingsList>().FirstOrDefault();
            if (researchList == null || researchList.list == null)
            {
                MelonLogger.Warning("OneWayTransfers: research list not found, research left unchanged.");
                return;
            }

            ResearchSettings node = researchList.list.FirstOrDefault(r =>
                r != null && r.unlockedParts != null && r.unlockedParts.Any(p => p == valve));
            if (node == null)
            {
                MelonLogger.Warning("OneWayTransfers: no tech unlocks " + DonorAssetName + ", the gate has no research entry.");
                return;
            }

            // Old Parts
            List<PartSettings> parts = node.unlockedParts
                .Where(p => p != null && (p.id != OneWayMod.PartId || p == gate))
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