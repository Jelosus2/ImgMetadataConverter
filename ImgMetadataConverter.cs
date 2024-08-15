using SwarmUI.Core;
using SwarmUI.Utils;
using System.IO;
using Newtonsoft.Json;
using ImgMetadataConverter.WebAPI;
using Newtonsoft.Json.Linq;
using SwarmUI.Text2Image;
using System.Diagnostics;
using SwarmUI.Backends;
using FreneticUtilities.FreneticToolkit;
using System.Net.Http;

namespace ImgMetadataConverter;

public class ImgMetadataConverter : Extension
{
    public static Action ShutDownEvent;
    public string settingsFile = Utils.settingsFile;

    public override void OnInit()
    {
        Logs.Init("[ImgMetadataConverter] loaded");

        ScriptFiles.Add("js/ImgMetadataConverter.js");
        Logs.Debug("[ImgMetadataConverter] Added the script files.");

        ImgMetadataConverterAPI.Register();
        Logs.Debug("[ImgMetadataConverter] Registered API callbacks.");

        if (!Path.Exists(settingsFile))
        {
            JObject defaultSettings = new JObject()
            {
                ["cache"] = true,
                ["outputDirectory"] = "[SwarmUI.OutputPath]"
            };

            string jsonString = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

            File.WriteAllText(settingsFile, jsonString);
            Logs.Debug("[ImgMetadataConverter] Created default config file.");
        }

        T2IEngine.PostGenerateEvent += PostGenerationEvent;
    }

    public override void OnShutdown()
    {
        ShutDownEvent?.Invoke();
        T2IEngine.PostGenerateEvent -= PostGenerationEvent;

        if (RunningProcess != null)
        {
            if (!RunningProcess.HasExited)
            {
                RunningProcess.Kill();
            }
            RunningProcess = null;
        }
    }

    public static HttpClient WebClient;
    public int Port;
    public Process RunningProcess;
    public volatile BackendStatus Status = BackendStatus.DISABLED;
    public LockObject InitLock = new();

    public void EnsureActive()
    {
        while (Status == BackendStatus.LOADING)
        {
            Task.Delay(TimeSpan.FromSeconds(0.5)).Wait(Program.GlobalProgramCancel);
        }
        lock (InitLock)
        {
            if (Status == BackendStatus.RUNNING || Program.GlobalProgramCancel.IsCancellationRequested)
            {
                return;
            } 
            
            WebClient ??= NetworkBackendUtils.MakeHttpClient();
            async Task<bool> Check(bool _)
            {
                try
                {
                    if (await DoPostRequest("API/Alive", []) != null)
                    {
                        Status = BackendStatus.RUNNING;
                    }
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            NetworkBackendUtils.DoSelfStart(FilePath + "httpserver.py", "ImgMetadataConverter", "imgmetadataconverter", "0", "{PORT}", s => Status = s, Check, (p, r) => { Port = p; RunningProcess = r; }, () => Status, a => ShutDownEvent += a).Wait();
        }
    }

    public async Task<JObject> DoPostRequest(string url, JObject data)
    {
        return (await (await WebClient.PostAsync($"http://localhost:{Port}/{url}", Utilities.JSONContent(data))).Content.ReadAsStringAsync()).ParseToJson();
    }

    public async Task<JObject> GetImageMetadata(JObject userInput, JObject subfolders, JObject settings)
    {
        EnsureActive();
        JObject result = await DoPostRequest("API/ConvertMetadata", new() { ["userInput"] = userInput, ["subfolders"] = subfolders, ["settings"] = settings });

        return result;
    }

    public void PostGenerationEvent(T2IEngine.PostGenerationEventParams param)
    {
        JObject settings = JObject.Parse(File.ReadAllText(settingsFile));
        try
        {
            JObject response = GetImageMetadata(param.UserInput.ToJSON(), Utils.parsedSubfolders(), settings).Result;

            if (response.TryGetValue("result", out JToken result) && result.ToString() == "fail")
            {
                Logs.Error($"[ImgMetadataConverter] {response.GetValue("error").ToString()}");
            }
            else
            {
                if (response.TryGetValue("metadata", out JToken metadata))
                {
                    string format = param.UserInput.Get(T2IParamTypes.ImageFormat, param.UserInput.SourceSession.User.Settings.FileFormat.ImageFormat);
                    Image image = param.Image.ConvertTo(format, param.UserInput.SourceSession.User.Settings.FileFormat.SaveMetadata ? metadata.ToString() : null, param.UserInput.SourceSession.User.Settings.FileFormat.DPI);
                    string outputDirectory = settings["outputDirectory"].ToString().Replace("[SwarmUI.OutputPath]", Program.ServerSettings.Paths.OutputPath);
                    Utils.CustomSaveImage(image, 1, param.UserInput, metadata.ToString(), param.UserInput.SourceSession.User, Utilities.CombinePathWithAbsolute(Environment.CurrentDirectory, outputDirectory));
                }
            }
        }
        catch (Exception e)
        {
            Logs.Error($"Something unexpected ocurred: {e}");
        }
    }
}
