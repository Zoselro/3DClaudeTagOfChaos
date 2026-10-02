using System;
using System.Collections.Generic;
using UnityEngine;

// 배경음·스팅어·징글 ID → 클립(SoundPlan.md §3.1). 곡 고르기 규칙(씬 × 단계 → 곡)은 S3의 MusicRules가 맡는다.
// 클립은 에디터 도구가 파일 이름(= ID)으로 채운다. 배경음 클립은 스트리밍으로 가져온다(임포트 후처리).
[CreateAssetMenu(menuName = "Audio/MusicCatalog")]
public class MusicCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Audio/MusicCatalog";

    [Serializable]
    public class Entry
    {
        public MusicId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("반복 곡이면 켠다(스팅어·징글은 끈다)")]
        public bool loop = true;
    }

    [SerializeField] private Entry[] entries = new Entry[0];

    private Dictionary<MusicId, Entry> lookup;

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGet(MusicId id, out Entry entry)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<MusicId, Entry>(entries.Length);
            foreach (Entry e in entries) if (e != null && e.id != MusicId.None) lookup[e.id] = e;
        }
        return lookup.TryGetValue(id, out entry);
    }

    private void OnValidate() => lookup = null;

    // 반복 곡이 아닌 것(스팅어·징글)은 ID 구간으로 정한다(SoundId.cs 주석의 구간 규칙).
    public static bool IsLoopById(MusicId id) => (int)id < 100;

#if UNITY_EDITOR
    public void EditorSetEntries(Entry[] value)
    {
        entries = value;
        lookup = null;
    }
#endif

    private static MusicCatalogSO cached;

    public static MusicCatalogSO Current
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<MusicCatalogSO>(ResourcePath);
            if (cached == null)
            {
                Debug.LogWarning($"[Audio] Resources/{ResourcePath} not found. Music is muted.");
                cached = CreateInstance<MusicCatalogSO>();
            }
            return cached;
        }
    }
}
