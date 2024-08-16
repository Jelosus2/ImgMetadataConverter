using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.IO;

namespace ImgMetadataConverter;

public static class Utils
{
    public static readonly string settingsFile = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, "src/Extensions/ImgMetadataConverter", "settings.json");
    public static readonly JObject subfolders = new()
    {
        ["SDLoraFolder"] = "Lora",
        ["unet"] = "Unet",
        ["SDModelFolder"] = "Stable-Diffusion"
    };
    private static readonly Settings.PathsData paths = Program.ServerSettings.Paths;

    public static JObject parsedSubfolders()
    {
        JObject subfoldersObj = new();

        foreach ((string key, JToken val) in subfolders)
        {
            subfoldersObj[$"{val}"] = paths.GetFieldValueOrDefault<string>(key) != null
                ? Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, paths.ModelRoot, paths.GetFieldValueOrDefault<string>(key))
                : Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, paths.ModelRoot, key);
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

    public static void CustomSaveImage(Image image, int batchIndex, T2IParamInput userInput, string metadata, User user, string outputDirectory)
    {
        if (!user.Settings.SaveFiles)
        {
            Logs.Warning("[ImgMetadataConverter] You have the option SaveFiles disabled in your user settings, skipping conversion...");
            return;
        }
        string rawImagePath = user.BuildImageOutputPath(userInput, batchIndex);
        string rawFileName = Path.GetFileNameWithoutExtension(rawImagePath);
        string fileName = rawFileName.Replace("[number]", "1").Replace(rawFileName, $"converted-{rawFileName}");
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
        string fullPath = $"{outputDirectory}/{fileName}.{extension}";
        lock (user.UserLock)
        {
            try
            {
                int num = 0;
                while (File.Exists(fullPath))
                {
                    num++;
                    fileName = rawImagePath.Contains("[number]") ? rawImagePath.Replace("[number]", $"{num}") : $"{rawImagePath}-{num}";
                    fullPath = $"{outputDirectory}/{fileName}.{extension}";
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
                    fileName = "image_name_error/" + Utilities.SecureRandomHex(10);
                    fullPath = $"{outputDirectory}/{fileName}.{extension}";
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
