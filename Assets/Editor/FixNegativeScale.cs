using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FixNegativeScale : EditorWindow
{
    [MenuItem("Tools/Fix Negative BoxCollider Scale")]
    static void ShowWindow()
    {
        GetWindow<FixNegativeScale>("Fix Negative BoxCollider Scale");
    }

    void OnGUI()
    {
        if (GUILayout.Button("Fix All Negative BoxCollider Scales"))
        {
            FixAllNegativeBoxColliderScales();
        }
    }

    static void FixAllNegativeBoxColliderScales()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    var collider = t.GetComponent<BoxCollider>();
                    if (collider != null)
                    {
                        Transform current = t;
                        while (current != null)
                        {
                            Vector3 scale = current.localScale;
                            bool hasNegative = scale.x < 0 || scale.y < 0 || scale.z < 0;
                            if (hasNegative)
                            {
                                scale.x = Mathf.Abs(scale.x);
                                scale.y = Mathf.Abs(scale.y);
                                scale.z = Mathf.Abs(scale.z);
                                Undo.RecordObject(current, "Fix Negative Scale");
                                current.localScale = scale;
                                Debug.Log($"Fixed negative scale on: {GetHierarchyPath(current)}");
                                Debug.Log(current.gameObject);
                            }
                            current = current.parent;
                        }
                    }
                }
            }
        }
    }

    static string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
