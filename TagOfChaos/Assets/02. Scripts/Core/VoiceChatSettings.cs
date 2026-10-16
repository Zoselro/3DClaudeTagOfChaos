using System;
using UnityEngine;

// 음성 채팅 말하기 방식(§26 H11·§27 K3): 누르면서 말하기(기본, 키 = KeyBindings.PushToTalk) / 항상 말하기. 이 PC에 저장한다.
public static class VoiceChatSettings
{
    public enum TalkMode { PushToTalk = 0, Always = 1 }

    private const string PrefsKey = "TOC_TalkMode";
    private static TalkMode? mode;

    public static event Action Changed;

    public static TalkMode Mode
    {
        get
        {
            if (mode == null)
            {
                int v = 0;
                try { v = PlayerPrefs.GetInt(PrefsKey, 0); } catch (Exception) { }
                mode = v == 1 ? TalkMode.Always : TalkMode.PushToTalk;
            }
            return mode.Value;
        }
        set
        {
            mode = value;
            try { PlayerPrefs.SetInt(PrefsKey, (int)value); PlayerPrefs.Save(); } catch (Exception) { }
            Changed?.Invoke();
        }
    }

    // 지금 말을 보내야 하는지(녹음 중에는 보내지 않는다 — §26 H11).
    public static bool ShouldTransmit(TalkMode talkMode, bool keyHeld, bool recording) =>
        !recording && (talkMode == TalkMode.Always || keyHeld);
}
