using UnityEngine;

// 기절(EscapePlan.md §1.8, D24·D29). 쿠키·스파이·괴물 모두 도구에 맞으면 잠깐 움직일 수 없고, 머리 위에 별이 빙글빙글 돈다.
// 움직임 잠금은 본인 클라이언트의 캐릭터 코드가 IsStunned를 보고 한다(HideOrSeekPlayer.IsMovementLocked,
// MonsterController). 별 이펙트는 모든 클라이언트에 보인다. 캐릭터가 Awake에서 붙인다.
public class StunReceiver : MonoBehaviour
{
    private const int StarCount = 3;

    private float stunnedUntil;
    private Transform starRoot;
    private float headHeight = 2.2f;

    public bool IsStunned => Time.time < stunnedUntil;

    public void Init(float characterHeight)
    {
        headHeight = characterHeight;
    }

    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;
        if (!IsStunned && TryGetComponent(out IGameCharacter character)) GameAudio.PlayCharacter(SoundId.StunStars, character); // 별이 도는 순간(S4)
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        EnsureStars();
        starRoot.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (starRoot == null || !starRoot.gameObject.activeSelf) return;
        if (!IsStunned)
        {
            starRoot.gameObject.SetActive(false);
            return;
        }
        starRoot.position = transform.position + Vector3.up * headHeight;
        starRoot.Rotate(0f, 240f * Time.deltaTime, 0f, Space.World);
    }

    // 별 이펙트는 미리 한 번 만들어 두고 켜고 끈다(최적화). 크기는 캐릭터 키에 맞춘다.
    private void EnsureStars()
    {
        if (starRoot != null) return;
        starRoot = new GameObject("StunStars").transform;
        starRoot.SetParent(transform, true);
        float radius = Mathf.Max(0.35f, headHeight * 0.18f);
        float size = Mathf.Max(0.12f, headHeight * 0.06f);
        for (int i = 0; i < StarCount; i++)
        {
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(star.GetComponent<Collider>());
            star.name = "Star";
            star.transform.SetParent(starRoot, false);
            star.transform.localPosition = Quaternion.Euler(0f, i * 360f / StarCount, 0f) * Vector3.forward * radius;
            star.transform.localScale = Vector3.one * size;
            EscapeVisuals.Tint(star, new Color(1f, 0.92f, 0.2f), 2f);
        }
        starRoot.gameObject.SetActive(false);
    }
}
