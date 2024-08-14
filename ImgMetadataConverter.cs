using SwarmUI.Core;
using SwarmUI.Utils;
using System.IO;
using Newtonsoft.Json;
using ImgMetadataConverter.WebAPI;
using Newtonsoft.Json.Linq;

namespace ImgMetadataConverter;

public class ImgMetadataConverter : Extension
{
    public override void OnInit()
    {
        Logs.Init("ImgMetadataConverter is ready!");

        ScriptFiles.Add("js/ImgMetadataConverter.js");
        Logs.Debug("[ImgMetadataConverter] Added the script files.");

        ImgMetadataConverterAPI.Register();
        Logs.Debug("[ImgMetadataConverter] Registered API callbacks.");

        Logs.Info(Utils.lol());

        string settingsFile = Utils.settingsFile;

        if (!Path.Exists(settingsFile))
        {
            JObject defaultSettings = new JObject()
            {
                ["cache"] = true,
                ["outputDirectory"] = Program.ServerSettings.Paths.OutputPath
            };

            string jsonString = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

            File.WriteAllText(settingsFile, jsonString);
            Logs.Debug("[ImgMetadataConverter] Created default config file.");
        }

    }
}
