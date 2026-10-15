using UnityEngine;

// AI 하나의 모습: 서 있기 / 녹음기로 걷기 + 목소리.
public class DecoyView : MonoBehaviour, IInteractable, IInteractionLabel
{
    private static readonly int WalkTrigger = Animator.StringToHash(nameof(PlayerMoveState.Walk));
    private static readonly int IdleTrigger = Animator.StringToHash(nameof(PlayerMoveState.Idle));

    private EscapeManager manager;
    private int id;
    private Animator animator;
    private AudioSource voice;
    private bool walking;
    private bool registered;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => 2.6f;

    public void Init(EscapeManager owner, int decoyId)
    {
        manager = owner;
        id = decoyId;
        animator = GetComponentInChildren<Animator>(true);
        registered = true;
        InteractableRegistry.Register(this);
    }

    public void Apply(EscapeState.Decoy d)
    {
        if (d.VoiceAt <= 0)
        {
            transform.position = manager.SnapToGround(d.Pos + Vector3.up);
            transform.rotation = Quaternion.Euler(0f, d.Yaw, 0f);
        }
    }

    public void Tick(EscapeState.Decoy d, double now, Rect map)
    {
        float t = d.VoiceAt > 0 ? (float)(now - d.VoiceAt) : -1f;
        bool nowWalking = t >= 0f;
        if (nowWalking != walking)
        {
            walking = nowWalking;
            if (animator != null) animator.SetTrigger(walking ? WalkTrigger : IdleTrigger);
            SetVoice(walking, d.Owner);
        }
        if (!walking) return;
        Vector3 p = DecoyWalk.PositionAt(d.Pos, d.Seed, t, map);
        transform.position = manager.SnapToGround(new Vector3(p.x, d.Pos.y + 1f, p.z));
        transform.rotation = Quaternion.Euler(0f, DecoyWalk.YawAt(d.Pos, d.Seed, t, map), 0f);
    }

    private void SetVoice(bool on, int owner)
    {
        if (on && voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.maxDistance = 30f;
            voice.loop = true;
        }
        if (voice == null) return;
        if (on) { voice.clip = VoiceStore.ClipFor(owner); voice.Play(); }
        else voice.Stop();
    }

    public void Vanish()
    {
        GameAudio.PlayAt(SoundId.CookieCrumble, transform.position + Vector3.up);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (registered) InteractableRegistry.Unregister(this);
    }

    // 괴물만 잡을 수 있다(쿠키에게는 아무 반응이 없어 진짜와 구별되지 않는다).
    public bool CanInteract(IGameCharacter character) => character.Role == CharacterRole.Monster && EscapeManager.ActionsAllowed;
    public void Interact(IGameCharacter character) => manager.Request(EscapeOp.DecoyCatch, id);
    public string GetLabel(IGameCharacter viewer) => EscapeTextsSO.Current.decoyCatch;
}
