using UnityEngine;

// 전역 설정(GameSettingsSO) 접근자. Resources/GameSettings 에셋을 처음 접근할 때 한 번만 로드해 캐시한다.
// 에셋이 없으면 기본값 인스턴스로 동작하되 경고를 남긴다(설정 누락이 조용히 지나가지 않도록).
public static class GameSettings
{
    private const string ResourcePath = "GameSettings";

    private static GameSettingsSO cached;

    public static GameSettingsSO Current
    {
        get
        {
            if (cached != null) return cached;

            cached = Resources.Load<GameSettingsSO>(ResourcePath);
            if (cached == null)
            {
                Debug.LogWarning($"[GameSettings] Resources/{ResourcePath} asset not found. Using default values.");
                cached = ScriptableObject.CreateInstance<GameSettingsSO>();
            }
            return cached;
        }
    }
}
