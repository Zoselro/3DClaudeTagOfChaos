using UnityEngine;

// 괴물 소음폭탄 하나의 모습(Request1009Plan.md §25.5 K) — 괴물에게만 보이고, 놓은 괴물은 E로 치운다(치워도 20초 쿨타임).
public class NoiseBombView : MonoBehaviour, IInteractable, IInteractionLabel
{
    private EscapeManager manager;
    private EscapeState.Bomb bomb;
    private Renderer blink;

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => 2.2f;

    public static NoiseBombView Create(EscapeManager owner, EscapeState.Bomb data, Transform parent)
    {
        var go = new GameObject("NoiseBomb_" + data.Id);
        go.transform.SetParent(parent, false);
        go.transform.position = data.Pos;
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = Vector3.up * 0.25f;
        body.transform.localScale = Vector3.one * 0.5f;
        EscapeVisuals.Tint(body, new Color(0.15f, 0.12f, 0.15f), 0f);
        var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(light.GetComponent<Collider>());
        light.transform.SetParent(go.transform, false);
        light.transform.localPosition = Vector3.up * 0.55f;
        light.transform.localScale = Vector3.one * 0.14f;
        EscapeVisuals.Tint(light, new Color(1f, 0.1f, 0.1f), 2.5f);
        var view = go.AddComponent<NoiseBombView>();
        view.manager = owner;
        view.bomb = data;
        view.blink = light.GetComponent<Renderer>();
        InteractableRegistry.Register(view);
        return view;
    }

    private void Update()
    {
        if (blink != null) blink.enabled = Mathf.Repeat(Time.time, 1f) < 0.5f;
    }

    private void OnDestroy() => InteractableRegistry.Unregister(this);

    public bool CanInteract(IGameCharacter character) =>
        character.Role == CharacterRole.Monster && character.View != null && character.View.Owner != null && character.View.Owner.ActorNumber == bomb.Owner;
    public void Interact(IGameCharacter character) => manager.Request(EscapeOp.RemoveBomb, bomb.Id);
    public string GetLabel(IGameCharacter viewer) => EscapeTextsSO.Current.removeBomb;
}
