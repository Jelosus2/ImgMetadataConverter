using Newtonsoft.Json.Linq;
using SwarmUI.Core;
using SwarmUI.Utils;
using System.IO;

namespace ImgMetadataConverter;

public static class Utils
{
    public static readonly string settingsFile = Path.Join(Environment.CurrentDirectory, "src", "Extensions", "ImgMetadataConverter", "settings.json");
    public static readonly JObject subfolders = new()
    {
        ["SDLoraFolder"] = "Lora",
        ["unet"] = "Unet",
        ["SDModelFolder"] = "Stable-Diffusion"
    };
    private static readonly Settings.PathsData paths = Program.ServerSettings.Paths;

    public static JObject parsedSubfolders()
    {
        JObject subfoldersObj = new();

        foreach ((string key, JToken val) in subfolders)
        {
            subfoldersObj[$"{val}"] = paths.GetFieldValueOrDefault<string>(key) != null
                ? Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, paths.ModelRoot, paths.GetFieldValueOrDefault<string>(key))
                : Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, paths.ModelRoot, key);
        }

        return subfoldersObj;
    }
}
