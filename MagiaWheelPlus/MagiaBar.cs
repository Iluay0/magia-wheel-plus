using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.Timelines;

namespace MagiaWheelPlus;

public sealed unsafe class MagiaBar : NativeAddon
{
    public const string BoardName = "EurekaElementalHud";
    private const float GapFromBoard = 10f;
    private const uint AutoIndicatorResNodeId = 38;
    private const uint AutoIndicatorImageNodeId = 39;
    private const ushort AutoAttackPartId = 15;
    private const ushort AutoDefensePartId = 16;

    private sealed record ButtonSpec(string Label, string Command, string Icon)
    {
        public string Tooltip => $"{Label}  {Command}";
    }

    private static readonly ButtonSpec Attack = new("Attack", "/magiaattack", "attack");
    private static readonly ButtonSpec Defense = new("Defense", "/magiadefense", "defense");
    private static readonly ButtonSpec Auto = new("Auto", "/magiaauto", "auto_off");

    private enum AutoMode
    {
        Off,
        Attack,
        Defense,
    }

    private readonly string assetDir = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName!, "Assets");
    private readonly List<NodeBase> nodes = [];
    private ImGuiIconButtonNode attackButton = null!;
    private ImGuiIconButtonNode defenseButton = null!;
    private ImGuiIconButtonNode autoButton = null!;
    private float appliedBoardScale;
    private bool? appliedHasTarget;
    private AutoMode? appliedAutoMode;

    [SetsRequiredMembers]
    public MagiaBar()
    {
        InternalName = "MagiaWheelPlusBar";
        Title = "Magia Wheel Plus";
        Size = new Vector2(32, 104);
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
        appliedAutoMode = null;

        var pill = new SimpleNineGridNode
        {
            TexturePath = "ui/uld/OperationGuide.tex",
            TextureCoordinates = new Vector2(0, 0),
            TextureSize = new Vector2(60, 28),
            TopOffset = 13,
            BottomOffset = 13,
            LeftOffset = 13,
            RightOffset = 13,
            Size = new Vector2(28, 100),
            Position = new Vector2(2, 2),
        };
        AddNode(pill);
        nodes.Add(pill);

        attackButton = AddButton(Attack, 10);
        defenseButton = AddButton(Defense, 40);
        autoButton = AddButton(Auto, 70);
    }

    private ImGuiIconButtonNode AddButton(ButtonSpec spec, float y)
    {
        var button = new ImGuiIconButtonNode
        {
            ShowBackground = false,
            Size = new Vector2(24),
            Position = new Vector2(4, y),
            TextTooltip = spec.Tooltip,
        };
        button.OnClick = () =>
        {
            if (button.IsEnabled)
                SendCommand(spec.Command);
        };
        RemoveIconInset(button);
        button.LoadTextureFromFile(Path.Combine(assetDir, $"{spec.Icon}.png"));
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

        var x = (short)MathF.Round(board->X + board->GetScaledWidth(true) + GapFromBoard * boardScale);
        var y = (short)MathF.Round(board->Y + 44 * boardScale);
        if (addon->X != x || addon->Y != y)
            addon->SetPosition(x, y);

        UpdateTargetState();
        UpdateAutoMode(board);
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
        var resVisible = resNode != null && resNode->IsVisible();
        var imageVisible = imageNode != null && imageNode->IsVisible();
        var partId = imageNode != null ? imageNode->PartId : (ushort)0;

        var mode = resVisible && imageVisible
            ? partId switch
            {
                AutoAttackPartId => AutoMode.Attack,
                AutoDefensePartId => AutoMode.Defense,
                _ => AutoMode.Off,
            }
            : AutoMode.Off;

        if (appliedAutoMode == mode) return;
        appliedAutoMode = mode;

        var name = mode.ToString().ToLowerInvariant();
        autoButton.LoadTextureFromFile(Path.Combine(assetDir, $"auto_{name}.png"));
        autoButton.TextTooltip = $"{Auto.Tooltip}  ({name})";

        var alpha = imageNode != null ? imageNode->Color.A : (byte)0;
        Plugin.Log.Info($"MagiaWheelPlus: auto mode {mode} (node38 {(resNode == null ? "missing" : resVisible ? "visible" : "hidden")}, node39 {(imageNode == null ? "missing" : imageVisible ? "visible" : "hidden")}, node39 PartId {partId}, node39 alpha {alpha})");
    }

    private static void SendCommand(string command)
    {
        var str = Utf8String.FromString(command);
        UIModule.Instance()->ProcessChatBoxEntry(str);
        str->Dtor(true);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        foreach (var node in nodes)
            node.Dispose();
        nodes.Clear();
    }
}
