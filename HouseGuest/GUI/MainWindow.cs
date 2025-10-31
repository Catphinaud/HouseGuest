using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HouseGuest.GUI;

public class MainWindow : Window
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("HouseGuest")
    {
        this.plugin = plugin;
        RespectCloseHotkey = true;
        IsOpen = false;
    }

    public override void Draw()
    {
        ImGui.Text(Plugin.TaskManager.IsBusy ? "Status: Running" : "Status: Idle");

        var current = Plugin.GetHouseGuestState();

        ImGui.Text($"Current State: {current:G}");

        if (ImGui.Button("Open Guest Access")) {
            plugin.StartGuestAction(true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Close Guest Access")) {
            plugin.StartGuestAction(false);
        }

        if (Plugin.TaskManager.IsBusy) {
            ImGui.Text("Current task: " + Plugin.TaskManager.CurrentTask?.Name);

            if (ImGui.Button("Abort")) {
                Plugin.TaskManager.Abort();
            }
        }
    }
}
