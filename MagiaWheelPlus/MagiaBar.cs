using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.Timelines;

namespace MagiaWheelPlus;

public sealed unsafe class MagiaBar : NativeAddon
{
    public const string BoardName = "EurekaElementalHud";
    private const float GapFromBoard = 10f;
    private const float BoardYOffset = 44f;
    private const uint AutoIndicatorResNodeId = 38;
    private const uint AutoIndicatorImageNodeId = 39;
    private const ushort AutoAttackPartId = 15;
    private const ushort AutoDefensePartId = 16;
    private const long ToggleDebounceMs = 1000;
    private const string AutoOffCommand = "/magiaauto off";

    private sealed record ButtonSpec(string Label, string Command, string Icon)
    {
        public string Tooltip => $"{Label}  {Command}";
    }

    private static readonly ButtonSpec Attack = new("Attack", "/magiaattack", "attack");
    private static readonly ButtonSpec Defense = new("Defense", "/magiadefense", "defense");

    private enum AutoMode
    {
        Off,
        Attack,
        Defense,
    }

    private sealed record ToggleSpec(string Label, AutoMode Mode, string OnCommand, string Icon);

    private static readonly ToggleSpec AutoAttack = new("Auto Attack", AutoMode.Attack, "/magiaauto attack", "auto_attack");
    private static readonly ToggleSpec AutoDefense = new("Auto Defense", AutoMode.Defense, "/magiaauto defense", "auto_defense");

    private readonly Configuration config;
    private readonly string assetDir = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Assets");
    private readonly List<NodeBase> nodes = [];
    private CircleButtonNode sideButton = null!;
    private ImGuiIconButtonNode attackButton = null!;
    private ImGuiIconButtonNode defenseButton = null!;
    private ImGuiIconButtonNode autoAttackButton = null!;
    private ImGuiIconButtonNode autoDefenseButton = null!;
    private float appliedBoardScale;
    private bool? appliedHasTarget;
    private bool? appliedOnLeft;
    private AutoMode currentAutoMode;
    private AutoMode? appliedAutoMode;
    private AutoMode? toggleLockMode;
    private long toggleLockUntil;

    [SetsRequiredMembers]
    public MagiaBar(Configuration config)
    {
        this.config = config;
        InternalName = "MagiaWheelPlusBar";
        Title = "Magia Wheel Plus";
        Size = new Vector2(32, 158);
        OpenWindowSoundEffectId = 0;
        RememberClosePosition = false;
        DisableClose = true;
        EnableContextMenu = false;
        DisableScaleContextOption = true;
        DisableClamping = true;
        RespectCloseAll = false;
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        WindowNode?.IsVisible = false;
        appliedBoardScale = -1f;
        appliedHasTarget = null;
        appliedOnLeft = null;
        appliedAutoMode = null;
        toggleLockMode = null;

        var pill = new SimpleNineGridNode
        {
            TexturePath = "ui/uld/OperationGuide.tex",
            TextureCoordinates = Vector2.Zero,
            TextureSize = new Vector2(60, 28),
            TopOffset = 13,
            BottomOffset = 13,
            LeftOffset = 13,
            RightOffset = 13,
            Size = new Vector2(28, 154),
            Position = new Vector2(2, 2),
        };
        AddNode(pill);
        nodes.Add(pill);

        sideButton = new CircleButtonNode
        {
            Size = new Vector2(16),
            Position = new Vector2(8, 8),
        };
        sideButton.OnClick = ToggleSide;
        AddNode(sideButton);
        nodes.Add(sideButton);

        attackButton = AddIconButton(28, Attack.Tooltip, Attack.Icon, () => SendCommand(Attack.Command));
        defenseButton = AddIconButton(58, Defense.Tooltip, Defense.Icon, () => SendCommand(Defense.Command));

        var divider = new SimpleNineGridNode
        {
            TexturePath = "ui/uld/WindowA_Line.tex",
            TextureCoordinates = Vector2.Zero,
            TextureSize = new Vector2(32, 4),
            LeftOffset = 4,
            RightOffset = 4,
            Size = new Vector2(18, 4),
            Position = new Vector2(7, 86),
            Alpha = 0.6f,
        };
        AddNode(divider);
        nodes.Add(divider);

        autoAttackButton = AddIconButton(94, AutoAttack.Label, $"{AutoAttack.Icon}_dim", () => OnToggleClick(AutoAttack));
        autoDefenseButton = AddIconButton(124, AutoDefense.Label, $"{AutoDefense.Icon}_dim", () => OnToggleClick(AutoDefense));
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        var board = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(BoardName).Address;
        var boardVisible = board != null && board->IsVisible;
        if (addon->RootNode->IsVisible() != boardVisible)
            addon->RootNode->ToggleVisibility(boardVisible);
        if (!boardVisible) return;

        var boardScale = board->Scale;
        if (MathF.Abs(boardScale - appliedBoardScale) > 0.001f)
        {
            addon->SetScale(boardScale / AtkUnitBase.GetGlobalUIScale(), true);
            appliedBoardScale = boardScale;
        }

        var onLeft = config.BarOnLeft;
        var gap = GapFromBoard * boardScale;
        var x = (short)MathF.Round(onLeft
            ? board->X - addon->GetScaledWidth(true) - gap
            : board->X + board->GetScaledWidth(true) + gap);
        var y = (short)MathF.Round(board->Y + BoardYOffset * boardScale);
        if (addon->X != x || addon->Y != y)
            addon->SetPosition(x, y);

        UpdateSideButton(onLeft);
        UpdateTargetState();
        UpdateAutoMode(board);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        foreach (var node in nodes)
            node.Dispose();
        nodes.Clear();
    }

