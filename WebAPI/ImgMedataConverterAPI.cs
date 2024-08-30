using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.Utils;
using SwarmUI.WebAPI;
using System.IO;

namespace ImgMetadataConverter.WebAPI;

[API.APIClass("API routes related to ImgMetadataConverter extension")]
public static class ImgMetadataConverterAPI
{
    public static void Register()
    {
        API.RegisterAPICall(SaveImgMetadataConverterSettings, true);
        API.RegisterAPICall(LoadImgMetadataConverterSettings);
    }

    [API.APIDescription("Saves the configuration for the ImgMetadataConverter extension",
        """
        {
            "success": bool
        }
        """)]
    public static async Task<JObject> SaveImgMetadataConverterSettings(
        Session session,
        [API.APIParameter("A JObject containing the settings of the ImgMetadataConverter extension")] JObject parameters)
    {
        try
        {
            List<string> configParameters = ["active", "cache", "outputDirectory", "skipDuplicates", "appendOutPathBuild"];
            JObject oldSettings = JObject.Parse(File.ReadAllText(Utils.settingsFile));
            JObject newSettings = [];

            JObject settings = (JObject)parameters["settings"];

            foreach (string parameter in configParameters)
            {
                settings.TryGetValue(parameter, out JToken val);
                if (val != null)
                {
                    if (parameter == "outputDirectory")
                    {
                        newSettings.Add(parameter, string.IsNullOrEmpty(val.ToString()) ? "[SwarmUI.OutputPath]" : val);
                    }
                    else
                    {
                        newSettings.Add(parameter, val);
                    }
                }
                else
                {
                    newSettings.Add(parameter, oldSettings[parameter]);
                }
            }

            string jsonString = JsonConvert.SerializeObject(newSettings, Formatting.Indented);
            File.WriteAllText(Utils.settingsFile, jsonString);

            return new JObject()
            {
                ["success"] = true
            };
        }
        catch (Exception e) 
        {
            Logs.Debug($"{e}");
            return new JObject()
            {
                ["error"] = "Error saving the settings. Check the logs for more information"
            };    
        }
    }

    [API.APIDescription("Loads the configuration of the ImgMetadataConverter extension",
        """
        {
            "success": bool,
            "active": bool,
            "cache": bool,
            "outputDirectory": string,
            "skipDuplicates": bool,
            "appendOutPathBuild": bool
        }
        """)]
    public static async Task<JObject> LoadImgMetadataConverterSettings(Session session)
    {
        try
        {
            JObject settingsObj = JObject.Parse(File.ReadAllText(Utils.settingsFile));

            return new JObject()
            {
                ["success"] = true,
                ["active"] = settingsObj["active"],
                ["cache"] = settingsObj["cache"],
                ["outputDirectory"] = settingsObj["outputDirectory"],
                ["skipDuplicates"] = settingsObj["skipDuplicates"],
                ["appendOutPathBuild"] = settingsObj["appendOutPathBuild"]
            };
        }
        catch (Exception e)
        {

            Logs.Debug($"{e}");
            return new JObject()
            {
                ["error"] = "Error loading the settings, loading defaults instead. Check the logs for more information"
            };
        }
    }
}

