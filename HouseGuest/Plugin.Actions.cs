using System;
using Dalamud.Game.NativeWrapper;
using Dalamud.Interface.ImGuiNotification;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace HouseGuest;

public sealed partial class Plugin
{
    internal static AtkUnitBasePtr? GetAddon(string name)
    {
        var addon = GameGui.GetAddonByName(name);

        return !addon.IsReady || !addon.IsVisible || addon.Address == IntPtr.Zero ? null : addon;
    }

    public static AtkUnitBasePtr? GetHousingSubmenuAddon() => GetAddon("HousingSubmenu");

    public static AtkUnitBasePtr? GetHousingSelectHouseAddon() => GetAddon("HousingSelectHouse");

    public static AtkUnitBasePtr? GetHousingMenuAddon() => GetAddon("HousingMenu");

    private static unsafe void HandleHousingSubmenu(AtkUnitBase* addonPtr)
    {
        const string addonName = "HousingSubmenu";

        var values = addonPtr->AtkValuesSpan;

        if (!EzThrottler.Throttle("HandleHousingSubmenu", TimeSpan.FromSeconds(2))) {
            return;
        }

        if (values.Length < 8) {
            Log.Error($"Unexpected AtkValues in {addonName}.");
            return;
        }

        var length = values[3].Int;

        if (length < 6) {
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

        Log.Debug($"Found {values.Length} AtkValues in {addonName}.");

        if (!Utils.FindViaAtk(new Utils.FindAtkInput(7, 2, 3, "Guest"), addonPtr, out var idx)) {
            return;
        }

        Log.Information($"Clicking Guest Access button in {addonName} at index {idx}.");

        Callback.Fire(addonPtr, true, idx);

        TaskManager.EnqueueDelay(507);
    }

    private static unsafe void HandleHousingConfig(bool openOrClose, AtkUnitBase* addonPtr)
    {
        var current = addonPtr->AtkValuesSpan;

        if (current.Length < 6) {
            Log.Error("Unexpected AtkValues in HousingConfig.");
            return;
        }

        var str = current[5];

        if (str.Type != ValueType.String && str.Type != ValueType.String8 && str.Type != ValueType.ManagedString) {
            Log.Error($"Unexpected AtkValue type {str.Type} in HousingConfig.");
            return;
        }

        if (!str.ToString().Contains("Allow", StringComparison.OrdinalIgnoreCase)) {
            Log.Error($"Unexpected AtkValue string '{str}' in HousingConfig.");
            return;
        }

        Log.Information($"Clicking {(openOrClose ? "Open" : "Close")} Guest Access button in HousingConfig.");

        Callback.Fire(addonPtr, true, 0, openOrClose ? 1 : 0);

        Svc.NotificationManager.AddNotification(new Notification
        {
            Title = "Housing Guest",
            Content = $"{(openOrClose ? "Opened" : "Closed")} Guest Access.",
            Type = NotificationType.Success
        });

        TaskManager.EnqueueDelay(101);

        TaskManager.BeginStack();

        TaskManager.EnqueueTask(new TaskManagerTask(() => {
            if (GetHousingSubmenuAddon() is {} submenuAddon) {
                if (EzThrottler.Throttle("close submenu", 250)) {
                    var addon = (AtkUnitBase*) submenuAddon.Address;
                    Callback.Fire(addon, true, -32);
                } else {
                    return false;
                }

                return true;
            }

            return true;
        }, "Close Sub menu", new TaskManagerConfiguration(5000)));

        TaskManager.EnqueueDelay(301);

        TaskManager.EnqueueTask(new TaskManagerTask(() => {
            if (GetHousingSelectHouseAddon() is {} selectHouseAddon) {
                if (EzThrottler.Throttle("close select house")) {
                    var addon = (AtkUnitBase*) selectHouseAddon.Address;
                    Callback.Fire(addon, false, -2);
                    Callback.Fire(addon, true, -32);
                } else {
                    return false;
                }

                return true;
            }

            return true;
        }, "Close select menu", new TaskManagerConfiguration(5000)));

        TaskManager.EnqueueDelay(302);

        TaskManager.EnqueueTask(new TaskManagerTask(() => {
            if (GetHousingMenuAddon() is {} housingMenuAddon) {
                if (EzThrottler.Throttle("close house menu")) {
                    var addon = (AtkUnitBase*) housingMenuAddon.Address;
                    Callback.Fire(addon, true, -32);
                }

                return true;
            }

            return true;
        }, "Close HouseMenu", new TaskManagerConfiguration(5000)));

        TaskManager.EnqueueStack();

        TaskManager.EnqueueDelay(30);
    }
}
