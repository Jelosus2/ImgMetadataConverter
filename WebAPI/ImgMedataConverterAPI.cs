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
            "success": bool,
            "error": string 
        }
        """)]
    public static async Task<JObject> SaveImgMetadataConverterSettings(
        Session session,
        [API.APIParameter("Whenever activate the extension or not")] bool active,
        [API.APIParameter("Whenever cache or not the resource hashes")] bool cache,
        [API.APIParameter("The directory to save the images with the changed metadata")] string outputDirectory)
    {
        JObject newSettings = new JObject()
        {
            ["active"] = active,
            ["cache"] = cache,
            ["outputDirectory"] = string.IsNullOrEmpty(outputDirectory) ? "[SwarmUI.OutputPath]" : Utils.PathCleanUp(outputDirectory)
        };

        try
        {
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
                ["success"] = false,
                ["error"] = "Error saving the settings. Check the logs for more information"
            };    
        }
    }

    [API.APIDescription("Loads the configuration of the ImgMetadataConverter extension",
        """
        {
            "success": bool,
            "cache": bool,
            "outputDirectory": string,
            "error": string
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
                ["outputDirectory"] = settingsObj["outputDirectory"]
            };
        }
        catch (Exception e)
        {

            Logs.Debug($"{e}");
            return new JObject()
            {
                ["success"] = false,
                ["error"] = "Error loading the settings, loading defaults instead. Check the logs for more information"
            };
        }
    }
}

