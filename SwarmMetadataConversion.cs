using SwarmUI.Core;
using SwarmUI.Utils;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;

namespace Jelosus1.Extensions.SwarmMetadataConversion;

public class SwarmMetadataConversion : Extension
{

    public override void OnInit()
    {
        Logs.Init("SwarmMetadataConverter is ready!");

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

        JObject settingsObj = JObject.Parse(File.ReadAllText(settingsFile));

        Logs.Info(settingsObj["cache"].ToString());
        Logs.Info(settingsObj["saveDirectory"].ToString());
    }
}
