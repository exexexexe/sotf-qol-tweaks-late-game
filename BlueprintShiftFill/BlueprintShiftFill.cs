using SonsSdk;
using SUI;

namespace BlueprintShiftFill;

public class BlueprintShiftFill : SonsMod
{
    public BlueprintShiftFill()
    {
        HarmonyPatchAll = true;
        OnUpdateCallback = () =>
        {
            ShiftFill.OnUpdate();
            ShiftTransfer.OnUpdate();
        };
    }

    protected override void OnInitializeMod()
    {
        Config.Init();
    }

    protected override void OnSdkInitialized()
    {
        SettingsRegistry.CreateSettings(this, null, typeof(Config));
    }
}
