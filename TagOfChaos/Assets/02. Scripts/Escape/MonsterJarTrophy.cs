using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 괴물 승리 결과 화면의 쿠키 유리병 트로피(EscapePlan.md §1.3 추천안). 괴물마다 유리병 하나를 그리고, 잡은 쿠키 수만큼 병 안에
// 쿠키가 들어 있다. 괴물이 둘 이상이면 가장 많이 잡은 괴물이 가운데에 서고 왕관(MVP)이 붙는다. 프리팹 없이 코드로 만든다.
// 유리병은 3D 모델을 전용 카메라로 찍어 보여 준다(MonsterJarStage, EscapeVisualPlan.md §3.3). 모델이 없으면 2D로 그린다.
public static class MonsterJarTrophy
{
    public struct Entry
    {
        public string Name;
        public int Catches;
    }

    private const float JarWidth = 150f;
    private const float JarHeight = 190f;
    private const float Gap = 60f;
    private const float JarOffsetX = -560f;
    private static TMP_FontAsset font; // 결과 화면 글꼴(한글 닉네임)

    public static void Build(Transform parent, List<Entry> entries)
    {
        if (entries == null || entries.Count == 0) return;
        TMP_Text sample = parent.GetComponentInChildren<TMP_Text>(true);
        font = sample != null ? sample.font : null;
        Transform old = parent.Find("MonsterJarTrophy");
        if (old != null) Object.Destroy(old.gameObject);

        // MVP를 가운데로: 가장 많이 잡은 괴물을 가운데, 나머지를 양옆에.
        var sorted = new List<Entry>(entries);
        sorted.Sort((a, b) => b.Catches.CompareTo(a.Catches));
        var ordered = new List<Entry>();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (i % 2 == 0) ordered.Add(sorted[i]);
            else ordered.Insert(0, sorted[i]);
        }
        int mvp = ordered.IndexOf(sorted[0]);

        var root = new GameObject("MonsterJarTrophy", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(JarOffsetX, -200f); // 가운데의 쿠키 수·결과 목록과 겹치지 않게 왼쪽에 선다
        float width = ordered.Count * JarWidth + (ordered.Count - 1) * Gap;
        root.sizeDelta = new Vector2(width, JarHeight + 80f);

        for (int i = 0; i < ordered.Count; i++)
            BuildJar(root, ordered[i], -width / 2f + JarWidth / 2f + i * (JarWidth + Gap), i == mvp && ordered.Count > 1);
    }

    private static void BuildJar(RectTransform root, Entry entry, float x, bool isMvp)
    {
        RectTransform jar = Rect("Jar_" + entry.Name, root, new Vector2(x, -40f), new Vector2(JarWidth, JarHeight));
        bool model = MonsterJarStage.Available;
        if (model)
        {
            RectTransform view = Rect("Jar3D", jar, Vector2.zero, new Vector2(JarWidth * 1.3f, JarHeight * 1.3f));
            view.gameObject.AddComponent<RawImage>();
            view.gameObject.AddComponent<MonsterJarStage>().Init(entry.Catches, view.sizeDelta);
        }
        else
        {
            Img(jar, new Color(0.75f, 0.9f, 1f, 0.25f));                                      // 유리
            Img(Rect("Lid", jar, new Vector2(0f, JarHeight / 2f + 10f), new Vector2(JarWidth * 0.8f, 20f)), new Color(0.5f, 0.35f, 0.2f));

            // 잡힌 쿠키: 병 안에 줄지어 쌓인다(최대 12개까지 그린다).
            int count = Mathf.Min(entry.Catches, 12);
            for (int i = 0; i < count; i++)
            {
                int col = i % 3, row = i / 3;
                var cookie = Rect("Cookie" + i, jar, new Vector2(-40f + col * 40f, -JarHeight / 2f + 28f + row * 38f), new Vector2(34f, 34f));
                Img(cookie, new Color(0.78f, 0.55f, 0.3f));
            }
        }

        TMP_Text name = Text(jar, entry.Name, new Vector2(0f, -JarHeight / 2f - 26f), 24f);
        name.color = Color.white;
        // 3D 병은 쿠키가 보이도록 숫자를 오른쪽 위에 둔다
        TMP_Text count2 = Text(jar, "x" + entry.Catches, model ? new Vector2(JarWidth / 2f, JarHeight / 2f - 10f) : Vector2.zero, model ? 34f : 40f);
        count2.color = new Color(1f, 1f, 1f, 0.9f);
        if (isMvp)
        {
            TMP_Text crown = Text(jar, "MVP", new Vector2(0f, JarHeight / 2f + 44f), 30f);
            crown.color = new Color(1f, 0.85f, 0.2f);
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Img(RectTransform rt, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static TMP_Text Text(Transform parent, string value, Vector2 pos, float size)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(260f, 50f);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = value;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }
}
