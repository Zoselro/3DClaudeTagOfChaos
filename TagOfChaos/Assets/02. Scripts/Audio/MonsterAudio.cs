using UnityEngine;

// 괴물 캐릭터 소리(SoundPlan.md S4). MonsterController가 Awake에서 붙이고 상태 변화(MonsterController.StateChanged)를 듣는다 —
// 본인·원격 모두 같은 상태가 흐르므로 모든 화면에서 난다. 괴물 소리는 크게·멀리(Heavy 곡선, 쿠키에게 경고 — D3).
// - 걷는 동안 무거운 발소리, 촉수 돌진, 잡기(처형 시작). 눌림·가루는 처형 연출(CookieLifeStatePresenter)이 그 순간에 낸다.
public class MonsterAudio : MonoBehaviour
{
    private const float WalkStepInterval = 0.5f;

    private MonsterController monster;
    private float nextStep;

    public void Init(MonsterController owner)
    {
        monster = owner;
        monster.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (monster != null) monster.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(MonsterMoveState from, MonsterMoveState to)
    {
        switch (to)
        {
            case MonsterMoveState.TentacleDash: GameAudio.PlayCharacter(SoundId.MonsterDash, monster); break;
            case MonsterMoveState.GrabKill: GameAudio.PlayCharacter(SoundId.MonsterGrab, monster); break;
            case MonsterMoveState.Walk: nextStep = Time.time; break;
        }
    }

    private void Update()
    {
        if (monster.CurrentMoveState != MonsterMoveState.Walk || Time.time < nextStep) return;
        nextStep = Time.time + WalkStepInterval;
        GameAudio.PlayCharacter(SoundId.MonsterStep, monster);
    }
}
