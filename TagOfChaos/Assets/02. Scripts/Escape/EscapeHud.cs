using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탈출 모드 화면 표시(EscapePlan.md §1.9, §1.11). 프리팹 없이 코드로 만든다(맵 씬마다 EscapeManager가 하나 만든다).
// - 오른쪽 위: 필요한 재료(이미지·이름·지금/필요). 쿠키는 탈출 장치, 스파이는 자기 로켓의 재료.
// - 아래 가운데: 인벤토리 4칸(고른 칸 강조, 도구 남은 횟수).
// - 가운데 위: 알림(떴다가 서서히 사라짐) — 스파이 잡힘, 로켓에 끼움 등.
// - 스파이 본인: 시작 때 "당신은 스파이" 알림과 작은 표시.
// - 타임어택 타이머(빨강). 문구는 모두 EscapeTextsSO에서 읽는다(코드에 한글 금지).
public class EscapeHud : MonoBehaviour
{
    private const int MaxRequirementRows = 6;
    private static EscapeHud instance;
    private readonly List<StolenNotice> pendingStolen = new List<StolenNotice>();

    private EscapeManager manager;
    private EscapeTextsSO texts;
    private readonly List<RequirementRow> rows = new List<RequirementRow>();
    private readonly SlotView[] slots = new SlotView[PlayerInventory.SlotCount];
    private CanvasGroup toastGroup;
    private Canvas hudCanvas;
    private RectTransform hotbar;
    private CanvasGroup hotbarGroup;
    private const float LockedHotbarAlpha = 0.4f;
    private const float HotbarY = 150f;
    private const float HotbarYAbovePalette = 290f; // 변장 단계에는 아래쪽 색 팔레트와 겹치지 않게 위로 올린다
    private TMP_Text toastText;
    private Coroutine toastRoutine;
    private GameObject spyBadge;
    private TMP_Text timeAttackText;
    private TMP_Text boardingText;
    private PlayerInventory boundInventory;
    private bool spyNoticeShown;
    private int lastTimeAttackSeconds = -1;
    private static Sprite whiteSprite;

    private struct RequirementRow { public GameObject Root; public Image Icon; public TMP_Text Label; }
    private struct SlotView { public Image Frame; public Image Icon; public TMP_Text Name; public TMP_Text Charges; }

    public static void Create(EscapeManager owner)
    {
        if (instance != null) Destroy(instance.gameObject);
        var go = new GameObject(nameof(EscapeHud), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        instance = go.AddComponent<EscapeHud>();
        instance.hudCanvas = canvas;
        instance.manager = owner;
        instance.texts = EscapeTextsSO.Current;
        instance.Build();
        owner.StateChanged += instance.RefreshRequirements;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= RefreshRequirements;
        if (instance == this) instance = null;
    }

    // ---------------- static API ----------------

    // alert = 모두가 주목할 알림(스파이 잡힘·마녀) — 소리가 다르다(SoundPlan.md S2).
    public static void Toast(string message, bool alert = false)
    {
        if (instance == null || string.IsNullOrEmpty(message)) return;
        instance.ShowToast(message, 2.5f);
        UiSoundCues.Toast(alert);
    }

    // 스파이가 장치에서 뺀 재료 알림(Request1003bPlan.md §2): 방장이 정한 시각이 되면 띄운다. 괴물 화면에는 띄우지 않는다.
    public static void ScheduleStolen(string itemId, int itemIndex, double showAt)
    {
        if (instance == null || RoomState.IsLocalMonster()) return;
        instance.pendingStolen.Add(new StolenNotice { ItemId = itemId, ItemIndex = itemIndex, ShowAt = showAt });
    }

    private void TickStolenNotices()
    {
        for (int i = pendingStolen.Count - 1; i >= 0; i--)
        {
            StolenNotice n = pendingStolen[i];
            EscapeState state = manager.State;
            StolenNotice.Decision d = n.Decide(PhotonNetwork.Time, state != null && n.ItemIndex >= 0 && n.ItemIndex < state.Items.Count ? state.Items[n.ItemIndex].Loc : ItemLocation.Held);
            if (d == StolenNotice.Decision.Wait) continue;
            pendingStolen.RemoveAt(i);
            if (d == StolenNotice.Decision.Cancel) continue;
            ItemSO item = manager.Catalog.Find(n.ItemId);
            string name = item != null ? item.DisplayName : n.ItemId;
            Toast(string.Format(KoreanText.HasFinalConsonant(name) ? texts.stolenFromDeviceWithFinal : texts.stolenFromDeviceNoFinal, name), alert: true);
        }
    }

    public static void Notice(EscapeNoticeKind kind, string itemId)
    {
        if (instance == null) return;
        EscapeTextsSO t = instance.texts;
        switch (kind)
        {
            case EscapeNoticeKind.SpyCaught: Toast(t.spyCaught, alert: true); break;
            case EscapeNoticeKind.DeviceComplete: Toast(t.deviceComplete); break;
        }
    }

    // ---------------- build ----------------

    private void Build()
    {
        // 오른쪽 위 필요한 재료
        var reqRoot = NewRect("Requirements", transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -120f), new Vector2(300f, 40f * MaxRequirementRows));
        reqRoot.pivot = new Vector2(1f, 1f);
        for (int i = 0; i < MaxRequirementRows; i++)
        {
            var row = NewRect("Row" + i, reqRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * 40f), new Vector2(0f, 36f));
            row.pivot = new Vector2(0.5f, 1f);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = WhiteSprite(); bg.color = new Color(0f, 0f, 0f, 0.45f); bg.raycastTarget = false;
            var icon = NewImage("Icon", row, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(28f, 28f));
            var label = NewText("Label", row, 22f, TextAlignmentOptions.MidlineLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f); label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(40f, 0f); label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            rows.Add(new RequirementRow { Root = row.gameObject, Icon = icon, Label = label });
            row.gameObject.SetActive(false);
        }

