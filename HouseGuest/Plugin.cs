using System;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HouseGuest.GUI;
using VT = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace HouseGuest;

public sealed partial class Plugin : IDalamudPlugin
{
    /*
     * Closing:
     *  /housing
     *  /callback "HousingMenu" true 3 <errorif.addonnotfound>
     *  /callback "HousingSelectHouse" true 3 <errorif.addonnotfound>
     *  /callback "HousingSubmenu" true 2 <errorif.addonnotfound>
     *  /callback "HousingConfig" true 0 0 <errorif.addonnotfound>
     */

    public enum HouseGuestState
    {
        Finished,
        InHousingMenu,
        InHousingSelectHouse,
        InHousingSubmenu,
        InHousingConfig,
        None
    }

    private const string CommandName = "/guest";

    public static TaskManager TaskManager = null!;
    private readonly MainWindow _mainWindow;

    private readonly WindowSystem _windowSystem;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;

        ECommonsMain.Init(pluginInterface, this);

        TaskManager = new TaskManager();

        var overlay1 = new ProgressOverlay();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "/guest [open|close]"
        });

        _windowSystem = new WindowSystem("HouseGuest");
        _windowSystem.AddWindow(overlay1);
        _mainWindow = new MainWindow(this);
        _windowSystem.AddWindow(_mainWindow);

        PluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }


    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IGameGui GameGui { get; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IDataManager DataManager { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; } = null!;

    public void Dispose()
    {
        CommandManager.RemoveHandler("/guestdebug");
        CommandManager.RemoveHandler(CommandName);

        PluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ECommonsMain.Dispose();
    }

    public static HouseGuestState GetHouseGuestState()
    {
        if (GameGui.GetAddonByName("HousingMenu").IsVisible) {
            return HouseGuestState.InHousingMenu;
        }

        if (GameGui.GetAddonByName("HousingSelectHouse").IsVisible) {
            return HouseGuestState.InHousingSelectHouse;
        }

        if (GameGui.GetAddonByName("HousingSubmenu").IsVisible) {
            return HouseGuestState.InHousingSubmenu;
        }

        if (GameGui.GetAddonByName("HousingConfig").IsVisible) {
            return HouseGuestState.InHousingConfig;
        }

        return HouseGuestState.None;
    }

    private void ToggleMainUi()
    {
        _mainWindow.IsOpen = !_mainWindow.IsOpen;
    }

    internal void StartGuestAction(bool open)
    {
        if (TaskManager.IsBusy) {
            Svc.Chat.Print("HouseGuest is already running. Please wait or abort.");
            TaskManager.Abort();
        }

        TaskManager.EnqueueTask(HousingMenuTask(open));
    }

    private unsafe void HandleHousingMenu(AtkUnitBase* addonPtr)
    {
        // Find Estate Settings
        const string addonName = "HousingMenu";

        var values = addonPtr->AtkValuesSpan;

        if (!EzThrottler.Throttle("HandleHousingMenu", TimeSpan.FromSeconds(2))) {
            return;
        }

        if (values.Length < 8) {
            Log.Error($"Unexpected AtkValues in {addonName}.");
            return;
        }

        var length = values[3].Int;

        if (length == 1) {
            // Only move to front door
            Log.Information("Only one house found in HousingMenu. Moving to front door.");

            TaskManager.Abort();

            Svc.NotificationManager.AddNotification(new Notification
            {
                Title = "Housing Guest",
                Content = "Only one house found.",
                Type = NotificationType.Error
            });

            return;
        }

        if (!Utils.FindViaAtk(new Utils.FindAtkInput(7, 2, 15, "Estate Settings"), addonPtr, out var idx)) {
            return;
        }

        Log.Information($"Clicking Estate Settings button in {addonName} at index {idx}.");

        Callback.Fire(addonPtr, true, idx);

        TaskManager.EnqueueDelay(405);
    }

    private unsafe void HandleSelectHouse(AtkUnitBase* addonPtr)
    {
        const string addonName = "HandleSelectHouse";

        var values = addonPtr->AtkValuesSpan;

        if (!EzThrottler.Throttle("HandleSelectHouse", TimeSpan.FromSeconds(2))) {
            return;
        }

        if (values.Length < 6) {
            Log.Error($"Unexpected AtkValues in {addonName}.");
            return;
        }

        var length = values[3].Int;

        if (length < 4) {
            TaskManager.Abort();

            Svc.NotificationManager.AddNotification(new Notification
            {
                Title = "Housing Guest",
                Content = "Only one house found.",
                Type = NotificationType.Error
            });

            return;
        }

        if (!Utils.FindViaAtk(new Utils.FindAtkInput(7, 2, 3, "Private Chamber"), addonPtr, out var idx)) {
            return;
        }

        Log.Information($"Clicking Private Chambers button in {addonName} at index {idx}.");

        Callback.Fire(addonPtr, true, idx);

        TaskManager.EnqueueDelay(503);
    }

    public unsafe TaskManagerTask HousingMenuTask(bool openOrClose)
    {
        return new TaskManagerTask(() => {
            // If already on config, apply the requested action
            if (GameGui.GetAddonByName("HousingConfig").IsVisible) {
                var addon = GameGui.GetAddonByName("HousingConfig");
                if (addon == null || !addon.IsVisible) {
                    Log.Warning("HousingConfig addon not found or not visible.");
                    return false;
                }

                HandleHousingConfig(openOrClose, (AtkUnitBase*) addon.Address);
                return true;
            }

            if (GameGui.GetAddonByName("HousingSubmenu").IsVisible) {
                var addon = GameGui.GetAddonByName("HousingSubmenu");
                if (addon == null || !addon.IsVisible) {
                    Log.Warning("HousingSubmenu addon not found or not visible.");
                    return false;
                }

                HandleHousingSubmenu((AtkUnitBase*) addon.Address);

                return false;
            }

            if (GameGui.GetAddonByName("HousingSelectHouse").IsVisible) {
                var addon = GameGui.GetAddonByName("HousingSelectHouse");
                if (addon == null || !addon.IsVisible) {
                    Log.Warning("HousingSelectHouse addon not found or not visible.");
                    return false;
                }

                HandleSelectHouse((AtkUnitBase*) addon.Address);

                return false;
            }

            if (GameGui.GetAddonByName("HousingMenu").IsVisible) {
                var addon = GameGui.GetAddonByName("HousingMenu");
                if (addon == null || !addon.IsVisible) {
                    Log.Warning("HousingMenu addon not found or not visible.");
                    return false;
                }

                var addonPtr = (AtkUnitBase*) addon.Address;

                HandleHousingMenu(addonPtr);

                return false;
            }

            if (EzThrottler.Throttle("HousingMenuTask_OpenHousingMenu", TimeSpan.FromSeconds(1))) {
                Log.Information("Opening Housing Menu...");
                Chat.ExecuteCommand("/housing");
                return false;
            }

            return false;
        }, new TaskManagerConfiguration(showError: true, showDebug: true, abortOnTimeout: true, timeLimitMS: 12000));
    }

    private void OnCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args)) {
            _mainWindow.IsOpen = !_mainWindow.IsOpen;
            return;
        }

        switch (args.ToLowerInvariant()) {
            default:
                Svc.Chat.Print("Invalid argument. Usage: /guest [open|close]");
                return;
            case "open":
                StartGuestAction(true);
                break;
            case "close":
                StartGuestAction(false);
                break;
        }
    }

    private enum GuestAction
    {
        None,
        Open,
        Close
    }
}
