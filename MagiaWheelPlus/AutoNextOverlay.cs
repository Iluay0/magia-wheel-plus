using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MagiaWheelPlus;

public sealed unsafe class AutoNextOverlay : Window
{
    private float height;

    public AutoNextOverlay()
        : base("Magia Wheel Plus auto next overlay",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings, true)
    {
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public void Tick()
    {
        var list = AutoNext.GetVisibleList();
        if (list != null && (list->X != 0 || list->Y != 0))
            Position = new Vector2(list->X, list->Y - height);
    }

    public override bool DrawConditions() => AutoNext.GetVisibleList() != null;

    public override void Draw()
    {
        ImGui.Checkbox("Auto next", ref AutoNext.Operation);
        height = ImGui.GetWindowSize().Y;
    }
}
