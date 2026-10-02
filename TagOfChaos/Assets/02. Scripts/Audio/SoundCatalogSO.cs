using System;
using System.Collections.Generic;
using UnityEngine;

public enum SoundSpace { Flat2D, World3D }

// 풀이 꽉 찼을 때 무엇을 먼저 뺏을지(낮은 것부터).
public enum SoundImportance { Low, Normal, High, Critical }

// 효과음 ID → 클립과 재생 규칙(SoundPlan.md §2.3·§2.5). 거리·음량·쿨다운을 여기 두어 코드 수정 없이 조정한다.
// 항목은 에디터 도구(Tools/TagOfChaos/Audio/Build Catalogs)가 ID마다 하나씩 만들고, 클립은 파일 이름(= ID, 변형은 ID_1, ID_2 …)으로 채운다.
[CreateAssetMenu(menuName = "Audio/SoundCatalog")]
public class SoundCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Audio/SoundCatalog";

    [Serializable]
    public class Entry
    {
        public SoundId id;
        [Tooltip("재생할 때 하나를 무작위로 고른다(같은 소리가 기계적으로 반복되지 않게)")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생마다 이 범위에서 피치를 고른다")]
        public Vector2 pitchRange = Vector2.one;
        public SoundSpace space = SoundSpace.World3D;
        public SoundBus bus = SoundBus.Sfx;
        [Tooltip("3D: 이 거리에서 들리지 않게 된다(m)")]
        [Min(1f)] public float maxDistance = 15f;
        [Tooltip("같은 소리를 다시 낼 수 있는 최소 간격(초)")]
        [Min(0f)] public float cooldown = 0.05f;
        [Tooltip("같은 소리가 동시에 재생될 수 있는 수")]
        [Min(1)] public int maxInstances = 4;
        public SoundImportance importance = SoundImportance.Normal;

        public bool HasClip
        {
            get
            {
                if (clips == null) return false;
                foreach (AudioClip c in clips) if (c != null) return true;
                return false;
            }
        }

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip != null) return clip;
            foreach (AudioClip c in clips) if (c != null) return c; // 빈 칸을 골랐으면 다른 칸
            return null;
        }

        public float PickPitch() => UnityEngine.Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
    }

    [SerializeField] private Entry[] entries = new Entry[0];

    private Dictionary<SoundId, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(SoundId id, out Entry entry)
    {
        if (lookup == null) BuildLookup();
        return lookup.TryGetValue(id, out entry);
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<SoundId, Entry>(entries.Length);
        foreach (Entry e in entries) if (e != null && e.id != SoundId.None) lookup[e.id] = e;
    }

    private void OnValidate() => lookup = null; // 인스펙터에서 고치면 다시 만든다

#if UNITY_EDITOR
    public void EditorSetEntries(Entry[] value)
    {
        entries = value;
        lookup = null;
    }
#endif

    private static SoundCatalogSO cached;

    // 처음 접근할 때 Resources에서 한 번 읽는다(GameSettings와 같은 방식). 없으면 빈 카탈로그 — 소리만 나지 않는다.
    public static SoundCatalogSO Current
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<SoundCatalogSO>(ResourcePath);
            if (cached == null)
            {
                Debug.LogWarning($"[Audio] Resources/{ResourcePath} not found. Sounds are muted.");
                cached = CreateInstance<SoundCatalogSO>();
            }
            return cached;
        }
    }
}
