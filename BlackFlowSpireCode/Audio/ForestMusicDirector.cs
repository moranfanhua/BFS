using BlackFlowSpire.Integration;
using BlackFlowSpire.Map.Models;
using BlackFlowSpire.Map.Presentation;
using BlackFlowSpire.Persistence;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Audio;

namespace BlackFlowSpire.Audio;

internal partial class ForestMusicDirector(NRunMusicController owner, RunState state) : Node
{
    private const string NodeName = "BlackFlowSpireMusic";
    private const string BankPath = "res://BlackFlowSpire/Audio/BlackFlowSpireMusic.bank";
    private const string GuidPath = "res://BlackFlowSpire/Audio/BlackFlowSpireMusic.guids.txt";
    private const string EventPrefix = "event:/BlackFlowSpire/";
    private static readonly MethodInfo LoadNativeActBank = AccessTools.Method(typeof(NRunMusicController), "LoadActBank");
    private static readonly AccessTools.FieldRef<NRunMusicController, string> NativeAmbience =
        AccessTools.FieldRefAccess<NRunMusicController, string>("_currentAmbience");
    private AudioMusicHandle? _music;
    private AudioMusicHandle? _outgoing;
    private Tween? _transition;
    private AbstractRoom? _battle;
    private string? _battleTrack;
    private string? _track;
    private string? _nativeTrack;
    private bool _ready;
    private bool _disposed;
    private bool _stoppedVanilla;
    private double _refreshIn;
    private ulong _retryAfter;
    private ulong _nativeRetryAfter;
    private float _gain;

    internal static void RegisterAssets()
    {
        FmodStudioDeferredBankRegistration.RegisterBank(BankPath);
        FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings(GuidPath);
    }

    internal static void Attach(NRunMusicController owner, IRunState runState)
    {
        if (runState is RunState state && ForestGenerationService.IsEnabled(state)
            && owner.GetNodeOrNull(NodeName) == null)
            owner.AddChild(new ForestMusicDirector(owner, state) { Name = NodeName });
    }

    internal static bool TryUpdate(NRunMusicController owner) =>
        owner.GetNodeOrNull<ForestMusicDirector>(NodeName)?.RefreshMusic() == true;

    public override void _Ready()
    {
        ActiveScreenContext.Instance.Updated += OnScreenChanged;
        RefreshMusic();
    }

    public override void _Process(double delta)
    {
        _refreshIn -= delta;
        if (_refreshIn > 0) return;
        _refreshIn = 0.1;
        RefreshMusic();
    }

    private void OnScreenChanged() => RefreshMusic();

    private bool RefreshMusic()
    {
        if (_disposed || NonInteractiveMode.IsActive) return false;
        if (!ReferenceEquals(ForestRuntime.State, state) || !ForestRuntime.CanContinueRun)
        {
            Stop();
            return false;
        }
        if (Time.GetTicksMsec() < _retryAfter) return _music is { IsValid: true };
        if (!_ready)
        {
            _ready = FmodStudioServer.TryLoadBank(BankPath)
                && FmodStudioServer.TryLoadStudioGuidMappings(GuidPath);
            if (!_ready) { _retryAfter = Time.GetTicksMsec() + 5000; return false; }
        }
        if (!_stoppedVanilla)
        {
            owner.StopMusic();
            NAudioManager.Instance?.StopMusic();
            NativeAmbience(owner) = "";
            _nativeTrack = null;
            _stoppedVanilla = true;
        }
        KeepNativeActAssets();
        // Traversed nodes can briefly create native rooms while the screen is
        // faded out. Switch once the final destination has finished entering.
        if (ForestRuntime.IsTraveling) return true;
        var next = SelectTrack();
        if (_track == next && _music is { IsValid: true }) return true;
        var replacement = GameFmod.Playback.PlayMusic(AudioSource.Event(EventPrefix + next), new AudioPlaybackOptions
        {
            Volume = 0,
            Scope = AudioLifecycleScope.Run,
            AllowFadeOutOnStop = false,
            UseVanillaRouting = false,
            DebugName = "BlackFlowSpire music: " + next
        });
        if (replacement == null)
        {
            _retryAfter = Time.GetTicksMsec() + 5000;
            _stoppedVanilla = false;
            Entry.Logger.Error("[Forest] Could not start FMOD music event: " + next);
            return false;
        }
        _transition?.Kill();
        Release(ref _outgoing);
        _outgoing = _music;
        var from = _gain;
        _music = replacement;
        _gain = 0;
        _track = next;
        _transition = CreateTween();
        _transition.TweenMethod(Callable.From<float>(amount =>
        {
            _gain = amount;
            _music?.TrySetVolume(amount);
            _outgoing?.TrySetVolume(from * (1 - amount));
        }), 0f, 1f, 0.35);
        _transition.TweenCallback(Callable.From(() => Release(ref _outgoing)));
        Entry.Logger.Info("[Forest] Music: " + next);
        return true;
    }

    private void KeepNativeActAssets()
    {
        if (Time.GetTicksMsec() < _nativeRetryAfter) return;
        var selection = NRunMusicController.ResolveMusic(_nativeTrack, state.Act.BgMusicOptions,
            state.Act.MusicBankPaths, state.Rng.Seed);
        if (selection is not { } native) return;
        // Retain the native act-bank load/retry path for ambience and other
        // referenced events, while leaving its music event unstarted.
        if ((bool)LoadNativeActBank.Invoke(owner, [native.BankPath, native.Track])!)
        {
            _nativeTrack = native.Track;
            owner.UpdateAmbience();
        }
        else _nativeRetryAfter = Time.GetTicksMsec() + 5000;
    }

    private string SelectTrack()
    {
        var node = state.Map is ForestActMap map && state.CurrentMapCoord is { } position
            ? map.GetNodeData(position) : null;
        if (CombatManager.Instance.IsInProgress && state.CurrentRoom is CombatRoom combat)
        {
            if (!ReferenceEquals(_battle, combat))
            {
                _battle = combat;
                var boss = combat.RoomType == RoomType.Boss || node?.Content == ForestNodeContent.Boss;
                // A separate seeded generator keeps the same battle's choice on
                // reload without advancing any encounter, card or shop RNG.
                var choice = new Rng(state.Rng.Seed,
                    $"black_flow_spire_music/{state.CurrentActIndex}/{node?.Id ?? -1}").NextInt(0, 2);
                _battleTrack = boss ? "boss" : choice == 0 ? "battle_one" : "battle_two";
            }
            return _battleTrack!;
        }
        if (NMapScreen.Instance is not { IsOpen: true })
        {
            if (state.CurrentRoom is MerchantRoom or EventRoom
                && node?.Details?.VisualId is ForestNodeVisuals.SCRAP_SHOP or ForestNodeVisuals.EMPLOY)
                return "special_trader";
            if (state.CurrentRoom is MerchantRoom) return "trader";
        }
        return state.CurrentActIndex < 2 ? "map_low" : "map_high";
    }

    private static void Release(ref AudioMusicHandle? handle)
    {
        handle?.Dispose();
        handle = null;
    }

    private void Stop()
    {
        _transition?.Kill();
        _transition = null;
        Release(ref _outgoing);
        Release(ref _music);
        _gain = 0;
        _track = null;
    }

    public override void _ExitTree()
    {
        _disposed = true;
        ActiveScreenContext.Instance.Updated -= OnScreenChanged;
        Stop();
    }
}
