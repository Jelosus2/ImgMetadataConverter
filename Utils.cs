using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.IO;
using System.Security.Cryptography;

namespace ImgMetadataConverter;

public static class Utils
{
    public static readonly string settingsFile = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, "src/Extensions/ImgMetadataConverter", "settings.json");
    public static readonly string cacheFile = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, "src/Extensions/ImgMetadataConverter", "cache.json");

    public static readonly List<string> assetFiles = ["assets/trash-can.svg", "assets/globe.svg", "assets/refresh.svg"];

    public static string PathCleanUp(string path)
    {
        path = path.Replace('\\', '/');
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }
        path = path.Trim();
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        for (int i = 0; i < parts.Length; i++)
        {
            if (i == 0 && parts[i].Contains(':') && parts[i].Length == 2)
            {
                parts[i] = parts[i];
            }
            else
            {
                parts[i] = Utilities.FilePathForbidden.TrimToNonMatches(parts[i]);
            }
        }

        return parts.JoinString("/");
    }

    public static void CreateCacheFile()
    {
        File.WriteAllText(cacheFile, "{}");
    }

    public static string FormatMetadata(T2IParamInput userInput, JObject settings)
    {
        HashSet<string> excludeParams = ["prompt", "negativeprompt", "cfgscale", "steps", "sampler", "scheduler", "seed", "width", "height", "model", "loras"];

        string prompt = userInput.Get(T2IParamTypes.Prompt);
        string negativePrompt = userInput.Get(T2IParamTypes.NegativePrompt, "");
        double cfgScale = userInput.Get(T2IParamTypes.CFGScale);
        int steps = userInput.Get(T2IParamTypes.Steps);
        string sampler = userInput.Get(ComfyUIBackendExtension.SamplerParam);
        string scheduler = userInput.Get(ComfyUIBackendExtension.SchedulerParam);
        long seed = userInput.Get(T2IParamTypes.Seed);
        int width = userInput.Get(T2IParamTypes.Width);
        int height = userInput.Get(T2IParamTypes.Height);

        T2IModel model = userInput.Get(T2IParamTypes.Model);
        List<string> loras = userInput.Get(T2IParamTypes.Loras, []); 

        string LoraHashStringBuilder()
        {
            if (loras.Count == 0)
            {
                return null;
            }

            string loraHashes = " Lora hashes: \"";

            for (int i = 0; i < loras.Count; i++)
            {
                T2IModelHandler loraHandler = Program.T2IModelSets["LoRA"];

                if (!loraHandler.Models.TryGetValue($"{loras[i]}.safetensors", out T2IModel lora))
                {
                    if (!loraHandler.Models.TryGetValue(loras[i], out lora))
                    {
                        Logs.Error($"[ImgMetadataConverter] LoRA {loras[i]} not found, be sure it's a safetensors file.");
                        return "lora-error";
                    }
                }

                string autoV3Hash = CalculateAutoV3(lora.RawFilePath, loras[i]);
                if (autoV3Hash == null)
                {
                    return "lora-error";
                }

                if (i == loras.Count - 1)
                {
                    loraHashes += $"{loras[i].AfterLast('/')}: {autoV3Hash}\",";
                }
                else
                {
                    loraHashes += $"{loras[i].AfterLast('/')}: {autoV3Hash}, ";
                }
            }

            return loraHashes;
        }

        string CalculateModelHash()
        {
            if (!File.Exists(model.RawFilePath))
            {
                Logs.Error($"[ImgMetadataConverter] {model.Title} not found");
                return "model-error";
            }

            string autoV3Hash = CalculateAutoV3(model.RawFilePath, model.Name.BeforeLast('.'));

            if (autoV3Hash == null)
            {
                return "model-error";
            }

            return autoV3Hash;
        }

        string loraHashes = LoraHashStringBuilder() ?? "";
        string modelHash = CalculateModelHash();

        if (loraHashes == "lora-error" || modelHash == "model-error")
        {
            return null;
        }

        string newMetadataString = $"{prompt}\nNegative prompt: {negativePrompt}\nSteps: {steps}, Sampler: {sampler}, Schedule type: {scheduler}, CFG scale: {cfgScale}, Seed: {seed}, Size: {width}x{height}, Model hash: {modelHash}, Model: {model},{loraHashes}";

        foreach ((string key, JToken val) in userInput.ToJSON())
        {
            if (!excludeParams.Contains(key) && val.ToString().Length < 250)
            {
                string value = val.ToString().Contains(',') ? $"\"{val}\"" : val.ToString();
                newMetadataString += $" {key}: {value},";
            }
        }

        return newMetadataString;
    }

    public static string CalculateAutoV3(string filePath, string modelName = "")
    {
        bool cache = !string.IsNullOrWhiteSpace(modelName);

        if (cache && !File.Exists(cacheFile))
        {
            CreateCacheFile();
        }

        JObject cacheObj = [];

        if (cache)
        {
            try
            {
                cacheObj = JObject.Parse(File.ReadAllText(cacheFile));

                if (cacheObj.TryGetValue(modelName, out JToken cacheVal))
                {
                    return cacheVal.ToString();
                }
            }
            catch (JsonReaderException)
            {
                File.Delete(cacheFile);
                CreateCacheFile();
                cacheObj = [];
            }
        }

        using FileStream reader = File.OpenRead(filePath);
        byte[] headerLength = new byte[8];
        reader.ReadExactly(headerLength, 0, 8);

        long length = BitConverter.ToInt64(headerLength, 0);
        if (length < 0 || length > 100 * 1024 * 1024)
        {
            Logs.Error($"[ImgMetadataConverter] Model {Path.GetFileName(filePath)} has invalid metadata length {length}, aborting...");
            return null;
        }

        byte[] header = new byte[length];
        reader.ReadExactly(header, 0, (int)length);

        string headerString = Encoding.UTF8.GetString(header);
        JObject jsonObj = JObject.Parse(headerString);
        JObject metadataHeader = (jsonObj["__metadata__"] as JObject) ?? [];

        string hash = (metadataHeader?.ContainsKey("modelspec.hash_sha256") ?? false) ? metadataHeader.Value<string>("modelspec.hash_sha256") : "0x" + Utilities.BytesToHex(SHA256.HashData(reader));
        string autoV3Hash = hash.StartsWith("0x") ? hash.Substring(2, 12) : hash[..10];

        if (cache)
        {
            cacheObj[modelName] = autoV3Hash;
            File.WriteAllText(cacheFile, JsonConvert.SerializeObject(cacheObj, Formatting.Indented));
        }

        Logs.Debug($"[ImgMetadataConverter] Calculated hash for {modelName}: {autoV3Hash}");

        return autoV3Hash;
    }

    public static void CustomSaveImage(Image image, int batchIndex, T2IParamInput userInput, string metadata, User user, string outputDirectory, bool useOutPathBuilder)
    {
        if (!user.Settings.SaveFiles)
        {
            Logs.Warning("[ImgMetadataConverter] You have the option SaveFiles disabled in your user settings, skipping conversion...");
            return;
        }
        string rawImagePath = user.BuildImageOutputPath(userInput, batchIndex);
        string rawFileName = Path.GetFileNameWithoutExtension(rawImagePath);
        string imagePath = (useOutPathBuilder ? rawImagePath : rawFileName).Replace("[number]", "1").Replace(rawFileName, $"{rawFileName}-converted");
        string format = userInput.Get(T2IParamTypes.ImageFormat, user.Settings.FileFormat.ImageFormat);
        string extension;
        try
        {
            extension = Image.ImageFormatToExtension(format);
        }
        catch (Exception)
        {
            extension = "jpg";
        }
        if (image.Type != Image.ImageType.IMAGE)
        {
            extension = image.Extension;
        }
        string fullPath = $"{outputDirectory}/{imagePath}.{extension}";
        lock (user.UserLock)
        {
            try
            {
                int num = 0;
                while (File.Exists(fullPath))
                {
                    num++;
                    imagePath = rawImagePath.Contains("[number]") 
                        ? (useOutPathBuilder ? rawImagePath : rawFileName).Replace("[number]", $"{num}").Replace(rawFileName, $"{rawFileName}-converted")
                        : $"{(useOutPathBuilder ? rawImagePath : rawFileName)}-{num}-converted";
                    fullPath = $"{outputDirectory}/{imagePath}.{extension}";
                }
                Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                File.WriteAllBytes(fullPath, image.ImageData);
                if (user.Settings.FileFormat.SaveTextFileMetadata && !string.IsNullOrWhiteSpace(metadata))
                {
                    File.WriteAllBytes(fullPath.BeforeLast('.') + ".txt", metadata.EncodeUTF8());
                }
            }
            catch (Exception e1)
            {
                string pathA = fullPath;
                try
                {
                    imagePath = "image_name_error/" + Utilities.SecureRandomHex(10);
                    fullPath = $"{outputDirectory}/{imagePath}.{extension}";
                    Directory.CreateDirectory(Directory.GetParent(fullPath).FullName);
                    File.WriteAllBytes(fullPath, image.ImageData);
                }
                catch (Exception ex)
                {
                    Logs.Error($"[ImgMetadataConverter] Could not save user image (to '{pathA}' nor to '{fullPath}': first error '{e1.Message}', second error '{ex.Message}'");
                    return;
                }
            }
        }
    }
}
