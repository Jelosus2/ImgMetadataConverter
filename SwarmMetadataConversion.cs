using SwarmUI.Core;
using SwarmUI.Utils;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;
using Jelosus1.Extensions.SwarmMetadataConverter;

namespace Jelosus1.Extensions.SwarmMetadataConversion;

public class SwarmMetadataConversion : Extension
{

    public override void OnInit()
    {
        Logs.Init("SwarmMetadataConverter is ready!");

        ScriptFiles.Add("js/SwarmMetadataConverter.js");
        ExtensionAPI.Register();

        Logs.Info(Path.Join(FilePath.Replace("/", ""), @".."));

        string settingsFile = Path.Join(FilePath, "settings.json");

        if (!Path.Exists(settingsFile))
        {
            Dictionary<string, string> defaultSettings = new()
            {
                ["cache"] = "active",
                ["saveDirectory"] = "Output"
            };

            string jsonString = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

            File.WriteAllText(settingsFile, jsonString);
        }

    }
}
