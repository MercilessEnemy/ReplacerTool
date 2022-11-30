using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

//using Unity.VisualScripting;
//using AmplifyImpostors;

public class ReplacerTool : EditorWindow
{
    [MenuItem("Tools/Replacer Tool")]
    private static void Init()
    {
        const float width = 300f;
        const float height = 800f;

        GetWindow<ReplacerTool>().minSize = new Vector2(width, height);
    }

    [SerializeField] private Object prefab;
    [SerializeField] private Vector3 newPosition;
    [SerializeField] private Vector3 newRotation;
    [SerializeField] private Vector3 newScale;
    [SerializeField] private int selectedLOD;
    [SerializeField] private List<float> lods;
    [SerializeField] private bool isHDRP;
    [SerializeField] private Shader sourceShader;
    [SerializeField] private Shader targetShader;

    private LayerMask newLayer;
    private bool staticEnabled;
    private List<LayerMask> layers = new List<LayerMask>();
    private Vector3 position;
    private Vector3 rotation;
    private Vector3 scale;
    private LODGroup lodGroup;
    private List<Material> materials = new List<Material>();
    private List<string> assetPaths = new List<string>();

    private void OnEnable()
    {
        materials.Clear();
        assetPaths.Clear();
    }

    private void OnGUI()
    {
        var previousPrefab = prefab;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Prefab:");
        GUILayout.FlexibleSpace();
        prefab = EditorGUILayout.ObjectField(prefab, typeof(Object), true);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Layer:");
        GUILayout.FlexibleSpace();
        newLayer = EditorGUILayout.LayerField(newLayer);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Position:");
        GUILayout.FlexibleSpace();
        EditorGUILayout.Vector3Field("", newPosition);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Rotation:");
        GUILayout.FlexibleSpace();
        EditorGUILayout.Vector3Field("", newRotation);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Scale:");
        GUILayout.FlexibleSpace();
        EditorGUILayout.Vector3Field("", newScale);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Selected LOD:");
        GUILayout.FlexibleSpace();
        selectedLOD = EditorGUILayout.IntField("", selectedLOD);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("LODs:");
        GUILayout.FlexibleSpace();

        EditorGUILayout.Space();

        ScriptableObject target = this;
        var so = new SerializedObject(target);
        var stringsProperty = so.FindProperty("lods");
        EditorGUILayout.PropertyField(stringsProperty, true);
        GUILayout.EndHorizontal();
        so.ApplyModifiedProperties();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Source Shader:");
        GUILayout.FlexibleSpace();
        sourceShader = EditorGUILayout.ObjectField(sourceShader, typeof(Shader), false) as Shader;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Target Shader:");
        GUILayout.FlexibleSpace();
        targetShader = EditorGUILayout.ObjectField(targetShader, typeof(Shader), false) as Shader;
        GUILayout.EndHorizontal();

        if (previousPrefab != prefab)
        {
            materials.Clear();
            assetPaths.Clear();

            GetLayer();
            GetPosition();
            GetRotation();
            GetScale();
            GetLODGroup();
            GetMaterials();
        }

        if (GUILayout.Button("Find"))
        {
            GetLayer();
            GetPosition();
            GetRotation();
            GetScale();
            GetLODGroup();
            GetMaterials();
        }

        EditorGUILayout.Space();
        var style = new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold};
        EditorGUILayout.LabelField("Results", style, GUILayout.ExpandWidth(true));


