using MelonLoader;
using SDModKit.Game;
using Ships.Interface.Settings;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace OneWayTransfers
{
    public static class SignalGateBuildMenu
    {
        private const int RowCapacity = 8;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Install(HarmonyLib.Harmony harmony)
        {
            MethodInfo target = HarmonyLib.AccessTools.Method(typeof(CustomParts), "BeforeBuildMenuStart");
            if (target == null)
            {
                MelonLogger.Warning("OneWayTransfers: CustomParts.BeforeBuildMenuStart not found, the signal gate keeps the default build menu position.");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyLib.HarmonyMethod(typeof(SignalGateBuildMenu), nameof(Prefix)));
        }

        public static void Prefix(object __0)
        {
            SignalGate.ResolveDonor();
            try
            {
                Place(__0);
            }
            catch (Exception exception)
            {
                MelonLogger.Error("OneWayTransfers: placing the signal gate in the build menu failed: " + exception.Message);
            }
        }

        private static PartSettings FindLever()
        {
            return Resources
                .FindObjectsOfTypeAll<PartSettings>()
                .FirstOrDefault(p =>
                    p != null &&
                    p.name.IndexOf(
                        "Lever",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void Place(object menu)
        {
            PartSettings gate = CustomParts.Get(SignalGate.PartId);
            PartSettings lever = FindLever();

            if (menu == null || gate == null || lever == null)
            {
                return;
            }

            FieldInfo tabsField = menu.GetType().GetField("tabsConfig", Flags);
            Array tabs = tabsField != null ? tabsField.GetValue(menu) as Array : null;
            if (tabs == null)
            {
                return;
            }

            foreach (object tab in tabs)
            {
                foreach (PartSettings[] parts in PartsOfRows(tab))
                {
                    if (IndexOf(parts, gate) >= 0)
                    {
                        return;
                    }
                }
            }

            foreach (object tab in tabs)
            {
                Array rows = tab != null ? GetMember(tab, "rows") as Array : null;
                if (rows == null)
                {
                    continue;
                }
                for (int r = 0; r < rows.Length; r++)
                {
                    object row = rows.GetValue(r);
                    PartSettings[] parts = row != null ? GetMember(row, "parts") as PartSettings[] : null;
                    int index = IndexOf(parts, lever);
                    if (index < 0)
                    {
                        continue;
                    }

                    if (parts.Length < RowCapacity)
                    {
                        PartSettings[] extended = new PartSettings[parts.Length + 1];
                        Array.Copy(parts, 0, extended, 0, index + 1);
                        extended[index + 1] = gate;
                        Array.Copy(parts, index + 1, extended, index + 2, parts.Length - index - 1);
                        SetMember(row, "parts", extended);
                    }
                    else
                    {
                        Type rowType = rows.GetType().GetElementType();
                        object newRow = Activator.CreateInstance(rowType);
                        SetMember(newRow, "parts", new[] { gate });
                        Array newRows = Array.CreateInstance(rowType, rows.Length + 1);
                        Array.Copy(rows, 0, newRows, 0, r + 1);
                        newRows.SetValue(newRow, r + 1);
                        Array.Copy(rows, r + 1, newRows, r + 2, rows.Length - r - 1);
                        SetMember(tab, "rows", newRows);
                    }
                    return;
                }
            }
        }

        private static IEnumerable<PartSettings[]> PartsOfRows(object tab)
        {
            Array rows = tab != null ? GetMember(tab, "rows") as Array : null;
            if (rows == null)
            {
                yield break;
            }
            foreach (object row in rows)
            {
                PartSettings[] parts = row != null ? GetMember(row, "parts") as PartSettings[] : null;
                if (parts != null)
                {
                    yield return parts;
                }
            }
        }

        private static int IndexOf(PartSettings[] parts, PartSettings part)
        {
            if (parts == null)
            {
                return -1;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == part)
                {
                    return i;
                }
            }
            return -1;
        }

        private static object GetMember(object target, string name)
        {
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, Flags);
            if (field != null)
            {
                return field.GetValue(target);
            }
            PropertyInfo property = type.GetProperty(name, Flags);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static void SetMember(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, Flags);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            PropertyInfo property = type.GetProperty(name, Flags);
            if (property != null)
            {
                property.SetValue(target, value, null);
            }
        }
    }
}