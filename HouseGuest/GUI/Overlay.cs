using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;

namespace HouseGuest.GUI;

// Temporary borrowed from Lifestream
public class ProgressOverlay : Window
{
    public ProgressOverlay() : base("HouseGuest Progress Overlay",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize, true)
    {
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = ImGuiHelpers.MainViewport.Size with { Y = 0 },
            MaximumSize = new Vector2(0, float.MaxValue)
        };
    }

    public override void Draw()
    {
        CImGui.igBringWindowToDisplayBack(CImGui.igGetCurrentWindow());
        if (ImGui.IsWindowHovered()) {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("Right click to stop all tasks and movement");
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
                Plugin.TaskManager.Abort();
            }
        }

        var percent = 1f - Plugin.TaskManager.NumQueuedTasks / (float) Plugin.TaskManager.MaxTasks;
        var col = EColor.Violet;
        var overlay = $"Progress: {Plugin.TaskManager.MaxTasks - Plugin.TaskManager.NumQueuedTasks}/{Plugin.TaskManager.MaxTasks}";

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
        ImGui.ProgressBar(percent, new Vector2(ImGui.GetContentRegionAvail().X, 20), overlay);
        ImGui.PopStyleColor();
        // Toggle ProgressOverlay position logic
        Position = new Vector2(0, ImGuiHelpers.MainViewport.Size.Y - ImGui.GetWindowSize().Y);
    }

    public override bool DrawConditions()
        //return ((Plugin.TaskManager.IsBusy && Plugin.TaskManager.MaxTasks > 0)) && !C.NoProgressBar;
        => Plugin.TaskManager.IsBusy && Plugin.TaskManager.MaxTasks > 0;
}
