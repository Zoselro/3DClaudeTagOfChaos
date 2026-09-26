using UnityEditor;
using UnityEngine;

// WitchCookieProps 폴더의 소품 FBX 임포트 규칙(과자집과 동일한 기준).
// - 1 unit = 1 m, 축 변환은 Blender에서 구워 둠
// - 머티리얼은 소품 Materials 폴더 우선, 없으면 과자집 Materials 폴더의 같은 이름 .mat으로 리맵
// - COL_ 오브젝트는 convex MeshCollider로 변환
public class WitchCookiePropsImportPostprocessor : AssetPostprocessor
{
    public const string PropsRoot = "Assets/09. Environment/WitchCookieProps";
    public const string PropsMaterialFolder = PropsRoot + "/Materials";

    public static readonly string[] MaterialFolders =
    {
        PropsMaterialFolder,
        WitchCookieHouseImportPostprocessor.MaterialFolder
    };

    public override uint GetVersion() => 1;

    public static bool IsPropModel(string path) => path.StartsWith(PropsRoot + "/") && path.EndsWith(".fbx");

    private void OnPreprocessModel()
    {
        if (!IsPropModel(assetPath)) return;

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
        WitchCookieHouseImportPostprocessor.RemapMaterials(importer, MaterialFolders);
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (!IsPropModel(assetPath)) return;
        WitchCookieHouseImportPostprocessor.ConvertCollisionObjects(root);
    }
}
