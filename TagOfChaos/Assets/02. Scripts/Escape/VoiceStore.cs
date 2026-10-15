using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 접착 목소리 녹음기(Request1009Plan.md §25.3 Q6, §25.3.1 R2): 게임 시작 전(대기실) 5초 동안 내 목소리를 녹음해 두고,
// 판이 시작되면 방 안 사람들에게 한 번 보낸다. 녹음기가 붙은 내 AI가 이 목소리를 3D로 낸다. 녹음하지 않았으면 기본 목소리(합성).
// 8 kHz 한 채널을 8비트 μ-law로 줄여 약 40 KB, 8 KB 조각으로 보낸다(Photon 이벤트 VoiceClip). 받은 목소리는 방 안에서만, 판이 바뀌면 버린다.
public static class VoiceStore
{
    public const int SampleRate = 8000;
    public const int Seconds = 5;
    private static float[] localSamples;
    private static readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
    private static AudioClip defaultClip;

    public static bool HasLocal => localSamples != null && localSamples.Length > 0;
    public static float[] LocalSamples => localSamples;

    public static void SetLocal(float[] samples)
    {
        localSamples = samples;
        if (PhotonNetwork.LocalPlayer != null) clips.Remove(PhotonNetwork.LocalPlayer.ActorNumber);
    }

    public static void SetRemote(int actor, float[] samples)
    {
        if (samples == null || samples.Length == 0) return;
        var clip = AudioClip.Create("Voice_" + actor, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        clips[actor] = clip;
    }

    public static void ClearRemote() => clips.Clear();

    // 그 사람의 목소리(녹음이 없으면 기본 목소리).
    public static AudioClip ClipFor(int actor)
    {
        if (clips.TryGetValue(actor, out AudioClip clip) && clip != null) return clip;
        if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber && HasLocal)
        {
            SetRemote(actor, localSamples);
            return clips[actor];
        }
        return DefaultClip;
    }

    // 기본 목소리: 높낮이가 바뀌는 짧은 "재잘거림"(합성, 1.6초).
    public static AudioClip DefaultClip
    {
        get
        {
            if (defaultClip != null) return defaultClip;
            int n = SampleRate * 16 / 10;
            var data = new float[n];
            var rng = new System.Random(7);
            float phase = 0f;
            for (int syl = 0, i = 0; i < n; syl++)
            {
                int len = SampleRate / 8 + rng.Next(SampleRate / 10);
                float pitch = 220f + (float)rng.NextDouble() * 220f;
                for (int k = 0; k < len && i < n; k++, i++)
                {
                    float env = Mathf.Sin(Mathf.PI * k / len);
                    phase += 2f * Mathf.PI * (pitch + 40f * Mathf.Sin(k * 0.002f)) / SampleRate;
                    data[i] = 0.35f * env * (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f));
                }
                int gap = SampleRate / 25;
                for (int k = 0; k < gap && i < n; k++, i++) data[i] = 0f;
            }
            defaultClip = AudioClip.Create("VoiceDefault", n, 1, SampleRate, false);
            defaultClip.SetData(data, 0);
            return defaultClip;
        }
    }

    // μ-law 8비트(순수 계산, 시험용).
    public static byte Encode(float sample)
    {
        const float Mu = 255f;
        float s = Mathf.Clamp(sample, -1f, 1f);
        float m = Mathf.Sign(s) * Mathf.Log(1f + Mu * Mathf.Abs(s)) / Mathf.Log(1f + Mu);
        return (byte)Mathf.Clamp(Mathf.RoundToInt((m + 1f) * 127.5f), 0, 255);
    }

    public static float Decode(byte value)
    {
        const float Mu = 255f;
        float m = value / 127.5f - 1f;
        return Mathf.Sign(m) * (Mathf.Pow(1f + Mu, Mathf.Abs(m)) - 1f) / Mu;
    }
}

// 목소리 조각 주고받기(NetEventCodes.VoiceClip). 내용: [조각 번호, 조각 수, byte[]]. 받은 쪽은 보낸 사람 번호로 모은다(남의 목소리로 꾸밀 수 없다).
public static class VoiceNet
{
    private const int ChunkBytes = 8000;
    private static readonly Dictionary<int, byte[][]> pending = new Dictionary<int, byte[][]>();

    public static void SendLocalVoice()
    {
        VoiceStore.ClearRemote();
        pending.Clear();
        if (!VoiceStore.HasLocal || !PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
        float[] samples = VoiceStore.LocalSamples;
        var bytes = new byte[samples.Length];
        for (int i = 0; i < samples.Length; i++) bytes[i] = VoiceStore.Encode(samples[i]);
        int total = (bytes.Length + ChunkBytes - 1) / ChunkBytes;
        for (int c = 0; c < total; c++)
        {
            int len = Mathf.Min(ChunkBytes, bytes.Length - c * ChunkBytes);
            var chunk = new byte[len];
            System.Array.Copy(bytes, c * ChunkBytes, chunk, 0, len);
            PhotonNetwork.RaiseEvent(NetEventCodes.VoiceClip, new object[] { c, total, chunk },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }
    }

    public static void Receive(int sender, object data)
    {
        if (!(data is object[] d) || d.Length < 3 || !(d[0] is int index) || !(d[1] is int total) || !(d[2] is byte[] chunk)) return;
        if (total <= 0 || total > 16 || index < 0 || index >= total || chunk.Length > ChunkBytes) return; // 크기 제한(최대 128 KB)
        if (!pending.TryGetValue(sender, out byte[][] parts) || parts.Length != total) pending[sender] = parts = new byte[total][];
        parts[index] = chunk;
        foreach (byte[] p in parts) if (p == null) return;
        int length = 0;
        foreach (byte[] p in parts) length += p.Length;
        var samples = new float[length];
        int k = 0;
        foreach (byte[] p in parts) foreach (byte b in p) samples[k++] = VoiceStore.Decode(b);
        VoiceStore.SetRemote(sender, samples);
        pending.Remove(sender);
    }
}
