using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.Core;
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
        [API.APIParameter("Whenever cache or not the resource hashes")] bool cache,
        [API.APIParameter("The directory to save the images with the changed metadata")] string outputDirectory)
    {
        JObject newSettings = new JObject()
        {
            ["cache"] = cache,
            ["outputDirectory"] = string.IsNullOrEmpty(outputDirectory) ? Program.ServerSettings.Paths.OutputPath : outputDirectory
        };

        string jsonString = JsonConvert.SerializeObject(newSettings, Formatting.Indented);
        File.WriteAllText(Utils.settingsFile, jsonString);

        return new JObject()
        {
            ["success"] = true
        };
    }

    [API.APIDescription("Loads the configuration of the ImgMetadataConverter extension",
        """
        {
            "cache": bool,
            "outputDirectory": string
        }
        """)]
    public static async Task<JObject> LoadImgMetadataConverterSettings(Session session)
    {
        JObject settingsObj = JObject.Parse(File.ReadAllText(Utils.settingsFile));

        return new JObject()
        {
            ["cache"] = settingsObj["cache"],
            ["outputDirectory"] = settingsObj["outputDirectory"]
        };
    }
}