    private ImGuiIconButtonNode AddIconButton(float y, string tooltip, string icon, Action onClick)
    {
        var button = new ImGuiIconButtonNode
        {
            ShowBackground = false,
            Size = new Vector2(24),
            Position = new Vector2(4, y),
            TextTooltip = tooltip,
        };
        button.OnClick = () =>
        {
            if (button.IsEnabled)
                onClick();
        };
        RemoveIconInset(button);
        button.LoadTextureFromFile(Path.Combine(assetDir, $"{icon}.png"));
        AddNode(button);
        nodes.Add(button);
        return button;
    }

    private static void RemoveIconInset(ImGuiIconButtonNode button)
    {
        var white = new Vector3(100);
        button.ImageNode.Size = button.Size;
        button.ImageNode.Position = Vector2.Zero;
        button.ImageNode.AddTimeline(new TimelineBuilder()
            .AddFrameSetWithFrame(1, 10, 1, Vector2.Zero, 255, multiplyColor: white)
            .AddFrameSetWithFrame(11, 17, 11, Vector2.Zero, 255, multiplyColor: white)
            .AddFrameSetWithFrame(18, 26, 18, new Vector2(0, 1), 255, multiplyColor: white)
            .AddFrameSetWithFrame(27, 36, 27, Vector2.Zero, 255, multiplyColor: white)
            .AddFrameSetWithFrame(37, 46, 37, Vector2.Zero, 255, multiplyColor: white)
            .AddFrameSetWithFrame(47, 53, 47, Vector2.Zero, 255, multiplyColor: white)
            .Build());
    }

    private void ToggleSide()
    {
        config.BarOnLeft = !config.BarOnLeft;
        config.Save();
    }

    private void OnToggleClick(ToggleSpec spec)
    {
        var now = Environment.TickCount64;
        if (toggleLockMode == currentAutoMode && now < toggleLockUntil)
            return;

        SendCommand(currentAutoMode == spec.Mode ? AutoOffCommand : spec.OnCommand);
        toggleLockMode = currentAutoMode;
        toggleLockUntil = now + ToggleDebounceMs;
    }

    private void UpdateSideButton(bool onLeft)
    {
        if (appliedOnLeft == onLeft) return;
        appliedOnLeft = onLeft;

        sideButton.Icon = onLeft ? CircleButtonIcon.RightArrow : CircleButtonIcon.LeftArrow;
        sideButton.TextTooltip = "Move bar to the other side";
    }

    private void UpdateTargetState()
    {
        var hasTarget = Plugin.TargetManager.Target?.ObjectKind == ObjectKind.BattleNpc;
        if (appliedHasTarget == hasTarget) return;
        appliedHasTarget = hasTarget;

        SetEnabled(attackButton, Attack, hasTarget);
        SetEnabled(defenseButton, Defense, hasTarget);
    }

    private void SetEnabled(ImGuiIconButtonNode button, ButtonSpec spec, bool enabled)
    {
        button.IsEnabled = enabled;
        button.LoadTextureFromFile(Path.Combine(assetDir, enabled ? $"{spec.Icon}.png" : $"{spec.Icon}_disabled.png"));
        button.TextTooltip = enabled ? spec.Tooltip : $"{spec.Tooltip} (no target)";
    }

    private void UpdateAutoMode(AtkUnitBase* board)
    {
        var resNode = board->GetNodeById(AutoIndicatorResNodeId);
        var imageNode = board->GetImageNodeById(AutoIndicatorImageNodeId);
        var indicatorVisible = resNode != null && resNode->IsVisible() && imageNode != null && imageNode->IsVisible();

        var mode = indicatorVisible
            ? imageNode->PartId switch
            {
                AutoAttackPartId => AutoMode.Attack,
                AutoDefensePartId => AutoMode.Defense,
                _ => AutoMode.Off,
            }
            : AutoMode.Off;

        currentAutoMode = mode;
        if (appliedAutoMode == mode) return;
        appliedAutoMode = mode;

        UpdateToggle(autoAttackButton, AutoAttack, mode);
        UpdateToggle(autoDefenseButton, AutoDefense, mode);
    }

    private void UpdateToggle(ImGuiIconButtonNode button, ToggleSpec spec, AutoMode mode)
    {
        var active = mode == spec.Mode;
        button.LoadTextureFromFile(Path.Combine(assetDir, active ? $"{spec.Icon}.png" : $"{spec.Icon}_dim.png"));
        button.TextTooltip = active
            ? $"{spec.Label}  {AutoOffCommand}"
            : $"{spec.Label}  {spec.OnCommand}";
    }

    private static void SendCommand(string command)
    {
        var str = Utf8String.FromString(command);
        UIModule.Instance()->ProcessChatBoxEntry(str);
        str->Dtor(true);
    }
}