        if (prefab != null)
        {
            var text = (staticEnabled) ? "Enable Static" : "Disable Static";
            if (GUILayout.Button(text))
            {
                SetStatic();
            }

            for (var i = 0; i < layers.Count; ++i)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Layer:");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(LayerMask.LayerToName(layers[0].value));
                }
                GUILayout.EndHorizontal();
            }

            if (layers.Count > 0)
            {
                if (GUILayout.Button("Update Layer"))
                {
                    SetLayer();
                }
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Position:");
                GUILayout.FlexibleSpace();
                GUILayout.Label(position.ToString());
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Update Position"))
            {
                UpdatePosition();
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Rotation:");
                GUILayout.FlexibleSpace();
                GUILayout.Label(rotation.ToString());
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Update Rotation"))
            {
                UpdateRotation();
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Scale:");
                GUILayout.FlexibleSpace();
                GUILayout.Label(scale.ToString());
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Update Scale"))
            {
                UpdateScale();
            }

            if (lodGroup != null)
            {
                for (var i = 0; i < lodGroup.lodCount; ++i)
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("LOD" + i + ":");
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(lodGroup.GetLODs()[i].screenRelativeTransitionHeight.ToString());
                    }
                    GUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Replace LODs"))
                {
                    ReplaceLods();
                }
            }

            if (GUILayout.Button("Save as terrain detail prefab"))
            {
                CreateDetailPrefab();
            }

            if (GUILayout.Button("Add Impostor"))
            {
                AddImpostor();
            }
        }

        for (var i = 0; i < materials.Count; ++i)
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(Path.GetFileNameWithoutExtension(materials[i].name));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Show"))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(assetPaths[i], typeof(Material)));
                }
            }
            GUILayout.EndHorizontal();
        }

        if (materials.Count > 0 && targetShader != null)
        {
            if (GUILayout.Button("Replace Shader"))
            {
                ReplaceShader();
            }
        }
    }

    private void SetStatic()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            go.isStatic = staticEnabled;

            var transforms = go.GetComponentsInChildren<Transform>();

            for (var i = 0; i < transforms.Length; ++i)
            {
                transforms[i].gameObject.isStatic = staticEnabled;
            }

            GetLayer();

            staticEnabled = !staticEnabled;
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void GetLayer()
    {
        if (prefab == null)
            return;

        layers.Clear();

        try
        {
            var go = (GameObject) prefab;

            layers.Add(go.layer);

            var transforms = go.GetComponentsInChildren<Transform>();

            for (var i = 0; i < transforms.Length; ++i)
            {
                layers.Add(transforms[i].gameObject.layer);
            }
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void SetLayer()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            go.layer = newLayer;

            var transforms = go.GetComponentsInChildren<Transform>();

            for (var i = 0; i < transforms.Length; ++i)
            {
                transforms[i].gameObject.layer = newLayer;
            }

            GetLayer();
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void GetPosition()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            position = go.transform.position;
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void UpdatePosition()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            go.transform.position = newPosition;

            GetPosition();
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void GetRotation()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            rotation = go.transform.localEulerAngles;
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void UpdateRotation()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            go.transform.localEulerAngles = newRotation;

            GetRotation();
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void GetScale()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            scale = go.transform.localScale;
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void UpdateScale()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            go.transform.localScale = newScale;

            GetScale();
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void GetLODGroup()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            lodGroup = go.GetComponent<LODGroup>();
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void ReplaceLods()
    {
        if (prefab == null)
            return;

        try
        {
            var go = (GameObject) prefab;
            lodGroup = go.GetComponent<LODGroup>();

            if (lodGroup == null)
                return;

            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            var newLods = new LOD[lods.Count];
            var renderers = go.GetComponentsInChildren<Renderer>();

            for (var i = 0; i < lods.Count; ++i)
            {
                var lod = new LOD(lods[i], renderers);
                newLods[i] = lod;
            }

            lodGroup.SetLODs(newLods);
            lodGroup.RecalculateBounds();

            GetLODGroup();
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }
    }

    private void AddImpostor()
    {
        if (prefab == null)
            return;

        Debug.Log("Requires Amplify Impostor and code uncommented");

        /*try
        {
            var go = (GameObject) prefab;
            AmplifyImpostor ai = go.GetComponent<AmplifyImpostor>();

            if (ai == null)
            {
                go.AddComponent<AmplifyImpostor>();
            }
        }
        catch
        {
            Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
        }*/
    }

    private void GetMaterials()
    {
        materials.Clear();
        assetPaths.Clear();

        if (prefab != null && sourceShader != null)
        {
            try
            {
                var go = (GameObject) prefab;
                var meshRenderer = go.GetComponent<MeshRenderer>();

                if (meshRenderer == null)
                {
                    meshRenderer = go.GetComponentInChildren<MeshRenderer>();
                }

                var allMaterials = new List<Material>();

                meshRenderer.GetSharedMaterials(allMaterials);

                if (allMaterials.Count > 0)
                {
                    for (var i = 0; i < allMaterials.Count; ++i)
                    {
                        if (allMaterials[i].shader == sourceShader)
                        {
                            materials.Add(allMaterials[i]);
                            assetPaths.Add(AssetDatabase.GetAssetPath(materials[i]));
                        }
                    }
                }
                else
                {
                    Debug.Log(allMaterials.Count + " materials found");
                }
            }
            catch
            {
                Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
            }
        }
    }

    private void ReplaceShader()
    {
        Color baseColor;
        Texture baseMap;
        Texture normalMap;
        Texture maskMap;

        for (var i = 0; i < materials.Count; ++i)
        {
            if (isHDRP)
            {
                baseColor = materials[i].GetColor("_MainColor");
                baseMap = materials[i].GetTexture("_MainTex");
                normalMap = materials[i].GetTexture("_BumpMap");
                maskMap = materials[i].GetTexture("_ExtraTex");
            }
            else
            {
                baseColor = materials[i].GetColor("_MainColor");
                baseMap = materials[i].GetTexture("_MainAlbedoTex");
                normalMap = materials[i].GetTexture("_MainNormalTex");
                maskMap = materials[i].GetTexture("_MainMaskTex");
            }

            materials[i].shader = targetShader;

            materials[i].SetColor("_BaseColor", baseColor);

            if (baseMap != null)
            {
                materials[i].SetTexture("_BaseColorMap", baseMap);
            }

            if (normalMap != null)
            {
                materials[i].SetTexture("_NormalMap", normalMap);
            }

            if (maskMap != null)
            {
                materials[i].SetTexture("_MaskMap", maskMap);
            }
        }

        materials.Clear();
    }

    private void CreateDetailPrefab()
    {
        var prefabGo = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        if (prefabGo == null)
            return;

        lodGroup = prefabGo.GetComponent<LODGroup>();

        if (lodGroup == null)
        {
            DestroyImmediate(prefabGo);
            return;
        }

        var go = new GameObject
        {
            name = prefabGo.name
        };
        var lodTransform = lodGroup.transform;

        foreach (Transform child in lodTransform)
        {
            var renderer = child.GetComponent<Renderer>();
            if (renderer != null && selectedLOD == child.GetSiblingIndex())
            {
                Debug.Log(child.gameObject);
                DestroyImmediate(go);
                go = Instantiate(child.gameObject);
                go.name = prefabGo.name;
                break;
            }
        }

        if (go == null)
        {
            DestroyImmediate(prefabGo);
            return;
        }

        if (!Directory.Exists("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        var localPath = "Assets/Prefabs/" + go.name + ".prefab";

        // Make sure the file name is unique, in case an existing Prefab has the same name.
        localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);

        PrefabUtility.SaveAsPrefabAsset(go, localPath, out var prefabSuccess);
        if (prefabSuccess)
        {
            Debug.Log("Prefab was saved successfully");
        }
        else
        {
            Debug.Log("Prefab failed to save");
        }

        DestroyImmediate(prefabGo);
        DestroyImmediate(go);
    }
}