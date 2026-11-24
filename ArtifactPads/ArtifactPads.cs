using SonsSdk;
using SUI;

namespace ArtifactPads;

public class ArtifactPads : SonsMod
{
    public ArtifactPads()
    {
        OnUpdateCallback = PadInput.OnUpdate;
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
        PadStore.Load();
    }
}
