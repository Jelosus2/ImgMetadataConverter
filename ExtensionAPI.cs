using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.WebAPI;

namespace Jelosus1.Extensions.SwarmMetadataConverter;

[API.APIClass("API routes related to SwarmMetadataConverter extension")]
public static class ExtensionAPI
{
    public static void Register()
    {
        API.RegisterAPICall(SaveSwarmMetadataConverterSettings, true);
    }

    [API.APIDescription("Saves the configuration for the SwarmMetadataConverter extension",
        """
        {
            "success": bool
        }
        """)]
    public static async Task<JObject> SaveSwarmMetadataConverterSettings(Session session, 
        [API.APIParameter("Whenever cache or not the resource hashes")] bool cache = true,
        [API.APIParameter("The directory to save the images with the changed metadata")] string outputDirectory = "Output")
    {
        return new JObject() { };
    }
}

