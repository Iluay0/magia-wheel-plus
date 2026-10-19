using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MagiaWheelPlus;

public sealed unsafe class AutoNext : IDisposable
{
    private const string ResultsAddonName = "ItemInspectionResult";
    private const string ListAddonName = "ItemInspectionList";
    private const uint NextButtonNodeId = 74;
    private const long RearmMs = 1500;

    public static bool Operation;

    private bool armed;
    private long lastClickAt;

    public AutoNext()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, ResultsAddonName, OnArm);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, ResultsAddonName, OnArm);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, ResultsAddonName, OnUpdate);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, ResultsAddonName, OnArm);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, ResultsAddonName, OnArm);
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, ResultsAddonName, OnUpdate);
        Operation = false;
    }

    public void Tick()
    {
        if (Operation && Plugin.GameGui.GetAddonByName(ListAddonName).Address == nint.Zero)
            Operation = false;
    }

    public static AtkUnitBase* GetVisibleList()
    {
        var list = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(ListAddonName).Address;
        return list != null && list->IsVisible ? list : null;
    }

    private void OnArm(AddonEvent type, AddonArgs args) => armed = true;

    private void OnUpdate(AddonEvent type, AddonArgs args)
    {
        if (!Operation) return;

        var now = Environment.TickCount64;
        if (!armed && now - lastClickAt >= RearmMs)
            armed = true;
        if (!armed) return;

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || !addon->IsVisible) return;

        var button = addon->GetComponentButtonById(NextButtonNodeId);
        if (button == null || button->OwnerNode == null) return;
        if (!button->OwnerNode->AtkResNode.IsVisible() || !button->IsEnabled) return;

        if (Click(addon, button))
        {
            armed = false;
            lastClickAt = now;
        }
    }

    private static bool Click(AtkUnitBase* addon, AtkComponentButton* button)
    {
        var node = &button->OwnerNode->AtkResNode;
        for (var evt = node->AtkEventManager.Event; evt != null; evt = evt->NextEvent)
        {
            if (evt->State.EventType != AtkEventType.ButtonClick) continue;

            var data = new AtkEventData();
            addon->ReceiveEvent(AtkEventType.ButtonClick, (int)evt->Param, evt, &data);
            return true;
        }

        return false;
    }
}
