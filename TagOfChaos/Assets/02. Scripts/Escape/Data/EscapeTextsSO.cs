using UnityEngine;

// 탈출 모드 화면 문구(Resources/Escape/EscapeTexts). 코드에 한글을 쓰지 않으므로 모든 문구는 이 에셋에 입력한다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Texts", fileName = "EscapeTexts")]
public class EscapeTextsSO : ScriptableObject
{
    public const string ResourcePath = "Escape/EscapeTexts";

    [Header("Chest labels (§1.4)")]
    public string materialChest = "Material Chest";
    public string spyChest = "Spy Chest";
    public string locked = "Locked";

    [Header("Interaction labels")]
    public string escapeDevice = "Escape";
    public string spyRocket = "Rocket";
    public string cannotEnter = "Cannot enter";
    public string inventoryFull = "Inventory is full";
    public string handsFull = "Put down what you are holding first";

    [Header("Toasts (§1.10, §1.11)")]
    public string spyCaught = "The spy was caught";
    [Tooltip("스파이가 장치에서 재료를 뺀 뒤 n초 후(괴물 제외). 받침 있을 때. {0} = 재료 이름")]
    public string stolenFromDeviceWithFinal = "The spy took the {0}!";
    [Tooltip("받침 없을 때. {0} = 재료 이름")]
    public string stolenFromDeviceNoFinal = "The spy took the {0}!";
    [Tooltip("쓰지 않음(2026-10-03 — 로켓에 끼울 때 알림은 없어졌다). 받침 있을 때. {0} = 재료 이름")]
    public string stolenToRocketWithFinal = "The spy stole {0} and put it in the rocket";
    [Tooltip("받침 없을 때. {0} = 재료 이름")]
    public string stolenToRocketNoFinal = "The spy stole {0} and put it in the rocket";
    public string youAreSpy = "You are the SPY";
    public string spyBadge = "SPY";
    public string deviceComplete = "The escape is open!";
    public string witchComing = "The witch is turning around...";

    [Header("Timers")]
    public string timeAttackFormat = "{0:00}:{1:00}";
    [Tooltip("{0} = 탄 쿠키 수, {1} = 살아 있는 쿠키 수(스파이 제외)")]
    public string boardingFormat = "Boarded {0} / {1}";
    public string departing = "Departing!";
    public string boardingLocked = "Wait until it is ready";

    private static EscapeTextsSO cached;

    public static EscapeTextsSO Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<EscapeTextsSO>(ResourcePath);
            if (cached == null) cached = CreateInstance<EscapeTextsSO>();
            return cached;
        }
    }
}
