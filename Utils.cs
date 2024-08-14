using SwarmUI.Core;
using SwarmUI.Utils;
using System.IO;

namespace ImgMetadataConverter;

public static class Utils
{
    public static readonly string settingsFile = Path.Join(Environment.CurrentDirectory, "src", "Extensions", "ImgMetadataConverter", "settings.json");
}
