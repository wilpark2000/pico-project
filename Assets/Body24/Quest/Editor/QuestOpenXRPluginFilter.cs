using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Body24.Editor
{
    /// <summary>
    /// Both SDKs ship libopenxr_loader.so. A Quest build must package Unity's
    /// loader only. This filter does not modify package files or import settings.
    /// </summary>
    public sealed class QuestOpenXRPluginFilter : IPreprocessBuildWithReport
    {
        // Reapply after SDK build processors register their own inclusion rules.
        public int callbackOrder => int.MaxValue;

        [InitializeOnLoadMethod]
        static void Initialize() => Register();

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Register();
        }

        public static int Register()
        {
            int count = 0;
            foreach (var plugin in PluginImporter.GetAllImporters())
            {
                if (string.Equals(plugin.assetPath,
                    "Packages/com.unity.xr.openxr/RuntimeLoaders/android/openxr_loader.aar",
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Native PICO uses its own loader. Evaluate the current target
                    // instead of retaining a delegate captured by an earlier Quest build.
                    plugin.SetIncludeInBuildDelegate(_ => UseUnityOpenXRLoader());
                    count++;
                    continue;
                }
                if (!string.Equals(plugin.assetPath,
                    "Packages/com.bytedance.pico.xr/Runtime/Android/LoaderForUnitySDK.aar",
                    StringComparison.OrdinalIgnoreCase)) continue;
                plugin.SetIncludeInBuildDelegate(_ => IncludeForCurrentConfiguration());
                count++;
            }
            return count;
        }

        public static bool UseUnityOpenXRLoader()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            return general && general.Manager && general.Manager.activeLoaders.Any(l => l is OpenXRLoader);
        }

        public static bool IncludeForCurrentConfiguration()
        {
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var loaders = general ? general.Manager : null;
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            bool openXR = loaders && loaders.activeLoaders.Any(l => l is OpenXRLoader);
            bool pico = loaders && loaders.activeLoaders.Any(l => l && l.GetType().FullName == "ByteDance.PICO.XR.PXR_Loader");
            // Retain the SDK's loader for PICO OpenXR configurations, too.
            if (settings)
                pico |= settings.GetFeatures<OpenXRFeature>().Any(f => f && f.enabled &&
                    (f.GetType().Namespace ?? "").StartsWith("ByteDance.PICO", StringComparison.Ordinal));
            var quest = settings ? settings.GetFeature<MetaQuestFeature>() : null;
            return ShouldInclude(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                openXR, quest && quest.enabled, pico);
        }

        public static bool ShouldInclude(bool android, bool openXR, bool quest, bool pico)
            => !(android && openXR && quest && !pico);
    }
}
