using SonsSdk;
using SUI;

namespace CollectiblesTracker;

public class CollectiblesTracker : SonsMod
{
    public CollectiblesTracker()
    {
        OnUpdateCallback = OnUpdate;
        OnGUICallback = Panel.OnGUI;
    }

    protected override void OnInitializeMod()
    {
        Config.Init();
    }

    protected override void OnSdkInitialized()
    {
        SettingsRegistry.CreateSettings(this, null, typeof(Config), callback: Config.OnSettingsUiClosed);
    }

    protected override void OnGameStart()
    {
        Progress.Reset();
        MapMarkers.Reset();
    }

    protected override void OnSonsSceneInitialized(ESonsScene sonsScene)
    {
        if (sonsScene == ESonsScene.Title)
        {
            Progress.Reset();
            MapMarkers.Reset();
            Panel.Visible = false;
            BigMap.Visible = false;
        }
    }

    private static void OnUpdate()
    {
        Panel.HandleInput();
        Progress.Tick();
        MapMarkers.Tick();
    }
}
