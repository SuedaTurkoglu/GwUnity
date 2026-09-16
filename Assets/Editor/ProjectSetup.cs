using UnityEngine;
using UnityEditor;

namespace Gwent.Editor
{
    public class ProjectSetup : EditorWindow
    {
        [MenuItem("Tools/Gwent Setup/Set Package Name")]
        public static void SetPackageName()
        {
            string packageName = "com.mst29.gwentmobile";
            PlayerSettings.applicationIdentifier = packageName;
            Debug.Log($"Project Package Name has been set to: {packageName}");
        }

        [MenuItem("Tools/Gwent Setup/Check Package Name")]
        public static void CheckPackageName()
        {
            Debug.Log($"Current Project Package Name: {PlayerSettings.applicationIdentifier}");
        }
    }
}
