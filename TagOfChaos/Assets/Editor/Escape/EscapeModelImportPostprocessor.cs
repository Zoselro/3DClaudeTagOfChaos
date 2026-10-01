using UnityEditor;

// 탈출 모드 Blender 모델(Assets/Maps/Source~/Scripts/escape_assets.py가 내보낸 FBX) 임포트 규칙(EscapePlan.md §4.4).
// - 1 unit = 1 m, 축 변환은 Blender에서 구웠으므로 bakeAxisConversion은 끈다. 애니메이션·카메라·조명은 가져오지 않는다.
// - 머티리얼은 맵 공용 팔레트(M_*)와 탈출 모드 전용(ME_*) .mat으로 이름이 같으면 연결한다(앞 폴더 우선).
public class EscapeModelImportPostprocessor : AssetPostprocessor
{
    public const string ModelFolder = "Assets/09. Environment/Escape/Models";
    public const string MaterialFolder = "Assets/09. Environment/Escape/Materials";
    public const string CommonMaterialFolder = "Assets/Maps/Common/Materials";

    // EscapeModelBuilder가 FBX 안의 원래 머티리얼 색을 읽는 동안에는 연결하지 않는다.
    public static bool SkipRemap;

    // 규칙이 바뀌면 숫자를 올려 재임포트를 강제한다.
    public override uint GetVersion() => 1;

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ModelFolder + "/")) return;

        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        if (!SkipRemap) RemapMaterials(importer);
    }

    public static bool RemapMaterials(ModelImporter importer) =>
        WitchCookieHouseImportPostprocessor.RemapMaterials(importer, CommonMaterialFolder, MaterialFolder);
}
