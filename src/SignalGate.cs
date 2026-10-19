using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SDModKit.Api;
using SDModKit.Game;
using Ships.Interface.Settings;
using UnityEngine;

namespace OneWayTransfers
{
    public sealed class SignalGate : SDMod
    {
        public const ushort PartId = 4712;
        private const string PartName = "OneWaySignalGate";
        private const string ProcessorStateClass = "SignalProcessorState";

        private static CustomPartDefinition _definition;
        private static bool _donorResolved;

        public override string Id => "onewaysignalgate";
        public override string Name => "One-Way Signal Gate";
        public override bool HostOnly => true;

        public override void OnRegistered()
        {
            _definition = new CustomPartDefinition
            {
                Id = PartId,
                Name = PartName,
                Donor = "SignalProcessor",
                BuildMenuAfter = "SignalProcessor",
                AutoFinish = false,
                Configure = Configure
            };
            CustomParts.Register(_definition);
        }

        public override void OnWorldEnter()
        {
            ResolveDonor();
            SignalGateResearch.Apply();
        }

        public static void ResolveDonor()
        {
            if (_donorResolved || _definition == null)
            {
                return;
            }
            PartSettings processor = FindProcessor();
            if (processor == null)
            {
                return;
            }
            _definition.Donor = processor.name;
            _definition.BuildMenuAfter = processor.name;
            _donorResolved = true;
        }

        internal static PartSettings FindProcessor()
        {
            return Resources.FindObjectsOfTypeAll<PartSettings>().FirstOrDefault(p =>
                p != null && p.part != null && p.id != PartId && p.internalStateClass == ProcessorStateClass);
        }

        private static void Configure(PartSettings settings, GameObject prefab)
        {
            settings.fullLabel = "One-Way Signal Gate";
            settings.description = "Passes a signal through unchanged, from the input to the output, never back.";
            settings.localizedDescription = null;
            settings.internalStateClass = ProcessorStateClass;
            settings.customCreationClass = null;

            DisableModeSwitching(prefab);
            PartModel.Replace(prefab, "SignalProcessorVisuals", "signal", new[] { "fluid", "valve" }, PartName);
        }

        private static void DisableModeSwitching(GameObject prefab)
        {
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }
                Type declaring = component.GetType();
                while (declaring != null && declaring.Name != "PartInteraction")
                {
                    declaring = declaring.BaseType;
                }
                if (declaring == null)
                {
                    continue;
                }
                FieldInfo field = declaring.GetField("interactions", BindingFlags.NonPublic | BindingFlags.Instance);
                IList list = field != null ? field.GetValue(component) as IList : null;
                if (list == null)
                {
                    continue;
                }
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (IsInteractConfig(list[i]))
                    {
                        list.RemoveAt(i);
                    }
                }
            }
        }

        private static bool IsInteractConfig(object config)
        {
            if (config == null)
            {
                return false;
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = config.GetType();
            FieldInfo field = type.GetField("type", flags);
            object value = field != null ? field.GetValue(config) : null;
            if (value == null)
            {
                PropertyInfo property = type.GetProperty("type", flags);
                value = property != null ? property.GetValue(config, null) : null;
            }
            return value != null && value.ToString() == "Interact";
        }
    }
}