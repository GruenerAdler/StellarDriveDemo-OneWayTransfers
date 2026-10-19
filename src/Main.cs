using MelonLoader;
using SDModKit;
using SDModKit.Configuration;

namespace OneWayTransfers
{
    public static class BuildInfo
    {
        public const string Name = "OneWayTransfers";
        public const string Description = "Adds the One-Way Fluid Gate and the One-Way Signal Gate";
        public const string Author = "GruenerAdler";
        public const string Company = null;
        public const string Version = "1.1.0";
        public const string DownloadLink = null;
    }

    public sealed class OneWayTransfers : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ModKit.Register(new FluidGate(), ConfigFile.For("onewayfluidgate"));
            FluidGateFlow.Install(HarmonyInstance);

            ModKit.Register(new SignalGate(), ConfigFile.For("onewaysignalgate"));
            SignalGatePatch.Install(HarmonyInstance);
            SignalGateBuildMenu.Install(HarmonyInstance);
        }
    }
}