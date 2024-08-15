using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json.Linq;
using SwarmUI.Accounts;
using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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

    public async static void CreateVenvAndInstallDependencies()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string venvPath = Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, "src/Extensions/ImgMetadataConverter", "venv");
        string venvPythonPath = isWindows
            ? Utilities.CombinePathWithAbsolute(venvPath, "Scripts/python.exe")
            : Utilities.CombinePathWithAbsolute(venvPath, "bin/python");
        string pythonPlatform = isWindows
            ? "python"
            : "python3";

        Logs.Debug("[ImgMetadataConverter] Creating venv...");
        await Process.Start(new ProcessStartInfo(pythonPlatform, $"-m venv {venvPath}") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }).WaitForExitAsync(Program.GlobalProgramCancel);
        Logs.Debug("[ImgMetadataConverter] venv created successfully. Attempting to install requirements now...");
        await Process.Start(new ProcessStartInfo(venvPythonPath, "-s -m pip install -U pillow") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true }).WaitForExitAsync(Program.GlobalProgramCancel);
        Logs.Debug("[ImgMetadataConverter] Requirements installed successfully");
        Logs.Info("[ImgMetadataConverter] Created venv and installed the requirements.");
    }

    public static void CustomSaveImage(Image image, int batchIndex, T2IParamInput userInput, string metadata, User user, string outputDirectory)
    {
        if (!user.Settings.SaveFiles)
        {
            Logs.Error("You have the option SaveFiles disabled in your user settings");
            return;
        }
        string rawImagePath = user.BuildImageOutputPath(userInput, batchIndex);
        string imagePath = $"converted-{rawImagePath.Replace("[number]", "1")}";
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
            Logs.Verbose($"Image is type {image.Type} and will save with extension '{image.Extension}'.");
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
                    imagePath = rawImagePath.Contains("[number]") ? rawImagePath.Replace("[number]", $"{num}") : $"{rawImagePath}-{num}";
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
                    Logs.Error($"Could not save user image (to '{pathA}' nor to '{fullPath}': first error '{e1.Message}', second error '{ex.Message}'");
                    return;
                }
            }
        }
    }
}
