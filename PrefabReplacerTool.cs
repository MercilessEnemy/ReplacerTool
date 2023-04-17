#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;

//using AmplifyImpostors;

public class PrefabReplacerTool : EditorWindow
{
    [SerializeField] private Vector3 newPosition;
    [SerializeField] private Vector3 newRotation;
    [SerializeField] private Vector3 newScale = new Vector3(1f, 1f, 1f);
    [SerializeField] private List<float> lods;
    [SerializeField] private int selectedLOD;
    [SerializeField] private Shader sourceShader;
    [SerializeField] private Shader targetShader;
    [SerializeField] private bool isHDRP;

    private bool isActive;
    private bool showLayers;
    private bool showStatics;
    private Vector2 scrollPosition = Vector2.zero;
    private GameObject newPrefab;
    private GameObject prefab;
    private List<string> gameObjectNames = new List<string>();
    private List<string> layerNames = new List<string>();
    private List<bool> isStatic = new List<bool>();
    private LayerMask newLayer;
    private bool staticEnabled;
    private LODGroup lodGroup;
    private List<Material> materials = new List<Material>();
    private List<string> assetPaths = new List<string>();
    public List<GameObject> matchingPrefabs = new List<GameObject>();
    private int vertexCount;
    private int submeshCount;
    private int triangleCount;

