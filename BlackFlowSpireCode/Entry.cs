using System.Reflection;
using BlackFlowSpire.Persistence;
using BlackFlowSpire.Audio;
using BlackFlowSpire.Ui;
using BlackFlowSpire.Integration;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace BlackFlowSpire;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string ModId = "BlackFlowSpire";
    private static bool _initialized;
    public static Logger Logger { get; private set; } = null!;

    public static void Initialize()
    {
        if (_initialized) return;
        Logger = RitsuLibFramework.CreateLogger(ModId);
        ForestLocalization.AttachLocaleManager();
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, Assembly.GetExecutingAssembly());
        ForestGenerationService.Register();
        ForestTeam.Register();
        ForestMusicDirector.RegisterAssets();
        new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());
        Logger.Info("BlackFlowSpire ready: select the mode from the Play screen.");
        _initialized = true;
    }
}
