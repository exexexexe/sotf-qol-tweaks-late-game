using RedLoader;
using SonsSdk;
using SUI;

namespace TrapResetSwitch;

public class TrapResetSwitch : SonsMod
{
    public TrapResetSwitch()
    {
        // Applies the switch patches in SwitchPatches.cs
        HarmonyPatchAll = true;
    }

    protected override void OnInitializeMod()
    {
        Config.Init();
    }

    protected override void OnSdkInitialized()
    {
        SettingsRegistry.CreateSettings(this, null, typeof(Config));
    }

    protected override void OnGameStart()
    {
        var patched = HarmonyLib.Harmony.GetAllPatchedMethods()
            .Where(m => m.DeclaringType?.Name.Contains("Switch") == true)
            .Select(m => $"{m.DeclaringType.Name}.{m.Name}");
        RLog.Msg($"Switch hooks: {string.Join(", ", patched)}");
    }
}
