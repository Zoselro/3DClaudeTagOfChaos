// 맵 밖으로 떨어졌을 때 스폰 지점으로 돌아갈 수 있는 캐릭터(쿠키·괴물 공통, Bug-fix-plan.md §30.4).
// VoidKillZone·FallGuard는 캐릭터 종류를 몰라도 이 계약만으로 처리한다 — 예전에는 둘 다 쿠키(HideOrSeekPlayer)만
// 알아서 괴물이 떨어지면 영원히 추락했다. 새 캐릭터 종류가 생겨도 이 인터페이스만 구현하면 된다.
public interface IRespawnable
{
    // 물리 소유자(PhotonView.IsMine)만 순간이동을 수행한다 — 원격 복사본은 동기화로 따라온다.
    bool IsLocallyControlled { get; }

    void RespawnToSpawnPoint();
}
