using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 아이템이 지금 어디 있는지(EscapePlan.md §5.2).
public enum ItemLocation : byte
{
    Chest,  // A = 상자 번호
    Held,   // A = 액터 번호, B = 인벤토리 칸(0~3)
    Ground, // Pos = 바닥 위치
    Device, // A = 탈출 장치 칸 번호
    Gone,   // 없어짐(도구 횟수 소진 등)
}

// 탈출 모드 한 판의 상태. 방장이 가진 원본을 Room Prop(NetKeys.EscapeState, byte[])로 통째로 쓰고, 모두가 읽는다.
// 문(DoorStates)과 같은 "방장만 쓴다" 방식이다. 재료·상자에는 PhotonView가 없다(최적화, §5.2).
public sealed class EscapeState
{
    public const byte FormatVersion = 1;

    public struct Item
    {
        public string Id;
        public ItemLocation Loc;
        public int A;
        public int B;
        public Vector3 Pos;
        public int Charges;
        public bool StolenFromDevice; // 장치에서 훔친 재료(로켓에 끼울 때 알림, D40)
    }

    public struct Chest
    {
        public int Anchor;  // 씬의 CHEST_Slot 번호
        public bool Spy;
        public bool Opened;
    }

    public struct Slot
    {
        public string Label;
        public string[] Accepts;
        public bool Prefilled; // 처음부터 채워진 칸(필요 없는 칸) — 표시만 하고 셈에서 뺀다
        public int ItemIndex;  // 끼워진 아이템(-1 = 비어 있음). 로켓 칸은 쓰지 않는다
        public bool RocketFilled;

        public bool Filled => Prefilled || ItemIndex >= 0 || RocketFilled;
        public bool Allows(string itemId) => Accepts != null && System.Array.IndexOf(Accepts, itemId) >= 0;
    }

    public int RequiredCount;
    public readonly List<Item> Items = new List<Item>();
    public readonly List<Chest> Chests = new List<Chest>();
    public readonly List<Slot> DeviceSlots = new List<Slot>();
    public readonly List<Slot> RocketSlots = new List<Slot>();
    public readonly List<int> Escaped = new List<int>(); // 탈출구로 나간 쿠키
    public readonly List<int> Boarded = new List<int>(); // 로켓에 탄 스파이

    public bool DeviceComplete
    {
        get
        {
            if (DeviceSlots.Count == 0) return false;
            foreach (Slot s in DeviceSlots) if (!s.Filled) return false;
            return true;
        }
    }

    public bool RocketComplete
    {
        get
        {
            if (RocketSlots.Count == 0) return false;
            foreach (Slot s in RocketSlots) if (!s.Filled) return false;
            return true;
        }
    }

    // ---------------- queries ----------------

    public int HeldItemAt(int actor, int slot)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Loc == ItemLocation.Held && Items[i].A == actor && Items[i].B == slot) return i;
        return -1;
    }

    public int HeldCount(int actor)
    {
        int n = 0;
        foreach (Item it in Items) if (it.Loc == ItemLocation.Held && it.A == actor) n++;
        return n;
    }

    public int ItemInChest(int chest)
    {
        for (int i = 0; i < Items.Count; i++)
            if (Items[i].Loc == ItemLocation.Chest && Items[i].A == chest) return i;
        return -1;
    }

    // ---------------- serialization ----------------

    public byte[] Encode()
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(FormatVersion);
            w.Write(RequiredCount);
            w.Write(Items.Count);
            foreach (Item it in Items)
            {
                w.Write(it.Id ?? string.Empty);
                w.Write((byte)it.Loc);
                w.Write(it.A);
                w.Write(it.B);
                w.Write(it.Pos.x); w.Write(it.Pos.y); w.Write(it.Pos.z);
                w.Write((byte)Mathf.Clamp(it.Charges, 0, 255));
                w.Write(it.StolenFromDevice);
            }
            w.Write(Chests.Count);
            foreach (Chest c in Chests)
            {
                w.Write((byte)c.Anchor);
                w.Write(c.Spy);
                w.Write(c.Opened);
            }
            WriteSlots(w, DeviceSlots);
            WriteSlots(w, RocketSlots);
            WriteInts(w, Escaped);
            WriteInts(w, Boarded);
            w.Flush();
            return ms.ToArray();
        }
    }

    public static EscapeState Decode(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadByte() != FormatVersion) return null;
                var s = new EscapeState { RequiredCount = r.ReadInt32() };
                int items = r.ReadInt32();
                for (int i = 0; i < items; i++)
                {
                    s.Items.Add(new Item
                    {
                        Id = r.ReadString(),
                        Loc = (ItemLocation)r.ReadByte(),
                        A = r.ReadInt32(),
                        B = r.ReadInt32(),
                        Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                        Charges = r.ReadByte(),
                        StolenFromDevice = r.ReadBoolean(),
                    });
                }
                int chests = r.ReadInt32();
                for (int i = 0; i < chests; i++)
                    s.Chests.Add(new Chest { Anchor = r.ReadByte(), Spy = r.ReadBoolean(), Opened = r.ReadBoolean() });
                ReadSlots(r, s.DeviceSlots);
                ReadSlots(r, s.RocketSlots);
                ReadInts(r, s.Escaped);
                ReadInts(r, s.Boarded);
                return s;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EscapeState] Decode failed: {e.Message}");
            return null;
        }
    }

    public EscapeState Clone() => Decode(Encode());

    private static void WriteSlots(BinaryWriter w, List<Slot> slots)
    {
        w.Write(slots.Count);
        foreach (Slot s in slots)
        {
            w.Write(s.Label ?? string.Empty);
            string[] accepts = s.Accepts ?? new string[0];
            w.Write(accepts.Length);
            foreach (string a in accepts) w.Write(a ?? string.Empty);
            w.Write(s.Prefilled);
            w.Write(s.ItemIndex);
            w.Write(s.RocketFilled);
        }
    }

    private static void ReadSlots(BinaryReader r, List<Slot> slots)
    {
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            var s = new Slot { Label = r.ReadString() };
            int a = r.ReadInt32();
            s.Accepts = new string[a];
            for (int k = 0; k < a; k++) s.Accepts[k] = r.ReadString();
            s.Prefilled = r.ReadBoolean();
            s.ItemIndex = r.ReadInt32();
            s.RocketFilled = r.ReadBoolean();
            slots.Add(s);
        }
    }

    private static void WriteInts(BinaryWriter w, List<int> values)
    {
        w.Write(values.Count);
        foreach (int v in values) w.Write(v);
    }

    private static void ReadInts(BinaryReader r, List<int> values)
    {
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++) values.Add(r.ReadInt32());
    }
}
