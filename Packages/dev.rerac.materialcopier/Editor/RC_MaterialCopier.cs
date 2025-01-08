using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public class MaterialCopier : EditorWindow
{
    private GameObject targetObject;
    private Dictionary<GameObject, List<Material>> objectMaterials;
    private Dictionary<GameObject, List<bool>> objectMaterialSelections;
    private bool copyTextures = false;
    private Vector2 scrollPos;
    private string targetPath;
    private string customSuffix = "copy";  // Default suffix is "copy"

    private List<String> copiedTemp = new List<String>();

    [MenuItem("ReraC/Material Copier")]
    static void Init()
    {
        MaterialCopier window = (MaterialCopier)EditorWindow.GetWindow(typeof(MaterialCopier));
        window.Show();
    }

    private static bool IsLiltoon(Material material)
    {
        return material.shader.name.Contains("lilToon");
    }

    private struct lilTex
    {
        public Texture tex;
        public Vector2 offset;
        public Vector2 scale;
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 25;
        GUILayout.Label("Material Copier.");
        GUI.skin.label.fontSize = 10;

        GUI.skin.label.alignment = TextAnchor.MiddleRight;
        GUILayout.Label("V1.5 by Rera*C");
        GUI.skin.label.alignment = TextAnchor.MiddleLeft;

        EditorGUILayout.Space(10);
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        if (targetObject == null)
        {
            return;
        }

        if (GUILayout.Button("Find Materials"))
        {
            FindMaterials();
            targetPath = "RC_MatCop/" + targetObject.name;
        }

        if (objectMaterials != null && objectMaterials.Count > 0)
        {
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);

            // Input field for the custom suffix
            customSuffix = EditorGUILayout.TextField("Custom Suffix", customSuffix);

            // Select/Deselect all materials button
            if (GUILayout.Button("Select All Materials"))
            {
                SetAllMaterialsSelection(true);
            }
            if (GUILayout.Button("Deselect All Materials"))
            {
                SetAllMaterialsSelection(false);
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var kvp in objectMaterials)
            {
                GameObject obj = kvp.Key;
                List<Material> materials = kvp.Value;
                List<bool> selections = objectMaterialSelections[obj];

                // Checkbox to select/deselect all materials for the GameObject
                EditorGUILayout.BeginHorizontal();
                bool allSelected = selections.TrueForAll(selected => selected);
                bool newAllSelected = EditorGUILayout.Toggle(allSelected, GUILayout.Width(20));
                EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                EditorGUILayout.EndHorizontal();

                if (newAllSelected != allSelected)
                {
                    for (int i = 0; i < selections.Count; i++)
                    {
                        selections[i] = newAllSelected;
                    }
                }

                // Add space (indent) for materials under the GameObject
                foreach (var material in materials)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20); // Add indentation for materials
                    selections[materials.IndexOf(material)] = EditorGUILayout.Toggle(selections[materials.IndexOf(material)], GUILayout.Width(20));
                    EditorGUILayout.ObjectField(material, typeof(Material), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            copyTextures = EditorGUILayout.Toggle("Copy Textures", copyTextures);
            targetPath = EditorGUILayout.TextField("Target Path", targetPath);
            if (GUILayout.Button("Copy Selected Materials"))
            {
                copiedTemp.Clear();
                CopySelectedMaterialsAndApply();
            }
        }
    }

    private void FindMaterials()
    {
        if (targetObject == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a target object.", "OK");
            return;
        }

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
        objectMaterials = new Dictionary<GameObject, List<Material>>();
        objectMaterialSelections = new Dictionary<GameObject, List<bool>>();

        foreach (Renderer renderer in renderers)
        {
            GameObject obj = renderer.gameObject;
            if (!objectMaterials.ContainsKey(obj))
            {
                objectMaterials[obj] = new List<Material>();
                objectMaterialSelections[obj] = new List<bool>();
            }

            foreach (Material mat in renderer.sharedMaterials)
            {
                if (!objectMaterials[obj].Contains(mat))
                {
                    objectMaterials[obj].Add(mat);
                    objectMaterialSelections[obj].Add(true);  // Default to selected
                }
            }
        }
    }

    private void CopySelectedMaterialsAndApply()
    {
        string assetFolderPath = "Assets/" + targetPath + "/Materials";
        if (!Directory.Exists(assetFolderPath))
        {
            Directory.CreateDirectory(assetFolderPath);
        }

        Dictionary<Material, Material> copiedMaterialMap = new Dictionary<Material, Material>();

        foreach (var kvp in objectMaterials)
        {
            GameObject obj = kvp.Key;
            List<Material> materials = kvp.Value;
            List<bool> selections = objectMaterialSelections[obj];

            for (int i = 0; i < materials.Count; i++)
            {
                if (selections[i])
                {
                    string path = AssetDatabase.GetAssetPath(materials[i]);
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string newFilePath = Path.Combine(assetFolderPath, fileName + "_" + customSuffix + ".mat");

                    Material newMat = new Material(materials[i]);
                    AssetDatabase.CreateAsset(newMat, newFilePath);
                    copiedMaterialMap[materials[i]] = newMat;

                    if (copyTextures)
                    {
                        CopyTextures(materials[i], newMat);
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ApplyCopiedMaterials(copiedMaterialMap);

        FindMaterials();

        EditorUtility.DisplayDialog("Materials Copied", "Selected materials have been copied and applied.", "OK");
    }

    private void CopyTextures(Material originalMat, Material newMat)
    {
        string assetFolderPath = "Assets/" + targetPath + "/Textures";
        if (!Directory.Exists(assetFolderPath))
        {
            Directory.CreateDirectory(assetFolderPath);
        }

        Shader shader = originalMat.shader;

        List<string> propertyDB = new List<string>();
        List<string> pathDB = new List<string>();

        for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                Texture texture = originalMat.GetTexture(propertyName);

                if (texture != null)
                {
                    string path = AssetDatabase.GetAssetPath(texture);
                    string fileName = Path.GetFileNameWithoutExtension(path);

                    string newFileName = fileName + "_" + customSuffix + Path.GetExtension(path);
                    string newFilePath = Path.Combine(assetFolderPath, newFileName);

                    if (!copiedTemp.Contains(path))
                    {
                        while (AssetDatabase.LoadAssetAtPath<Texture>(newFilePath) != null)
                        {
                            fileName = "_" + fileName;
                            newFilePath = Path.Combine(assetFolderPath, fileName + Path.GetExtension(path));
                        }

                        AssetDatabase.CopyAsset(path, newFilePath);
                        copiedTemp.Add(path);
                    }

                    pathDB.Add(newFilePath);
                    propertyDB.Add(propertyName);
                }
            }
        }

        AssetDatabase.Refresh();

        for (int i = 0; i < pathDB.Count; i++)
        {
            Texture newTexture = AssetDatabase.LoadAssetAtPath<Texture>(pathDB[i]);
            newMat.SetTexture(propertyDB[i], newTexture);
        }
    }


    private void ApplyCopiedMaterials(Dictionary<Material, Material> copiedMaterialMap)
    {
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material[] sharedMaterials = renderer.sharedMaterials;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (copiedMaterialMap.ContainsKey(sharedMaterials[i]))
                {
                    sharedMaterials[i] = copiedMaterialMap[sharedMaterials[i]];
                }
            }

            renderer.sharedMaterials = sharedMaterials;
        }
    }

    private void SetAllMaterialsSelection(bool selected)
    {
        foreach (var selections in objectMaterialSelections.Values)
        {
            for (int i = 0; i < selections.Count; i++)
            {
                selections[i] = selected;
            }
        }
    }
}
