using UnityEditor;
using UnityEngine;

// Witch_Cookie_House.fbx 전용 임포트 규칙.
// - 1 unit = 1 m 유지, Blender에서 축 변환을 이미 구워 두었으므로 bakeAxisConversion은 끈다.
// - 머티리얼은 Materials 폴더의 .mat으로 리맵한다(Unity에서 색/텍스처를 바로 조정 가능).
// - COL_ 로 시작하는 오브젝트는 렌더러를 제거하고 convex MeshCollider로 바꾼다.
public class WitchCookieHouseImportPostprocessor : AssetPostprocessor
{
    public const string ModelPath = "Assets/09. Environment/WitchCookieHouse/Witch_Cookie_House.fbx";
    public const string MaterialFolder = "Assets/09. Environment/WitchCookieHouse/Materials";
    public const string NormalMapPath = "Assets/09. Environment/WitchCookieHouse/Textures/T_Cookie_Normal.png";
    public const string CollisionPrefix = "COL_";

    // 규칙이 바뀌면 숫자를 올려 재임포트를 강제한다.
    public override uint GetVersion() => 2;

    private void OnPreprocessModel()
    {
        if (assetPath != ModelPath) return;

        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.generateSecondaryUV = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        RemapMaterials(importer);
    }

    private void OnPreprocessTexture()
    {
        if (assetPath != NormalMapPath) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.NormalMap;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath != ModelPath) return;
        ConvertCollisionObjects(root);
    }

    // COL_ 오브젝트: 렌더러를 제거하고 convex MeshCollider로 바꾼다(모든 COL_ 메시는 볼록 형태로 제작됨).
    public static void ConvertCollisionObjects(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(CollisionPrefix)) continue;

            var filter = t.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            var collider = t.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = true;

            var renderer = t.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
        }
    }

    // 폴더의 .mat을 이름으로 FBX 머티리얼 슬롯에 연결한다. 앞 폴더가 우선. 변경이 있으면 true.
    public static bool RemapMaterials(ModelImporter importer, params string[] folders)
    {
        if (folders == null || folders.Length == 0) folders = new[] { MaterialFolder };

        bool changed = false;
        var map = importer.GetExternalObjectMap();
        var assigned = new System.Collections.Generic.HashSet<string>();
        foreach (string folder in folders)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || !assigned.Add(mat.name)) continue;

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name);
                if (map.TryGetValue(id, out Object current) && current == mat) continue;

                importer.AddRemap(id, mat);
                changed = true;
            }
        }
        return changed;
    }
}