    [MenuItem("Tools/Prefab Replacer Tool")]
    public static void ShowWindow()
    {
        GetWindow<PrefabReplacerTool>("Prefab Replacer Tool");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Select a prefab:");
        newPrefab = EditorGUILayout.ObjectField(newPrefab, typeof(GameObject), true) as GameObject;

        if (newPrefab == null)
        {
            EditorGUILayout.HelpBox("No prefab selected.", MessageType.Info);

            if (Selection.activeGameObject == null)
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Name: ", Selection.activeGameObject.name);

            isActive = EditorGUILayout.Toggle("Is Active", Selection.activeGameObject.activeSelf);

            if (isActive != Selection.activeGameObject.activeSelf)
            {
                Selection.activeGameObject.SetActive(isActive);

                EditorUtility.SetDirty(Selection.activeGameObject);
            }

            if (Selection.activeGameObject.GetComponent<MeshFilter>())
            {
                var meshFilter = Selection.activeGameObject.GetComponent<MeshFilter>();
                vertexCount = meshFilter.sharedMesh.vertexCount;
                triangleCount = meshFilter.sharedMesh.triangles.Length / 3;
                submeshCount = meshFilter.sharedMesh.subMeshCount;

                EditorGUILayout.LabelField("Vertices: ", vertexCount.ToString());
                EditorGUILayout.LabelField("Triangles: ", triangleCount.ToString());
                EditorGUILayout.LabelField("SubMeshes: ", submeshCount.ToString());
            }

            if (GUILayout.Button("Unparent selected gameObject"))
            {
                UnparentGameObject();
            }

            EditorGUILayout.EndScrollView();
            Repaint();
            return;
        }

        if (newPrefab != prefab)
        {
            prefab = newPrefab;

            ShowPrefabInformation();

            matchingPrefabs.Clear();
        }

        GUILayout.Space(10);

        GUILayout.Label("Prefab Information", EditorStyles.boldLabel);

        isActive = EditorGUILayout.Toggle("Is Active", prefab.activeSelf);

        if (isActive != prefab.activeSelf)
        {
            prefab.SetActive(isActive);

            EditorUtility.SetDirty(prefab);
        }

        if (prefab.GetComponent<MeshFilter>() != null)
        {
            EditorGUILayout.BeginVertical("Box");

            var meshFilter = prefab.GetComponent<MeshFilter>();

            vertexCount = meshFilter.sharedMesh.vertexCount;
            triangleCount = meshFilter.sharedMesh.triangles.Length / 3;
            submeshCount = meshFilter.sharedMesh.subMeshCount;

            EditorGUILayout.LabelField("Vertices: ", vertexCount.ToString());
            EditorGUILayout.LabelField("Triangles: ", triangleCount.ToString());
            EditorGUILayout.LabelField("SubMeshes: ", submeshCount.ToString());

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
        }

        GUILayout.Label("Transform:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");

        GUILayout.BeginHorizontal();
        {
            GUILayout.Label("Position:");
            GUILayout.FlexibleSpace();
            GUILayout.Label(prefab.transform.position.ToString());
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            GUILayout.Label("Rotation:");
            GUILayout.FlexibleSpace();
            GUILayout.Label(prefab.transform.localEulerAngles.ToString());
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            GUILayout.Label("Scale:");
            GUILayout.FlexibleSpace();
            GUILayout.Label(prefab.transform.localScale.ToString());
        }
        GUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        showLayers = EditorGUILayout.Foldout(showLayers, "Layers:");

        if (showLayers)
        {
            EditorGUILayout.BeginVertical("Box");

            for (var i = 0; i < layerNames.Count; ++i)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(gameObjectNames[i] + ":");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(layerNames[i]);
                }
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(10);

        showStatics = EditorGUILayout.Foldout(showStatics, "Static:");

        if (showStatics)
        {
            EditorGUILayout.BeginVertical("Box");

            for (var i = 0; i < isStatic.Count; ++i)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(gameObjectNames[i] + ":");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(isStatic[i].ToString());
                }
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(10);

        if (lodGroup != null)
        {
            GUILayout.Label("LODs:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");

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

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        if (GUILayout.Button("Refresh Prefab Information"))
        {
            ShowPrefabInformation();
        }

        if (GUILayout.Button("Clear"))
        {
            newPrefab = null;
            prefab = null;
            EditorGUILayout.EndScrollView();
            Repaint();
            return;
        }

        GUILayout.Space(10);

        GUILayout.Label("Modify Transform:", EditorStyles.boldLabel);

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

        if (GUILayout.Button("Update Position"))
        {
            UpdatePosition();
        }

        if (GUILayout.Button("Update Rotation"))
        {
            UpdateRotation();
        }

        if (GUILayout.Button("Update Scale"))
        {
            UpdateScale();
        }

        GUILayout.Space(10);

        GUILayout.Label("Modify Layers:", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Layer:");
        GUILayout.FlexibleSpace();
        newLayer = EditorGUILayout.LayerField(newLayer);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Update All Layers"))
        {
            SetLayers();
        }

        GUILayout.Space(10);

        GUILayout.Label("Modify Static:", EditorStyles.boldLabel);
        var text = (staticEnabled) ? "Enable all" : "Disable all";
        if (GUILayout.Button(text))
        {
            SetStatic();
        }

        GUILayout.Space(10);

        GUILayout.Label("Modify LODs:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");

        GUILayout.BeginHorizontal();
        ScriptableObject target = this;
        var so = new SerializedObject(target);
        var stringsProperty = so.FindProperty("lods");
        EditorGUILayout.PropertyField(stringsProperty, true);
        GUILayout.EndHorizontal();
        so.ApplyModifiedProperties();

        if (GUILayout.Button("Add/Replace LODs"))
        {
            ReplaceLods();
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.Label("Create a Terrain Detail Prefab:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Selected LOD:");
        GUILayout.FlexibleSpace();
        selectedLOD = EditorGUILayout.IntField("", selectedLOD);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Save as Terrain Detail Prefab"))
        {
            CreateDetailPrefab();
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        GUILayout.Label("Modify Material:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("Box");

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

        GUILayout.BeginHorizontal();
        GUILayout.Label("HDRP:");
        GUILayout.FlexibleSpace();
        isHDRP = EditorGUILayout.Toggle(isHDRP);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (GUILayout.Button("Find All matching shader materials"))
        {
            GetMaterials();
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

        if (materials.Count > 0)
        {
            if (GUILayout.Button("Replace Shader on all Materials"))
            {
                ReplaceShader();
            }
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        /*if (GUILayout.Button("Unpack prefab"))
        {
            UnpackSelectedPrefab();
        }*/

        if (GUILayout.Button("Find matching prefabs"))
        {
            FindMatchingPrefabs();
        }

        EditorGUILayout.BeginVertical("Box");

        if (matchingPrefabs.Count > 0)
        {
            GUILayout.Label("Matching Prefabs:", EditorStyles.boldLabel);
            foreach (var matchingPrefab in matchingPrefabs)
            {
                EditorGUILayout.ObjectField(matchingPrefab, typeof(GameObject), true);
            }
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        if (GUILayout.Button("Unparent selected gameObject"))
        {
            UnparentGameObject();
        }

        GUILayout.Space(10);

        GUILayout.Label("ASE Impostor:", EditorStyles.boldLabel);

        if (GUILayout.Button("Add ASE Impostor"))
        {
            AddImpostor();
        }

        EditorGUILayout.EndScrollView();
    }

    private void ShowPrefabInformation()
    {
        GetLayers();
        GetStaticObjects();
        GetLODGroup();
    }

    private void GetLayers()
    {
        gameObjectNames.Clear();
        layerNames.Clear();

        if (prefab == null)
            return;

        var transforms = prefab.GetComponentsInChildren<Transform>(true);

        foreach (var child in transforms)
        {
            var layerName = LayerMask.LayerToName(child.gameObject.layer);

            if (!string.IsNullOrEmpty(layerName))
            {
                gameObjectNames.Add(child.name);
                layerNames.Add(layerName);
            }
        }
    }

    private void GetStaticObjects()
    {
        isStatic.Clear();

        if (prefab == null)
            return;

        var transforms = prefab.GetComponentsInChildren<Transform>(true);

        foreach (var child in transforms)
        {
            isStatic.Add(child.gameObject.isStatic);
        }
    }

    private void UpdatePosition()
    {
        if (prefab == null)
            return;

        prefab.transform.position = newPosition;
    }

    private void UpdateRotation()
    {
        if (prefab == null)
            return;

        prefab.transform.localEulerAngles = newRotation;
    }

    private void UpdateScale()
    {
        if (prefab == null)
            return;

        prefab.transform.localScale = newScale;
    }

    private void SetLayers()
    {
        if (prefab == null)
            return;

        //prefab.gameObject.layer = newLayer;

        var transforms = prefab.gameObject.GetComponentsInChildren<Transform>(true);

        foreach (var child in transforms)
        {
            child.gameObject.layer = newLayer;
        }
    }

    private void SetStatic()
    {
        if (prefab == null)
            return;

        prefab.gameObject.isStatic = staticEnabled;

        var transforms = prefab.gameObject.GetComponentsInChildren<Transform>(true);

        foreach (var child in transforms)
        {
            child.gameObject.isStatic = staticEnabled;
        }

        staticEnabled = !staticEnabled;
    }

    private void GetLODGroup()
    {
        if (prefab == null)
            return;

        lodGroup = prefab.gameObject.GetComponent<LODGroup>();
    }

    private void ReplaceLods()
    {
        if (prefab == null)
            return;

        lodGroup = prefab.gameObject.GetComponent<LODGroup>();

        if (lodGroup == null)
            return;

        lodGroup.fadeMode = LODFadeMode.CrossFade;
        lodGroup.animateCrossFading = true;

        var newLods = new LOD[lods.Count];
        var renderers = prefab.gameObject.GetComponentsInChildren<Renderer>();

        for (var i = 0; i < lods.Count; ++i)
        {
            var lod = new LOD(lods[i], renderers);
            newLods[i] = lod;
        }

        lodGroup.SetLODs(newLods);
        lodGroup.RecalculateBounds();

        GetLODGroup();
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

    private void GetMaterials()
    {
        materials.Clear();
        assetPaths.Clear();

        if (prefab == null)
            return;

        if (sourceShader == null)
        {
            Debug.Log("Please select a source shader");
            return;
        }

        var meshRenderer = prefab.gameObject.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            meshRenderer = prefab.gameObject.GetComponentInChildren<MeshRenderer>(true);
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

        if (materials.Count == 0)
        {
            Debug.Log(materials.Count + " materials found on prefab");
        }
    }

    private void ReplaceShader()
    {
        if (sourceShader == null || targetShader == null)
        {
            Debug.Log("Source or target shader is missing");
            return;
        }

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

    private void UnparentGameObject()
    {
        var selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("No gameObjects selected.");
            return;
        }

        foreach (var obj in selectedObjects)
        {
            obj.transform.SetParent(null);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void UnpackSelectedPrefab()
    {
        var selectedObjects = Selection.gameObjects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("No gameObjects selected.");
            return;
        }

        foreach (var obj in selectedObjects)
        {
            var parentPrefabInstance = PrefabUtility.GetPrefabInstanceHandle(obj.transform.parent);
            if (parentPrefabInstance != null)
            {
                PrefabUtility.UnpackPrefabInstance(obj.transform.parent.gameObject, PrefabUnpackMode.OutermostRoot,
                    InteractionMode.AutomatedAction);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private void FindMatchingPrefabs()
    {
        matchingPrefabs.Clear();

        if (prefab == null)
            return;

        var allObjects = FindObjectsOfType<GameObject>();

        foreach (var obj in allObjects)
        {
            if (PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab)
            {
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(obj) == prefab)
                {
                    matchingPrefabs.Add(obj);
                }
            }
        }

        Debug.Log(matchingPrefabs.Count + " matching prefabs were found");

        Repaint();
    }

    private void AddImpostor()
    {
        if (prefab == null)
            return;

        Debug.Log("Requires Amplify Impostor and code uncommented");

        /*AmplifyImpostor ai = prefab.gameObject.GetComponent<AmplifyImpostor>();

        if (ai == null)
        {
            prefab.gameObject.AddComponent<AmplifyImpostor>();
        }*/
    }
}

#endif