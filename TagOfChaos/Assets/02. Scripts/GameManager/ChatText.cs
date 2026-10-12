using System;
using System.Collections.Generic;

// 채팅 문자열 정리·도배 제한(AuditFixPlan.md S-11). 예전에는 보낸 쪽이 색 태그까지 붙인 완성 문자열을 보내고 받는 쪽은 그대로
// 표시해, 조작된 클라이언트가 남의 닉네임·시스템 문구를 흉내 내거나 <size=999> 같은 리치 텍스트로 화면을 덮을 수 있었다.
// 이제 보낸 쪽은 본문만 보내고, 받는 쪽이 보낸 사람(PhotonMessageInfo.Sender)의 닉네임으로 직접 꾸민다.
public static class ChatText
{
    public const int MaxLength = 120;
    private const string NoParseOpen = "<noparse>";
    private const string NoParseClose = "</noparse>";

    // 리치 텍스트를 글자 그대로 보이게 감싼다. 닫는 태그를 끼워 넣어 감싸기를 빠져나오지 못하게 먼저 지운다.
    public static string Sanitize(string raw, int maxLength = MaxLength)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string s = raw.Replace('\n', ' ').Replace('\r', ' ').Trim();
        int i;
        while ((i = s.IndexOf(NoParseClose, StringComparison.OrdinalIgnoreCase)) >= 0)
            s = s.Remove(i, NoParseClose.Length);
        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s.Length == 0 ? string.Empty : NoParseOpen + s + NoParseClose;
    }
}

// 보낸 사람별 도배 제한: windowSeconds 안에 maxPerWindow개까지만 받는다.
public class ChatRateLimiter
{
    private readonly int maxPerWindow;
    private readonly float windowSeconds;
    private readonly Dictionary<int, float> windowStart = new Dictionary<int, float>();
    private readonly Dictionary<int, int> counts = new Dictionary<int, int>();

    public ChatRateLimiter(int maxPerWindow = 2, float windowSeconds = 1f)
    {
        this.maxPerWindow = maxPerWindow;
        this.windowSeconds = windowSeconds;
    }

    public bool Allow(int sender, float now)
    {
        if (!windowStart.TryGetValue(sender, out float start) || now - start >= windowSeconds)
        {
            windowStart[sender] = now;
            counts[sender] = 1;
            return true;
        }
        int c = counts[sender];
        if (c >= maxPerWindow) return false;
        counts[sender] = c + 1;
        return true;
    }

    public void Clear()
    {
        windowStart.Clear();
        counts.Clear();
    }
}
