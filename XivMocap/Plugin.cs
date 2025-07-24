using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using XivMocap.Windows;
using XivMocap.GameObjects;
using System.Threading.Tasks;
using System.Threading;
using Brio;
using Brio.Capabilities.Posing;
using Brio.Game.Posing;
using System.Numerics;
using Brio.Core;
using Everything_To_IMU_SlimeVR.Osc;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace XivMocap;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    [PluginService] internal static IFramework Framework { get; private set; } = null!;


    private const string CommandName = "/pmycommand";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private MediaBoneManager mediaBoneManager;
    private nint _address;
    private bool setAddress;
    private Brio.Brio _brio;
    private OscHandler _oscHandler;
    private SkeletonPosingCapability _posingCapability;
    private bool _disposed;
    Dictionary<string, BonePoseInfo> _bones = new Dictionary<string, BonePoseInfo>();
    Dictionary<string, string> _boneNameMapping = new Dictionary<string, string>()
    {
        {"Hips", "j_kosi" },
        {"Spine", "j_sebo_a" },
        {"Chest", "j_sebo_b" },
        {"UpperChest", "j_sebo_c" },

        {"LeftShoulder", "j_sako_l" },
        {"LeftUpperArm", "j_ude_a_l" },
        {"LeftLowerArm", "j_ude_b_l" },
        {"LeftHand", "j_te_l" },

        {"RightShoulder", "j_sako_r" },
        {"RightUpperArm", "j_ude_a_r" },
        {"RightLowerArm", "j_ude_b_r" },
        {"RightHand", "j_te_r" },

        {"LeftUpperLeg", "j_asi_a_l" },
        {"LeftLowerLeg", "j_asi_c_l" },
        {"LeftFoot", "j_asi_d_l" },
        {"LeftToes", "j_adi_e_l" },

        {"RightUpperLeg", "j_asi_a_r" },
        {"RightLowerLeg", "j_asi_c_r" },
        {"RightFoot", "j_asi_d_r" },
        {"RightToes", "j_adi_e_r" },

        {"Neck", "j_kubi" },
        {"Head", "j_kao" },
        {"root", "n_root" },
    };
    Stopwatch _startingCooldown = new Stopwatch();
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
        Framework.Update += Framework_Update;
    }

    private void Framework_Update(IFramework framework)
    {
        if (!_disposed)
        {
            try
            {
                if (ClientState.LocalPlayer != null && ClientState.IsLoggedIn)
                {
                    if (!_startingCooldown.IsRunning)
                    {
                        _startingCooldown.Start();
                        _brio = new Brio.Brio(PluginInterface);
                        _oscHandler = new OscHandler();
                        _oscHandler.BoneUpdate += _oscHandler_BoneUpdate;
                    }
                    if (_startingCooldown.ElapsedMilliseconds > 5000)
                    {
                        if (!setAddress)
                        {
                            _address = ClientState.LocalPlayer.Address;
                            setAddress = true;
                        }
                        if (setAddress)
                        {
                            if (_posingCapability == null)
                            {
                                BrioAccessUtils.EntityManager.SetSelectedEntity(ClientState.LocalPlayer);
                                BrioAccessUtils.EntityManager.TryGetCapabilityFromSelectedEntity<SkeletonPosingCapability>(out _posingCapability);
                            }
                            if (_posingCapability != null)
                            {
                                if (_posingCapability.SkeletonService.Skeletons.Count > 0)
                                {
                                    foreach (var bone in _posingCapability.SkeletonService.Skeletons[0].Bones)
                                    {
                                        if (!_bones.ContainsKey(bone.Name))
                                        {
                                            _bones[bone.Name] = _posingCapability.GetBonePose(bone);
                                            Plugin.Log.Info(bone.Name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Warning(e, e.Message);
            }
        }
    }

    private void _oscHandler_BoneUpdate(object? sender, Tuple<string, Vector3,  Quaternion> e)
    {
        Framework.RunOnFrameworkThread(() =>
        {
            _bones[_boneNameMapping[e.Item1]].Apply(new Transform() { Position = e.Item2, Rotation = e.Item3, Scale = new Vector3() });
        });
    }

    public void Dispose()
    {
        _disposed = true;
        Framework.Update -= Framework_Update;
        _oscHandler?.Dispose();
        _brio?.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow?.Dispose();
        MainWindow?.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
