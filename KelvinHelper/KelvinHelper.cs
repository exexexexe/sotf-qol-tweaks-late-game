using SonsSdk;
using SUI;

namespace KelvinHelper;

public class KelvinHelper : SonsMod
{
    public KelvinHelper()
    {
        HarmonyPatchAll = true;
        OnUpdateCallback = KelvinWatcher.OnUpdate;
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
