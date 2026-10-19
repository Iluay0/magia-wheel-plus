using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;

namespace MagiaWheelPlus;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("MagiaWheelPlus");
    private readonly AutoNextOverlay autoNextOverlay = new();
    private readonly AutoNext autoNext;
    private readonly Task initTask;
    private MagiaBar? bar;
    private volatile bool disposed;

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        autoNext = new AutoNext();
        windowSystem.AddWindow(autoNextOverlay);
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        Framework.Update += OnFrameworkUpdate;

        initTask = InitializeAsync();
    }

    public void Dispose()
    {
        disposed = true;

        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        windowSystem.RemoveAllWindows();

        if (Framework.IsInFrameworkUpdateThread)
        {
            Cleanup();
            return;
        }

        initTask.Wait();
        Framework.RunOnFrameworkThread(Cleanup).Wait();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await KamiToolKitLibrary.InitializeAsync(PluginInterface);
            await Framework.RunOnFrameworkThread(() =>
            {
                if (disposed)
                {
                    KamiToolKitLibrary.Dispose();
                    return;
                }

                bar = new MagiaBar(config);
                AddonLifecycle.RegisterListener(AddonEvent.PostSetup, MagiaBar.BoardName, OnBoardSetup);
                AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, MagiaBar.BoardName, OnBoardFinalize);
                if (GameGui.GetAddonByName(MagiaBar.BoardName).Address != nint.Zero)
                    bar.Open();
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "MagiaWheelPlus initialisation failed");
        }
    }

    private void Cleanup()
    {
        autoNext.Dispose();

        if (bar == null) return;

        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, MagiaBar.BoardName, OnBoardSetup);
        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, MagiaBar.BoardName, OnBoardFinalize);
        bar.Dispose();
        bar = null;
        KamiToolKitLibrary.Dispose();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        autoNext.Tick();
        autoNextOverlay.Tick();
    }

    private void OnBoardSetup(AddonEvent type, AddonArgs args) => bar?.Open();

    private void OnBoardFinalize(AddonEvent type, AddonArgs args) => bar?.Close();
}
