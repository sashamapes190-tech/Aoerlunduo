using System;
using System.IO;
using Aoerlunduo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AoBuild
{
    [MenuItem("奥尔伦多/创建初版场景")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Game/Scenes");
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(previous.isDirty){Directory.CreateDirectory("Assets/Game/Backups");EditorSceneManager.SaveScene(previous,"Assets/Game/Backups/BeforePrototype-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color(.035f,.065f,.10f);
        new GameObject("Aoerlunduo",typeof(AoGame));
        EditorSceneManager.SaveScene(scene,"Assets/Game/Scenes/Aoerlunduo.unity");
        var existing=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if(!existing.Exists(s=>s.path==scene.path))existing.Add(new EditorBuildSettingsScene(scene.path,true));
        EditorBuildSettings.scenes=existing.ToArray();
        PlayerSettings.productName="奥尔伦多";PlayerSettings.companyName="Aoerlunduo";PlayerSettings.bundleVersion="0.1.0";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.defaultIsNativeResolution=false;
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;PlayerSettings.runInBackground=true;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.aoerlunduo.game");
        AssetDatabase.SaveAssets();Debug.Log("AO_SETUP_OK: Assets/Game/Scenes/Aoerlunduo.unity");
    }
    [MenuItem("奥尔伦多/验证基础玩法")]
    public static string SmokeTest()
    {
        int assertions=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("AO_TEST_FAILED: "+name);assertions++;};
        using(var m=new AoSimulation())
        {
            check(m.State.nations.Count==28,"country count");check(m.State.year==2136,"epoch");check(m.Select(3),"select player");check(!m.Select(0),"cannot reselect");
            long before=m.State.nations[3].treasury;check(!m.Build(0,false),"reject foreign build");check(before==m.State.nations[3].treasury,"rejected action preserves money");
            check(m.Build(3,false),"farm");check(m.State.nations[3].farms==2,"farm count");check(m.Recruit(3),"recruit");check(m.State.nations[3].troops==13,"troop count");
            check(!m.March(3,4),"reject ocean crossing");check(!m.March(3,0),"reject undeclared attack");check(m.Diplomacy(0),"declare war");check(m.March(3,0),"attack neighbor");check(m.State.nations[0].owner==3,"occupation");
            m.Tick();check(m.State.month==2,"advance month");
            var loaded=JsonUtility.FromJson<Campaign>(JsonUtility.ToJson(m.State));check(AoSimulation.Validate(loaded),"save round trip");check(loaded.nations[0].owner==3,"occupation persists");
            loaded.nations[0].owner=99;check(!AoSimulation.Validate(loaded),"reject corrupt save");
            m.State.pendingEvent=0;int month=m.State.month;m.Tick();check(m.State.month==month,"event pauses time");m.ChooseEvent(false);check(m.State.pendingEvent==-1,"resolve event");
        }
        string result="AO_TESTS_PASSED: "+assertions;Debug.Log(result);return result;
    }
    [MenuItem("奥尔伦多/构建 Windows 试玩版")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Game/Scenes/Aoerlunduo.unity"},"Builds/Windows/Aoerlunduo.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception(report.summary.result.ToString());
        Debug.Log("AO_WINDOWS_BUILD_OK");
    }
    [MenuItem("奥尔伦多/构建 Android APK")]
    public static void BuildAndroid()
    {
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("请先安装 Android Build Support / SDK / NDK / OpenJDK。");
        Directory.CreateDirectory("Builds/Android");PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;EditorUserBuildSettings.buildAppBundle=false;
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Game/Scenes/Aoerlunduo.unity"},"Builds/Android/Aoerlunduo-0.1.apk",BuildTarget.Android,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception(report.summary.result.ToString());
        Debug.Log("AO_ANDROID_BUILD_OK");
    }
}