        // 아래 가운데 인벤토리 4칸
        var bar = NewRect("Hotbar", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, HotbarY), new Vector2(4 * 96f, 90f));
        hotbar = bar;
        hotbarGroup = bar.gameObject.AddComponent<CanvasGroup>();
        hotbarGroup.blocksRaycasts = false;
        for (int i = 0; i < slots.Length; i++)
        {
            var cell = NewRect("Slot" + (i + 1), bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(48f + i * 96f, 0f), new Vector2(86f, 86f));
            var frame = cell.gameObject.AddComponent<Image>();
            frame.sprite = WhiteSprite(); frame.raycastTarget = false;
            var icon = NewImage("Icon", cell, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(50f, 50f));
            var name = NewText("Name", cell, 15f, TextAlignmentOptions.Bottom);
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(2f, 2f); name.rectTransform.offsetMax = new Vector2(-2f, -2f);
            var number = NewText("Key", cell, 16f, TextAlignmentOptions.TopLeft);
            number.rectTransform.anchorMin = Vector2.zero; number.rectTransform.anchorMax = Vector2.one;
            number.rectTransform.offsetMin = new Vector2(6f, 2f); number.rectTransform.offsetMax = new Vector2(-4f, -4f);
            number.text = (i + 1).ToString();
            var charges = NewText("Charges", cell, 18f, TextAlignmentOptions.TopRight);
            charges.rectTransform.anchorMin = Vector2.zero; charges.rectTransform.anchorMax = Vector2.one;
            charges.rectTransform.offsetMin = new Vector2(2f, 2f); charges.rectTransform.offsetMax = new Vector2(-6f, -4f);
            slots[i] = new SlotView { Frame = frame, Icon = icon, Name = name, Charges = charges };
        }

        // 가운데 위 알림
        var toast = NewRect("Toast", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1100f, 60f));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false; toastGroup.interactable = false;
        var toastBg = toast.gameObject.AddComponent<Image>();
        toastBg.sprite = WhiteSprite(); toastBg.color = new Color(0f, 0f, 0f, 0.6f); toastBg.raycastTarget = false;
        toastText = NewText("Text", toast, 30f, TextAlignmentOptions.Center);
        toastText.rectTransform.anchorMin = Vector2.zero; toastText.rectTransform.anchorMax = Vector2.one;
        toastText.rectTransform.offsetMin = Vector2.zero; toastText.rectTransform.offsetMax = Vector2.zero;

        // 스파이 표시(왼쪽 위, 본인에게만)
        var badge = NewRect("SpyBadge", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -60f), new Vector2(140f, 44f));
        var badgeBg = badge.gameObject.AddComponent<Image>();
        badgeBg.sprite = WhiteSprite(); badgeBg.color = new Color(0.55f, 0f, 0.1f, 0.85f); badgeBg.raycastTarget = false;
        var badgeText = NewText("Text", badge, 26f, TextAlignmentOptions.Center);
        badgeText.rectTransform.anchorMin = Vector2.zero; badgeText.rectTransform.anchorMax = Vector2.one;
        badgeText.rectTransform.offsetMin = Vector2.zero; badgeText.rectTransform.offsetMax = Vector2.zero;
        badgeText.text = texts.spyBadge;
        spyBadge = badge.gameObject;
        spyBadge.SetActive(false);

        // 타임어택 타이머(가운데 위, 빨강)
        // 탑승 인원(가운데 위, 탈출 장치가 완성된 뒤, EscapeVisualPlan.md §4.3)
        boardingText = NewText("Boarding", transform, 34f, TextAlignmentOptions.Center);
        boardingText.rectTransform.anchorMin = new Vector2(0.5f, 1f); boardingText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        boardingText.rectTransform.anchoredPosition = new Vector2(0f, -235f); boardingText.rectTransform.sizeDelta = new Vector2(600f, 50f);
        boardingText.color = new Color(1f, 0.9f, 0.45f);
        boardingText.gameObject.SetActive(false);

        timeAttackText = NewText("TimeAttack", transform, 44f, TextAlignmentOptions.Center);
        timeAttackText.rectTransform.anchorMin = new Vector2(0.5f, 1f); timeAttackText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        timeAttackText.rectTransform.anchoredPosition = new Vector2(0f, -110f); timeAttackText.rectTransform.sizeDelta = new Vector2(400f, 60f);
        timeAttackText.color = new Color(1f, 0.25f, 0.25f);
        timeAttackText.gameObject.SetActive(false);
    }

    // ---------------- update ----------------

    private void Update()
    {
        // 결과 화면이 뜨면 HUD(가방·필요 재료·알림)는 결과 위를 가리지 않도록 숨긴다.
        bool showHud = GamePhaseState.Current != GamePhase.Result;
        if (hudCanvas.enabled != showHud) hudCanvas.enabled = showHud;
        if (!showHud) return;
        TickStolenNotices();
        bool painting = GamePhaseState.Current == GamePhase.Paint;
        float barY = painting ? HotbarYAbovePalette : HotbarY;
        if (!Mathf.Approximately(hotbar.anchoredPosition.y, barY)) hotbar.anchoredPosition = new Vector2(0f, barY);
        float barAlpha = painting ? LockedHotbarAlpha : 1f; // 변장 시간에는 아이템을 쓸 수 없음을 흐리게 보여준다(§1.3)
        if (!Mathf.Approximately(hotbarGroup.alpha, barAlpha)) hotbarGroup.alpha = barAlpha;

        if (boundInventory != PlayerInventory.Local)
        {
            if (boundInventory != null) boundInventory.Changed -= RefreshHotbar;
            boundInventory = PlayerInventory.Local;
            if (boundInventory != null) boundInventory.Changed += RefreshHotbar;
            RefreshHotbar();
            RefreshRequirements();
        }

        bool spy = RoomState.IsLocalSpy();
        if (spyBadge.activeSelf != spy) spyBadge.SetActive(spy);
        if (spy && !spyNoticeShown)
        {
            spyNoticeShown = true;
            ShowToast(texts.youAreSpy, 4f);
            UiSoundCues.Toast(alert: false);
        }

        RefreshBoarding();

        bool timeAttack = RoomState.TryGetDouble(NetKeys.TimeAttackEndTime, out double end) && GamePhaseState.Current == GamePhase.TimeAttack;
        if (timeAttackText.gameObject.activeSelf != timeAttack) timeAttackText.gameObject.SetActive(timeAttack);
        if (timeAttack)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt((float)(end - PhotonNetwork.Time)));
            if (seconds != lastTimeAttackSeconds)
            {
                if (lastTimeAttackSeconds >= 0) UiSoundCues.CountdownTick(seconds); // 타임어택 마지막 10초
                lastTimeAttackSeconds = seconds;
                timeAttackText.text = string.Format(texts.timeAttackFormat, seconds / 60, seconds % 60);
            }
        }
    }

    // "탑승 2 / 4": 탄 쿠키 / 살아 있는 쿠키(스파이 제외). 출발하면 "출발!".
    private void RefreshBoarding()
    {
        EscapeState s = manager != null ? manager.State : null;
        bool show = s != null && s.CompletedAt > 0 && (s.DepartedAt <= 0 || manager.IsDeparting(PhotonNetwork.Time));
        if (boardingText.gameObject.activeSelf != show) boardingText.gameObject.SetActive(show);
        if (!show) return;
        if (s.DepartedAt > 0)
        {
            boardingText.text = texts.departing;
            return;
        }
        int alive = 0;
        foreach (Photon.Realtime.Player p in PhotonNetwork.PlayerList)
        {
            int actor = p.ActorNumber;
            if (RoomState.IsMonster(actor) || RoomState.IsSpy(actor) || RoomState.IsBroken(p)) continue;
            alive++;
        }
        boardingText.text = string.Format(texts.boardingFormat, s.Waiting.Count, alive);
    }

    private void RefreshHotbar()
    {
        PlayerInventory inv = boundInventory;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemSO item = inv != null ? inv.ItemAt(i) : null;
            bool selected = inv != null && inv.Selected == i;
            slots[i].Frame.color = selected ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0f, 0f, 0f, 0.5f);
            slots[i].Icon.enabled = item != null;
            if (item != null)
            {
                slots[i].Icon.sprite = item.Icon != null ? item.Icon : WhiteSprite();
                slots[i].Icon.color = item.Icon != null ? Color.white : item.Tint;
            }
            slots[i].Name.text = item != null ? item.DisplayName : string.Empty;
            int index = inv != null && item != null ? inv.HeldItemIndexAt(i) : -1;
            slots[i].Charges.text = item != null && item.IsTool && index >= 0 && manager.State != null ? manager.State.Items[index].Charges.ToString() : string.Empty;
        }
    }

    // 쿠키는 탈출 장치 재료, 스파이는 로켓 재료(§1.11). 같은 이름끼리 묶어 (지금/필요)로 보여준다.
    private void RefreshRequirements()
    {
        RefreshHotbar();
        EscapeState s = manager.State;
        var entries = new List<(string label, ItemSO item, int have, int need)>();
        if (s != null)
        {
            bool spy = RoomState.IsLocalSpy();
            foreach (EscapeState.Slot slot in spy ? s.RocketSlots : s.DeviceSlots)
            {
                if (slot.Prefilled) continue; // 처음부터 채워진 칸은 셈에서 뺀다
                int k = entries.FindIndex(e => e.label == slot.Label);
                ItemSO item = slot.Accepts.Length > 0 ? manager.Catalog.Find(slot.Accepts[0]) : null;
                if (k < 0) { entries.Add((slot.Label, item, 0, 0)); k = entries.Count - 1; }
                var e0 = entries[k];
                entries[k] = (e0.label, e0.item, e0.have + (slot.Filled ? 1 : 0), e0.need + 1);
            }
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool show = i < entries.Count;
            rows[i].Root.SetActive(show);
            if (!show) continue;
            var e = entries[i];
            rows[i].Icon.sprite = e.item != null && e.item.Icon != null ? e.item.Icon : WhiteSprite();
            rows[i].Icon.color = e.item != null && e.item.Icon != null ? Color.white : (e.item != null ? e.item.Tint : Color.white);
            rows[i].Label.text = $"{e.label}  {e.have}/{e.need}";
            rows[i].Label.color = e.have >= e.need ? new Color(0.5f, 1f, 0.5f) : Color.white;
        }
    }

    private void ShowToast(string message, float hold)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastText.text = message;
        toastRoutine = StartCoroutine(FadeToast(hold, 1.5f));
    }

    // 떴다가 서서히 사라진다(D34).
    private IEnumerator FadeToast(float hold, float fade)
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < fade; t += Time.deltaTime)
        {
            toastGroup.alpha = 1f - t / fade;
            yield return null;
        }
        toastGroup.alpha = 0f;
        toastRoutine = null;
    }

    // ---------------- ui helpers ----------------

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image NewImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect(name, parent, anchor, anchor, pos, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = WhiteSprite();
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }
}
