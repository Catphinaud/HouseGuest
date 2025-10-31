using System;
using System.Collections.Generic;
using Dalamud.Game.NativeWrapper;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using VT = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace HouseGuest;

public static unsafe class Utils
{
    public static bool FindViaAtk(FindAtkInput input, AtkUnitBase* addonPtr, out int idx)
    {
        var start = input.Start;
        var min = input.Min;
        var max = input.Max;
        var find = input.Find;
        idx = 0;
        var values = addonPtr->AtkValuesSpan;

        if (values.Length < 10) {
            Svc.Log.Error("Unexpected AtkValues in FindViaAtk.");
            return false;
        }

        if (start >= values.Length) {
            Svc.Log.Error($"Start index {start} is out of bounds in FindViaAtk.");
            return false;
        }

        if (min < 0 || max < 0 || min > max) {
            Svc.Log.Error($"Invalid min/max values {min}/{max} in FindViaAtk.");
            return false;
        }

        Svc.Log.Debug($"Found {values.Length} AtkValues in FindViaAtk.");

        var found = false;
        for (var i = start; i < values.Length; i++) {
            var val = values[i].ToString();
            Svc.Log.Debug($"Value at index {i}: {val} (Type: {values[i].Type})");

            if (val.Contains(find, StringComparison.OrdinalIgnoreCase)) {
                found = true;
                break;
            }

            idx++;
        }

        if (!found || idx == 0) {
            Svc.Log.Warning($"Could not find {find} button in FindViaAtk.");
            return false;
        }


        if (idx < min || idx > max) {
            Svc.Log.Error($"{find} button index {idx} seems too high in FindViaAtk.");
            return false;
        }

        return true;
    }

    public static bool ValidateAddon(string title,
        AtkUnitBasePtr addonPtr,
        int expectedMinValues,
        string? findValue,
        out Dictionary<int, string>? foundValues,
        out int? foundIndex)
    {
        if (addonPtr.Address == IntPtr.Zero) {
            Svc.Log.Error($"Addon {title} pointer is null.");
            foundValues = null;
            foundIndex = null;
            return false;
        }

        return ValidateAddon(title, (AtkUnitBase*) addonPtr.Address, expectedMinValues, findValue, out foundValues, out foundIndex);
    }

    public static bool ValidateAddon(string title,
        AtkUnitBase* addonPtr,
        int expectedMinValues,
        string? findValue,
        out Dictionary<int, string>? foundValues,
        out int? foundIndex)
    {
        foundValues = new Dictionary<int, string>();
        foundIndex = null;

        var values = addonPtr->AtkValuesSpan;

        if (values.Length < expectedMinValues) {
            Svc.Log.Error($"Unexpected AtkValues in {title}. Expected at least {expectedMinValues}, found {values.Length}.");
            return false;
        }

        // First should be the addon name

        var firstValue = values[0];

        if (firstValue.Type is not (VT.String or VT.ManagedString or VT.String8)) {
            Svc.Log.Error($"First AtkValue in {title} is not a string as expected.");
            return false;
        }

        var firstValueStr = firstValue.String.ToString();

        if (!firstValueStr.Equals(title)) {
            Svc.Log.Warning($"First AtkValue in {title} is '{firstValueStr}', expected '{title}'.");
            return false;
        }

        var listLength = values[3].Type == VT.Int ? values[3].Int : -1;

        if (listLength == -1) {
            Svc.Log.Error($"Fourth AtkValue in {title} is not a valid integer.");
            return false;
        }

        if (values.Length < 7) {
            Svc.Log.Error($"Not enough AtkValues in {title} to validate strings: found {values.Length}, need at least 7.");
            return false;
        }

        var maxIndexToCheck = 7 + listLength;

        if (maxIndexToCheck > 30) {
            Svc.Log.Error($"List length {listLength} in {title} is too large, exceeds maximum allowed.");
            return false;
        }

        if (values.Length < maxIndexToCheck) {
            Svc.Log.Error($"Not enough AtkValues in {title} to validate strings: found {values.Length}, need at least {maxIndexToCheck} for all lists.");
            return false;
        }

        for (var i = 7; i < maxIndexToCheck; i++) {
            var val = values[i];
            if (val.Type is not (VT.String or VT.ManagedString or VT.String8)) {
                Svc.Log.Error($"AtkValue at index {i} in {title} is not a string as expected.");
                break;
            }

            var valStr = val.String.ToString();
            foundValues[i - 7] = valStr;

            Svc.Log.Debug($"Value at index {i} ({i - 7} index): {valStr}");

            if (findValue != null && valStr.Equals(findValue, StringComparison.OrdinalIgnoreCase)) {
                foundIndex = i - 7;
            }
        }

        return true;
    }

    public record struct FindAtkInput(int Start, int Min, int Max, string Find);
}
