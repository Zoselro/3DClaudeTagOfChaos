using System.Collections.Generic;
using UnityEngine;

// 모든 아이템과 맵별 레시피 목록(Resources/Escape/EscapeCatalog). 네트워크로 받은 아이템 ID를 ItemSO로 바꿀 때 쓴다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Catalog", fileName = "EscapeCatalog")]
public class EscapeCatalogSO : ScriptableObject
{
    public const string ResourcePath = "Escape/EscapeCatalog";

    [SerializeField] private ItemSO[] items = new ItemSO[0];
    [SerializeField] private EscapeRecipeSO[] recipes = new EscapeRecipeSO[0];
    [Tooltip("일반 상자에 넣는 뿅망치.")]
    [SerializeField] private ItemSO hammer;
    [Tooltip("스파이 1명당 스파이 상자 한 쌍에 넣는 도구(스턴건, 물풍선).")]
    [SerializeField] private ItemSO[] spyChestTools = new ItemSO[0];
    [Tooltip("일반 상자에 넣는 뿅망치 수 = 쿠키 수 × 이 비율(올림).")]
    [SerializeField, Range(0f, 1f)] private float hammersPerCookie = 0.5f;

    [Header("Shared models (EscapeVisualPlan.md §3)")]
    [Tooltip("탈것에 타는 순간·좌석에 앉은 쿠키 인형")]
    [SerializeField] private GameObject passengerModel;
    [Tooltip("괴물 승리 결과의 쿠키 유리병(3D)")]
    [SerializeField] private GameObject jarModel;
    [Tooltip("유리병 안에 쌓이는 작은 쿠키")]
    [SerializeField] private GameObject jarCookieModel;

    private Dictionary<string, ItemSO> byId;
    private static EscapeCatalogSO cached;

    public IReadOnlyList<ItemSO> Items => items;
    public ItemSO Hammer => hammer;
    public IReadOnlyList<ItemSO> SpyChestTools => spyChestTools;
    public float HammersPerCookie => hammersPerCookie;
    public GameObject PassengerModel => passengerModel;
    public GameObject JarModel => jarModel;
    public GameObject JarCookieModel => jarCookieModel;

    public static EscapeCatalogSO Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<EscapeCatalogSO>(ResourcePath);
            return cached;
        }
    }

    public ItemSO Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId == null)
        {
            byId = new Dictionary<string, ItemSO>();
            foreach (ItemSO item in items)
                if (item != null && !string.IsNullOrEmpty(item.ItemId)) byId[item.ItemId] = item;
        }
        byId.TryGetValue(id, out ItemSO found);
        return found;
    }

    public EscapeRecipeSO RecipeFor(string mapName)
    {
        foreach (EscapeRecipeSO r in recipes)
            if (r != null && r.MapName == mapName) return r;
        return null;
    }

#if UNITY_EDITOR
    public void EditorSetup(ItemSO[] allItems, EscapeRecipeSO[] allRecipes, ItemSO hammerItem, ItemSO[] spyTools)
    {
        items = allItems;
        recipes = allRecipes;
        hammer = hammerItem;
        spyChestTools = spyTools;
        byId = null;
    }
#endif
}
