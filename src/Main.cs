using MelonLoader;
using SDModKit;
using SDModKit.Configuration;

namespace OneWayTransfers
{
    public static class BuildInfo
    {
        public const string Name = "OneWayTransfers";
        public const string Description = "Adds the One-Way Fluid Gate";
        public const string Author = "GruenerAdler";
        public const string Company = null;
        public const string Version = "1.0.0";
        public const string DownloadLink = null;
    }

    public sealed class OneWayTransfers : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ModKit.Register(new OneWayMod(), ConfigFile.For("onewaytransfers"));
            OneWayGateUpdater.Install(HarmonyInstance);
        }
    }
}