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

    public override void OnInit()
    {
        Logs.Init("ImgMetadataConverter is ready!");

        ScriptFiles.Add("js/ImgMetadataConverter.js");
        Logs.Debug("[ImgMetadataConverter] Added the script files.");

        ImgMetadataConverterAPI.Register();
        Logs.Debug("[ImgMetadataConverter] Registered API callbacks.");

        string settingsFile = Utils.settingsFile;

        if (!Path.Exists(settingsFile))
        {
            JObject defaultSettings = new JObject()
            {
                ["cache"] = true,
                ["outputDirectory"] = Program.ServerSettings.Paths.OutputPath
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
                    if (await DoPostRequest("API/ImgMedataConverterPing", []) != null)
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

            NetworkBackendUtils.DoSelfStart(FilePath + "convert_metadata.py", "ImgMetadataConverter", "imgmetadataconverter", "0", "{PORT}", s => Status = s, Check, (p, r) => { Port = p; RunningProcess = r; }, () => Status, a => ShutDownEvent += a).Wait();
        }
    }

    public async Task<JObject> DoPostRequest(string url, JObject data)
    {
        return (await (await WebClient.PostAsync($"http://localhost:{Port}/{url}", Utilities.JSONContent(data))).Content.ReadAsStringAsync()).ParseToJson();
    }

    public async Task ConvertImgMetadata(Image img, JObject userInput, JObject subfolders)
    {
        EnsureActive();
    }

    public void PostGenerationEvent(T2IEngine.PostGenerationEventParams param)
    {
        
    }
}
