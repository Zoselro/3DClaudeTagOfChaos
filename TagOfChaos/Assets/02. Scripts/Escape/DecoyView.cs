using UnityEngine;

// AI 하나의 모습: 늘 제자리에 서 있다(걷기는 없앴다, §26 H12). 녹음기가 붙으면 주인의 녹음 목소리를 5~20초 간격으로 3D로 낸다(DecoyVoice).
// 괴물에게 잡히면 그 괴물의 GrabKill 동작(2.63초)에 맞춰 Grab_Socket에 붙어 눌리고 부서진 뒤, 방장이 상태에서 지운다(§26 H6).
public class DecoyView : MonoBehaviour, IInteractable, IInteractionLabel
{
    private const float VoiceCheckSeconds = 0.25f;
    private const float BodyCenterHeight = 0.95f;

    private EscapeManager manager;
    private int id;
    private AudioSource voice;
    private double playingStart = -1;
    private float nextVoiceCheck;
    private bool registered;
    private bool caught;
    private bool crushed;
    private bool squashPlayed;
    private bool grabStarted;
    private Vector3 originalScale;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => 2.6f;

    public void Init(EscapeManager owner, int decoyId)
    {
        manager = owner;
        id = decoyId;
        originalScale = transform.localScale;
        registered = true;
        InteractableRegistry.Register(this);
    }

    public void Apply(EscapeState.Decoy d)
    {
        if (caught) return;
        transform.position = manager.SnapToGround(d.Pos + Vector3.up);
        transform.rotation = Quaternion.Euler(0f, d.Yaw, 0f);
    }

    public void Tick(EscapeState.Decoy d, double now)
    {
        if (d.Caught) { TickCaught(d); return; }
        if (d.VoiceAt <= 0 || Time.unscaledTime < nextVoiceCheck) return;
        nextVoiceCheck = Time.unscaledTime + VoiceCheckSeconds;
        AudioClip clip = VoiceStore.ClipFor(d.Owner);
        if (clip == null) return;
        if (!DecoyVoice.TryCurrentPlay(d.VoiceAt, d.Seed, clip.length, now, out double start) || start == playingStart) return;
        float offset = (float)(now - start);
        if (offset >= clip.length) return; // 지난 재생은 건너뛴다(늦게 들어온 화면)
        playingStart = start;
        EnsureVoice();
        voice.clip = clip;
        voice.time = Mathf.Clamp(offset, 0f, clip.length - 0.01f);
        voice.Play();
    }

    private void EnsureVoice()
    {
        if (voice != null) return;
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.maxDistance = 30f;
        voice.loop = false;
        voice.playOnAwake = false;
    }

    // 잡힘: 잡은 괴물의 손(Grab_Socket)을 따라가며 GrabKill 진행도대로 눌리고 사라진다. 잡은 괴물 본인 화면은 여기서 동작을 시작한다.
    private void TickCaught(EscapeState.Decoy d)
    {
        if (!caught)
        {
            caught = true;
            if (voice != null) voice.Stop();
            if (registered) { registered = false; InteractableRegistry.Unregister(this); }
            foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false; // 괴물 손에 들린 동안 부딪히지 않는다
        }
        MonsterController monster = FindMonster(d.CaughtBy);
        if (monster == null || monster.GrabSocket == null) return;
        if (!grabStarted)
        {
            grabStarted = true;
            if (monster.IsLocallyControlled && !monster.IsGrabKilling) monster.PlayGrabKill(); // 동작 상태는 괴물 동기화로 모두에게 간다
        }
        if (crushed) return;
        float progress = monster.GrabKillProgress;
        if (progress >= monster.GrabKillCrushNormalizedTime) { Crush(); return; }
        if (!squashPlayed && progress >= monster.GrabKillSquashStart)
        {
            squashPlayed = true;
            GameAudio.PlayAt(SoundId.MonsterSquash, transform.position);
        }
        Vector3 scale = CookieLifeStatePresenter.CrushScale(monster, progress);
        Transform socket = monster.GrabSocket;
        Vector3 toMonster = Vector3.ProjectOnPlane(monster.transform.position - socket.position, Vector3.up);
        Quaternion facing = toMonster.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toMonster) : transform.rotation;
        transform.localScale = Vector3.Scale(originalScale, scale);
        transform.SetPositionAndRotation(socket.position - Vector3.up * (BodyCenterHeight * scale.y), facing);
    }

    private static MonsterController FindMonster(int actor)
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is MonsterController m && CharacterRegistry.IsAlive(c) && c.View != null && c.View.Owner != null && c.View.Owner.ActorNumber == actor) return m;
        return null;
    }

    private void Crush()
    {
        crushed = true;
        GameAudio.PlayAt(SoundId.CookieCrumble, transform.position + Vector3.up * 0.5f);
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
    }

    public void Vanish()
    {
        if (!crushed) GameAudio.PlayAt(SoundId.CookieCrumble, transform.position + Vector3.up);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (registered) InteractableRegistry.Unregister(this);
    }

    // 괴물만 잡을 수 있다(쿠키에게는 아무 반응이 없어 진짜와 구별되지 않는다).
    public bool CanInteract(IGameCharacter character) => !caught && character.Role == CharacterRole.Monster && EscapeManager.ActionsAllowed
        && !(character is MonsterController m && m.IsGrabKilling);
    public void Interact(IGameCharacter character) => manager.Request(EscapeOp.DecoyCatch, id);
    public string GetLabel(IGameCharacter viewer) => EscapeTextsSO.Current.decoyCatch;
}
