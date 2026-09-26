using UnityEditor;

// Cauldron.fbx 전용 임포트 규칙(Plan.md/Cauldron.md). 클립은 CauldronBuilder가 만들므로 모델 애니메이션은 가져오지 않고,
// 수면 연출에 쓰는 블렌드셰이프는 반드시 가져온다. 재질은 Materials 폴더의 .mat으로 리맵한다(Unity에서 색·발광 조정 가능).
public class CauldronImportPostprocessor : AssetPostprocessor
{
    public const string Root = "Assets/09. Environment/Cauldron";
    public const string ModelPath = Root + "/Cauldron.fbx";
    public const string MaterialFolder = Root + "/Materials";

    // 규칙이 바뀌면 숫자를 올려 재임포트를 강제한다.
    public override uint GetVersion() => 1;

    private void OnPreprocessModel()
    {
        if (assetPath != ModelPath) return;

        var importer = (ModelImporter)assetImporter;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = true; // Liquid_Surface의 Idle_A/B, Splash_Dip, Ripple_* 사용
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        if (AssetDatabase.IsValidFolder(MaterialFolder))
            WitchCookieHouseImportPostprocessor.RemapMaterials(importer, MaterialFolder);
    }
}
