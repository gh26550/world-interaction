using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace WorldInteraction.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("World Interaction/Create Demo Scene")]
        public static void CreateDemo()
        {
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var boot=new GameObject("World Interaction Demo").AddComponent<DemoBootstrap>();boot.gaussianShader=Shader.Find("WorldInteraction/AnisotropicGaussian");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/InteractionLab.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/InteractionLab.unity",true)};
            PlayerSettings.companyName="WorldInteractionResearch";PlayerSettings.productName="World Interaction";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"org.worldinteraction.research");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.gpuSkinning=true;PlayerSettings.MTRendering=true;PlayerSettings.enableFrameTimingStats=true;
            var player=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            player.FindProperty("activeInputHandler").intValue=1;player.ApplyModifiedPropertiesWithoutUndo();
            ConfigureXR(BuildTargetGroup.Standalone);ConfigureXR(BuildTargetGroup.Android);
            AssetDatabase.SaveAssets();Debug.Log("WORLD_INTERACTION_SETUP_OK");
        }
        static void ConfigureXR(BuildTargetGroup target)
        {
            var per=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(target);
            if(per==null)
            {
                XRGeneralSettingsPerBuildTarget settings;
                if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out settings))
                {
                    settings=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();Directory.CreateDirectory("Assets/XR");AssetDatabase.CreateAsset(settings,"Assets/XR/XRGeneralSettings.asset");EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,settings,true);
                }
                settings.CreateDefaultSettingsForBuildTarget(target);per=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(target);
            }
            if(per.Manager==null){var manager=ScriptableObject.CreateInstance<XRManagerSettings>();AssetDatabase.AddObjectToAsset(manager,AssetDatabase.GetAssetPath(per));per.Manager=manager;}
            XRPackageMetadataStore.AssignLoader(per.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",target);
            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(target);
            var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(target);
            if(xr!=null)
            {
                xr.renderMode=OpenXRSettings.RenderMode.MultiPass;
                var touch=xr.GetFeature<OculusTouchControllerProfile>();if(!touch)throw new Exception("Oculus Touch feature not found");touch.enabled=true;EditorUtility.SetDirty(touch);
                foreach(var feature in xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                    if((target==BuildTargetGroup.Android&&feature.GetType().Name=="MetaQuestFeature")||feature.GetType().Name=="MetaQuestTouchProControllerProfile"){feature.enabled=true;EditorUtility.SetDirty(feature);}
                EditorUtility.SetDirty(xr);
            }
            EditorUtility.SetDirty(per);EditorUtility.SetDirty(per.Manager);
        }
        public static void BuildWindows()
        {
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/Windows/WorldInteraction.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed");
        }
        [MenuItem("World Interaction/Build Quest APK")]
        public static void BuildQuest()
        {
            ConfigureXR(BuildTargetGroup.Android);AssetDatabase.SaveAssets();
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/Android/WorldInteraction.apk",BuildTarget.Android,BuildOptions.Development);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Quest build failed");
        }
    }
}
