using UnityEditor;
using UnityEngine;

// 음원 임포트 설정을 폴더로 정한다(SoundPlan.md §2.5). 손으로 바꿔도 다시 임포트하면 이 규칙으로 돌아간다 — 규칙을 바꾸려면 여기를 고친다.
// - BGM: 스트리밍(메모리에 통째로 올리지 않음), Vorbis 70%, 미리 로드 안 함.
// - SFX/Ambience: 긴 반복음 — 압축한 채 메모리에, Vorbis 60%, 모노.
// - 그 밖의 SFX: 짧은 소리 — 로드할 때 압축 해제(재생 중 CPU 0), 모노(3D 위치감).
public class AudioImportPostprocessor : AssetPostprocessor
{
    public const string AudioRoot = "Assets/10. Audio/";
    public const string MusicFolder = AudioRoot + "BGM/";
    public const string SoundFolder = AudioRoot + "SFX/";
    public const string AmbienceFolder = SoundFolder + "Ambience/";

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioRoot)) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        if (assetPath.StartsWith(MusicFolder))
        {
            importer.forceToMono = false;
            importer.loadInBackground = true;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = false;
        }
        else if (assetPath.StartsWith(AmbienceFolder))
        {
            importer.forceToMono = true;
            importer.loadInBackground = true;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = true;
        }
        else if (assetPath.StartsWith(SoundFolder))
        {
            importer.forceToMono = true;
            importer.loadInBackground = false;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
        }
        else return;

        importer.defaultSampleSettings = settings;
    }
}
