#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PeopleFlow.EditorTools
{
    /// <summary>
    /// Editor menu that writes the built-in <see cref="DefaultLevels"/> out as LevelData .asset
    /// files so designers can tweak them in the Inspector. The game itself does not need these
    /// assets (it falls back to the code-defined levels), but they make iteration easy.
    /// </summary>
    public static class SampleLevelGenerator
    {
        const string FolderRel = "Assets/PeopleFlow/Levels";

        [MenuItem("PeopleFlow/Generate Sample Levels")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PeopleFlow"))
                AssetDatabase.CreateFolder("Assets", "PeopleFlow");
            if (!AssetDatabase.IsValidFolder(FolderRel))
                AssetDatabase.CreateFolder("Assets/PeopleFlow", "Levels");

            for (int i = 0; i < DefaultLevels.Count; i++)
            {
                var level = DefaultLevels.Get(i);
                string path = Path.Combine(FolderRel, $"Level_{i + 1:00}.asset").Replace('\\', '/');

                var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(level, existing);
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(level);
                }
                else
                {
                    AssetDatabase.CreateAsset(level, path);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PeopleFlow] Generated {DefaultLevels.Count} sample levels in {FolderRel}.");
        }
    }
}
#endif
