using SwarmUI.Core;
using SwarmUI.Utils;
using System.IO;
using Newtonsoft.Json;
using ImgMetadataConverter.WebAPI;
using Newtonsoft.Json.Linq;
using SwarmUI.Text2Image;

namespace ImgMetadataConverter;

public class ImgMetadataConverter : Extension
{
    public string settingsFile = Utils.settingsFile;

    public override void OnInit()
    {
        Logs.Init("[ImgMetadataConverter] loaded");

        ScriptFiles.Add("js/ImgMetadataConverter.js");
        Logs.Debug("[ImgMetadataConverter] Added the script files.");

        ImgMetadataConverterAPI.Register();
        Logs.Debug("[ImgMetadataConverter] Registered API callbacks.");

        foreach ((string key, JToken val) in Utils.ParsedSubfolders())
        {
            Logs.Debug($"[ImgMetadataConverter] Registered path for {key} folder: {val}");
        }

        if (!Path.Exists(settingsFile))
        {
            JObject defaultSettings = new()
            {
                ["active"] = false,
                ["cache"] = true,
                ["outputDirectory"] = "[SwarmUI.OutputPath]"
            };

            File.WriteAllText(settingsFile, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
            Logs.Debug("[ImgMetadataConverter] Created default config file.");
        }

        T2IEngine.PostGenerateEvent += PostGenerationEvent;
    }

    public override void OnShutdown()
    {
        T2IEngine.PostGenerateEvent -= PostGenerationEvent;
    }

    public void PostGenerationEvent(T2IEngine.PostGenerationEventParams param)
    {
        JObject settings = JObject.Parse(File.ReadAllText(settingsFile));

        if (!settings.Value<bool>("active"))
        {
            return;
        }

        string metadata = Utils.FormatMetadata(param.UserInput.ToJSON(), settings);
        if (metadata == null)
        {
            return;
        }

        try
        {
            string format = param.UserInput.Get(T2IParamTypes.ImageFormat, param.UserInput.SourceSession.User.Settings.FileFormat.ImageFormat);
            Image image = param.Image.ConvertTo(format, param.UserInput.SourceSession.User.Settings.FileFormat.SaveMetadata ? metadata : null, param.UserInput.SourceSession.User.Settings.FileFormat.DPI);
            string outputDirectory = settings.Value<string>("outputDirectory").Replace("[SwarmUI.OutputPath]", Program.ServerSettings.Paths.OutputPath);
            Utils.CustomSaveImage(image, 0, param.UserInput, metadata, param.UserInput.SourceSession.User, Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, outputDirectory));
        }
        catch (Exception e)
        {
            Logs.Error($"[ImgMetadataConverter] Something unexpected ocurred: {e}");
        }
    }
}
