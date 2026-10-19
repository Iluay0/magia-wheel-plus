using Dalamud.Configuration;

namespace MagiaWheelPlus;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; }

    public bool BarOnLeft { get; set; }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
