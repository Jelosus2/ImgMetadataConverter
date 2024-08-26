using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
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
    public static readonly JObject subfolders = new()
    {
        ["SDLoraFolder"] = "Lora",
        ["unet"] = "Unet",
        ["SDModelFolder"] = "Stable-Diffusion"
    };
    private static readonly Settings.PathsData paths = Program.ServerSettings.Paths;

    public static JObject ParsedSubfolders()
    {
        JObject subfoldersObj = [];

        foreach ((string key, JToken val) in subfolders)
        {
            subfoldersObj[$"{val}"] = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, paths.ModelRoot, paths.GetFieldValueOrDefault<string>(key) ?? key);
        }

        return subfoldersObj;
    }

    public static string PathCleanUp(string path)
    {
        path = Utilities.FilePathForbidden.TrimToNonMatches(path.Replace('\\', '/'));
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }
        path = path.Trim();
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
     
        return parts.JoinString("/");
    }

    public static void CreateCacheFile()
    {
        File.WriteAllText(cacheFile, "{}");
    }

    public static string FormatMetadata(JObject userInput, JObject settings)
    {
        HashSet<string> excludeParams = ["prompt", "negativeprompt", "cfgscale", "steps", "sampler", "scheduler", "seed", "width", "height", "model", "loras"];

        string prompt = userInput.Value<string>("prompt");
        string negativePrompt = userInput.Value<string>("negativeprompt") ?? "";
        string cfgScale = userInput.Value<string>("cfgscale");
        string steps = userInput.Value<string>("steps");
        string sampler = userInput.Value<string>("sampler") ?? "euler";
        string scheduler = userInput.Value<string>("scheduler") ?? "normal";
        string seed = userInput.Value<string>("seed");
        string size = $"{userInput.Value<string>("width")}x{userInput.Value<string>("height")}";
        string model = userInput.Value<string>("model");

        string loraHashes = LoraHashStringBuilder(userInput.Value<string>("loras")?.Split(",") ?? [], settings.Value<bool>("cache")) ?? "";
        string modelHash = CalculateModelHash(model, settings.Value<bool>("cache"));

        if (loraHashes == "lora-error" || modelHash == "model-error")
        {
            return null;
        }

        string newMetadataString = $"{prompt}\nNegative prompt: {negativePrompt}\nSteps: {steps}, Sampler: {sampler}, Schedule type: {scheduler}, CFG scale: {cfgScale}, Seed: {seed}, Size: {size}, Model hash: {modelHash}, Model: {model},{loraHashes}";

        foreach ((string key, JToken val) in userInput)
        {
            if (!excludeParams.Contains(key) && !(key.Contains("initimage") || key.Contains("imageinput")))
            {
                string value = val.ToString().Contains(',') ? $"\"{val}\"" : val.ToString();
                newMetadataString += $" {key}: {value},";
            }
        }

        return newMetadataString;
    }

    public static string LoraHashStringBuilder(string[] loras, bool cache)
    {
        if (loras.Length == 0)
        {
            return null;
        }

        string loraDir = ParsedSubfolders().Value<string>("Lora");
        if (!Path.Exists(loraDir))
        {
            Logs.Error("[ImgMetadataConverter] LoRAs were detected in the metadata but didn't find the LoRA folder");
            return "lora-error";
        }

        string loraHashes = " Lora hashes: \"";

        for (int i = 0; i < loras.Length; i++)
        {
            string loraName = loras[i].Split("/")[^1];

            string[] extensions = [$"{loraName}.safetensors", $"{loraName}.ckpt"];
            List<string> matchingLoras = [];

            Logs.Debug($"[ImgMetadataConverter] Attempting to search for lora '{loraName}' in {loraDir}");

            foreach (string extension in extensions)
            {
                matchingLoras.AddRange(Directory.GetFiles(loraDir, extension, SearchOption.AllDirectories));
            }

            if (matchingLoras.Count > 0)
            {
                string autoV3Hash = CalculateAutoV3(matchingLoras[0], cache);
                if (autoV3Hash == null)
                {
                    return "lora-error";
                }

                if (i == loras.Length - 1)
                {
                    loraHashes += $"{loraName}: {autoV3Hash}\",";
                }
                else
                {
                    loraHashes += $"{loraName}: {autoV3Hash}, ";
                }
            }
            else
            {
                Logs.Error("[ImgMetadataConverter] No LoRA in the specified LoraFolder path matched the metadata, please check again");
                return "lora-error";
            }
        }

        return loraHashes;
    }

    public static string CalculateModelHash(string model, bool cache)
    {
        string sdDir = ParsedSubfolders().Value<string>("Stable-Diffusion");
        string unetDir = ParsedSubfolders().Value<string>("Unet");

        if (!Path.Exists(sdDir) || !Path.Exists(unetDir))
        {
            Logs.Error("[ImgMetadataConverter] Couldn't find the Stable-Diffusion or Unet directory");
            return "model-error";
        }

        string modelName = model.Split("/")[^1];

        string[] extensions = [$"{modelName}.safetensors", $"{modelName}.ckpt", $"{modelName}.sft", $"{modelName}.engine", $"{modelName}.gguf"];
        List<string> matchingSDModel = [];
        List<string> matchingUnetModel = [];

        Logs.Debug($"[ImgMetadataConverter] Attempting to search for model '{modelName}' in {sdDir} or {unetDir}");

        foreach (string extension in extensions)
        {
            matchingSDModel.AddRange(Directory.GetFiles(sdDir, extension, SearchOption.AllDirectories));
            matchingUnetModel.AddRange(Directory.GetFiles(unetDir, extension, SearchOption.AllDirectories));
        }

        if (matchingSDModel.Count == 0 && matchingUnetModel.Count == 0)
        {
            Logs.Error("[ImgMetadataConverter] No Checkpoint found in Stable-Diffusion and unet folders");
            return "model-error";
        }

        string autoV3Hash = matchingSDModel.Count > 0
            ? CalculateAutoV3(matchingSDModel[0], cache)
            : CalculateAutoV3(matchingUnetModel[0], cache);

        if (autoV3Hash == null)
        {
            return "model-error";
        }

        return autoV3Hash;
    }

    public static string CalculateAutoV3(string filePath, bool cache)
    {
        if (cache && !Path.Exists(cacheFile))
        {
            CreateCacheFile();
        }

        JObject cacheObj = [];

        if (cache)
        {
            try
            {
                cacheObj = JObject.Parse(File.ReadAllText(cacheFile));

                cacheObj.TryGetValue(Path.GetFileNameWithoutExtension(filePath), out JToken cacheVal);
                if (cacheVal != null)
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
        long position = reader.Position;
        JObject metadataHeader = (jsonObj["__metadata__"] as JObject) ?? [];

        string hash = (metadataHeader?.ContainsKey("modelspec.hash_sha256") ?? false) ? metadataHeader.Value<string>("modelspec.hash_sha256") : "0x" + Utilities.BytesToHex(SHA256.HashData(reader));
        string autoV3Hash = hash.StartsWith("0x") ? hash.Substring(2, 12) : hash[..10];

        if (cache)
        {
            cacheObj[Path.GetFileNameWithoutExtension(filePath)] = autoV3Hash;
            File.WriteAllText(cacheFile, JsonConvert.SerializeObject(cacheObj, Formatting.Indented));
        }

        Logs.Debug($"[ImgMetadataConverter] Calculated hash for {Path.GetFileNameWithoutExtension(filePath)}: {autoV3Hash}");

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
