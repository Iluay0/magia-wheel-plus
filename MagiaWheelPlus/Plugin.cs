using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
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

    private readonly Task initTask;
    private MagiaBar? bar;
    private volatile bool disposed;

    public Plugin()
    {
        initTask = InitializeAsync();
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

                bar = new MagiaBar();
                AddonLifecycle.RegisterListener(AddonEvent.PostSetup, MagiaBar.BoardName, OnBoardSetup);
                AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, MagiaBar.BoardName, OnBoardFinalize);
                if (GameGui.GetAddonByName(MagiaBar.BoardName).Address != nint.Zero)
                    OpenBar();
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "MagiaWheelPlus initialisation failed");
        }
    }

    public void Dispose()
    {
        disposed = true;

        if (Framework.IsInFrameworkUpdateThread)
        {
            Cleanup();
            return;
        }

        initTask.Wait();
        Framework.RunOnFrameworkThread(Cleanup).Wait();
    }

    private void Cleanup()
    {
        if (bar == null) return;

        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, MagiaBar.BoardName, OnBoardSetup);
        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, MagiaBar.BoardName, OnBoardFinalize);
        bar.Dispose();
        bar = null;
        KamiToolKitLibrary.Dispose();
    }

    private void OnBoardSetup(AddonEvent type, AddonArgs args) => OpenBar();

    private void OnBoardFinalize(AddonEvent type, AddonArgs args) => bar?.Close();

    private void OpenBar()
    {
        bar?.Open();
        Log.Info("MagiaWheelPlus: bar opened");
    }
}
