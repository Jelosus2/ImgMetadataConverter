﻿using SwarmUI.Core;
using SwarmUI.Utils;
using System.IO;
using Newtonsoft.Json;
using ImgMetadataConverter.WebAPI;
using Newtonsoft.Json.Linq;
using SwarmUI.Text2Image;
using SwarmUI.Accounts;

namespace ImgMetadataConverter;

public class ImgMetadataConverter : Extension
{
    public string settingsFile = Utils.settingsFile;

    public override void OnInit()
    {
        Logs.Init("[ImgMetadataConverter] loaded");
        
        ScriptFiles.Add("js/ImgMetadataConverter.js");
        Logs.Debug("[ImgMetadataConverter] Added the script files.");

        OtherAssets.AddRange(Utils.assetFiles);
        Logs.Debug("[ImgMetadataConverter] Added the asset files.");

        ImgMetadataConverterAPI.Register();
        Logs.Debug("[ImgMetadataConverter] Registered API callbacks.");

        if (!Path.Exists(settingsFile))
        {
            JObject defaultSettings = new()
            {
                ["active"] = false,
                ["cache"] = true,
                ["outputDirectory"] = "[SwarmUI.OutputPath]",
                ["skipDuplicates"] = false,
                ["appendOutPathBuild"] = false
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

        string metadata = Utils.FormatMetadata(param.UserInput, settings);
        if (metadata == null)
        {
            return;
        }

        User user = param.UserInput.SourceSession.User;

        try
        {
            string format = param.UserInput.Get(T2IParamTypes.ImageFormat, user.Settings.FileFormat.ImageFormat);
            Image image = param.Image.ConvertTo(format, user.Settings.FileFormat.SaveMetadata ? metadata : null, user.Settings.FileFormat.DPI);

            if (!settings.Value<bool>("skipDuplicates"))
            {
                string outputDirectory = settings.Value<string>("outputDirectory").Replace("[SwarmUI.OutputPath]", user.OutputDirectory);
                bool useOutPathBuilder = settings.Value<bool>("appendOutPathBuild");
                Utils.CustomSaveImage(image, 0, param.UserInput, metadata, user, Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, Utils.PathCleanUp(outputDirectory)), useOutPathBuilder);
            }
            else
            {
                param.Image.ImageData = image.ImageData;
                param.Image.Type = Image.ImageType.ANIMATION;
            }
        }
        catch (Exception e)
        {
            Logs.Error($"[ImgMetadataConverter] Something unexpected ocurred: {e}");
        }
    }
}
