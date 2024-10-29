using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using SwarmUI.WebAPI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;

namespace ImgMetadataConverter.WebAPI;

[API.APIClass("API routes related to ImgMetadataConverter extension")]
public static class ImgMetadataConverterAPI
{
    public static PermInfoGroup ImgMetadataConverterPermGroup = new("ImgMetadataConverter", "Permissions related to the extension");
    public static PermInfo defaultPermission = Permissions.Register(new("img_metadata_converter_default", "ImgMetadataConverter Default", "The default permissions for the extension", PermissionDefault.USER, ImgMetadataConverterPermGroup));

    public static void Register()
    {
        API.RegisterAPICall(SaveImgMetadataConverterSettings, true, defaultPermission);
        API.RegisterAPICall(LoadImgMetadataConverterSettings, false, defaultPermission);
        API.RegisterAPICall(LoadModelsWithImgMetadataConverterHashes, false, defaultPermission);
        API.RegisterAPICall(DeleteImgMetadataConverterHashes, true, defaultPermission);
        API.RegisterAPICall(CalculateImgMetadataConverterHashWS, true, defaultPermission);
        API.RegisterAPICall(SearchModelOnCivitaiByHash, false, defaultPermission);
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

    [API.APIDescription("Loads the stored hashes of the extension ImgMetadataConverter if they are cached",
        """
        {
            "success": bool,
            "modelsWithHashes": JObject
        }
        """)]
    public static async Task<JObject> LoadModelsWithImgMetadataConverterHashes(Session session)
    {
        try
        {
            bool cachedHashes = Path.Exists(Utils.cacheFile);
            JObject storedHashes = cachedHashes ? JObject.Parse(File.ReadAllText(Utils.cacheFile)) : [];

            JArray models = [];
            foreach (T2IModelHandler handler in Program.T2IModelSets.Values)
            {
                foreach (string modelName in handler.ListModelNamesFor(session).Order().ToArray())
                {
                    if (modelName != "(None)")
                    {
                        models.Add(new JObject()
                        {
                            ["modelName"] = modelName,
                            ["modelHash"] = storedHashes[modelName.BeforeLast('.')] ?? ""
                        });
                    }
                }
            }

            return new JObject()
            {
                ["success"] = true,
                ["modelsWithHashes"] = models
            };
        }
        catch (Exception e)
        {
            Logs.Debug($"{e}");
            return new JObject()
            {
                ["error"] = "Error loading the stored hashes. Check the logs for more information"
            };
        }
    }

    [API.APIDescription("Deletes the hashes depending on the mode",
        """
        {
            "success": bool,
            "message": string
        }
        """)]
    public static async Task<JObject> DeleteImgMetadataConverterHashes(
        Session session,
        [API.APIParameter("The type of mode to delete the hashes")] string mode)
    {
        try
        {
            if (!Path.Exists(Utils.cacheFile))
            {
                return new JObject()
                {
                    ["success"] = false,
                    ["message"] = "There are no stored hashes, cannot delete"
                };
            }

            JObject storedHashes = JObject.Parse(File.ReadAllText(Utils.cacheFile));

            if (mode == "all_hashes")
            {
                if (storedHashes.Count > 0)
                {
                    File.WriteAllText(Utils.cacheFile, "{}");
                    return new JObject()
                    {
                        ["success"] = true,
                        ["message"] = "All hashes were deleted successfully"
                    };
                }
                else
                {
                    return new JObject()
                    {
                        ["success"] = false,
                        ["message"] = "There are no stored hashes to delete"
                    };
                }
            }
            else
            {
                if (storedHashes[mode.BeforeLast('.')] != null)
                {
                    storedHashes.Remove(mode.BeforeLast('.'));
                    File.WriteAllText(Utils.cacheFile, JsonConvert.SerializeObject(storedHashes, Formatting.Indented));

                    return new JObject()
                    {
                        ["success"] = true,
                        ["message"] = $"Successfully deleted the stored hash for {mode}"
                    };
                }
                else
                {
                    return new JObject()
                    {
                        ["success"] = false,
                        ["message"] = "Couldn't find the hash you're trying to delete"
                    };
                }
            }
        }
        catch (Exception e)
        {
            Logs.Debug($"{e}");
            return new JObject()
            {
                ["error"] = "Error deleting the hashes. Check the logs for more information"
            };
        }
    }

    [API.APIDescription("Calculates the hash of a model or all of them depending on the mode",
        """
        {
            "count": int,
            "total": int,
            "isDone": bool
        }
        """)]
    public static async Task<JObject> CalculateImgMetadataConverterHashWS(
        Session session,
        WebSocket ws,
        [API.APIParameter("The name of the model or the mode (overwrite or missing) to calculate the hash")] string mode)
    {
        try
        {
            JObject storedHashes = Path.Exists(Utils.cacheFile) ? JObject.Parse(File.ReadAllText(Utils.cacheFile)) : [];

            if (mode == "overwrite" || mode == "missing")
            {
                foreach (T2IModelHandler handler in Program.T2IModelSets.Values)
                {
                    int i = 1;
                    int length = handler.ListModelNamesFor(session).Count - 1;

                    foreach (string modelName in handler.ListModelNamesFor(session).Order().ToArray())
                    {                        
                        
                        if (modelName != "(None)")
                        {
                            await ws.SendJson(new JObject()
                            {
                                ["count"] = i,
                                ["total"] = length,
                                ["modelType"] = handler.ModelType,
                                ["isDone"] = false
                            }, API.WebsocketTimeout);
                            
                            string modelNameWithoutExtension = modelName.BeforeLast('.');

                            if (mode == "overwrite")
                            {
                                storedHashes[modelNameWithoutExtension] = Utils.CalculateAutoV3(handler.GetModel(modelName).RawFilePath);
                            }
                            else
                            {
                                storedHashes[modelNameWithoutExtension] = storedHashes[modelNameWithoutExtension] ?? Utils.CalculateAutoV3(handler.GetModel(modelName).RawFilePath);
                            }

                            i++;
                            await Task.Delay(500);
                        }
                    }
                }
                File.WriteAllText(Utils.cacheFile, JsonConvert.SerializeObject(storedHashes, Formatting.Indented));

                await ws.SendJson(new JObject()
                {
                    ["isDone"] = true
                }, API.WebsocketTimeout);
            }
            else
            {
                T2IModelHandler handler = Program.T2IModelSets.Values.FirstOrDefault(h => h.ListModelNamesFor(session).Contains(mode));
                if (handler == null)
                {
                    await ws.SendJson(new JObject()
                    {
                        ["error"] = "Model not found"
                    }, API.WebsocketTimeout);
                }

                await ws.SendJson(new JObject()
                {
                    ["count"] = 1,
                    ["total"] = 1,
                    ["modelType"] = handler.ModelType,
                    ["isDone"] = false
                }, API.WebsocketTimeout);

                storedHashes[mode.BeforeLast('.')] = Utils.CalculateAutoV3(handler.GetModel(mode).RawFilePath);
                File.WriteAllText(Utils.cacheFile, JsonConvert.SerializeObject(storedHashes, Formatting.Indented));

                await ws.SendJson(new JObject()
                {
                    ["isDone"] = true
                }, API.WebsocketTimeout);
            }
        }
        catch (Exception e)
        {
            Logs.Debug($"{e}");
            await ws.SendJson(new JObject()
            {
                ["error"] = "Error calculating the hash. Check the logs for more information"
            }, API.WebsocketTimeout);
        }

        return null;
    }

    [API.APIDescription("Searches for a model on civitai by hash",
        """
        {
            "success": bool,
            "message": string,
            "data": JObject
        }
        """)]
    public static async Task<JObject> SearchModelOnCivitaiByHash(
        Session session,
        [API.APIParameter("The hash of the resource to search for")] string hash)
    {
        try
        {   
            string requestUrl = $"https://civitai.com/api/v1/model-versions/by-hash/{hash.ToUpperFast()}";

            string response = await Utilities.UtilWebClient.GetStringAsync(requestUrl);
        
            return new JObject()
            {
                ["success"] = true,
                ["message"] = "Model found on civitai",
                ["data"] = response.ParseToJson()
            };
        }
        catch (HttpRequestException e)
        {
            return new JObject()
            {
                ["success"] = false,
                ["message"] = e.StatusCode == HttpStatusCode.NotFound ? "Model not found, might be too old to have an autoV3 hash" : $"{e.StatusCode}: {e.Message}"
            };
        }
        catch (Exception e)
        {
            Logs.Debug($"{e}");
            return new JObject()
            {
                ["error"] = "Error searching for the model on civitai. Check the logs for more information"
            };
        }
    }
}

