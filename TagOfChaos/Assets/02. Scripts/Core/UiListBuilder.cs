using System.Collections.Generic;
using UnityEngine;

// 데이터 개수만큼 UI 항목을 맞추는 공용 헬퍼(research.md §12 E4). 씬에 이미 배치된 항목(컨테이너의 직계 자식)은
// 순서대로 재사용하고, 모자라면 템플릿을 복제하고, 남으면 제거한다 — 예전에는 색 스와치 10개·스킨 버튼 3개를 씬에
// 손으로 배치해 팔레트·스킨 수를 바꾸면 UI가 따라가지 않았다. 레이아웃은 컨테이너의 LayoutGroup이 맡는다.
public static class UiListBuilder
{
    public static List<T> Sync<T>(Transform container, T template, int count) where T : Component
    {
        var items = new List<T>(count);
        if (container == null || template == null) return items;

        var existing = new List<T>();
        for (int i = 0; i < container.childCount; i++)
        {
            var item = container.GetChild(i).GetComponent<T>();
            if (item != null) existing.Add(item);
        }

        for (int i = 0; i < count; i++)
            items.Add(i < existing.Count ? existing[i] : Object.Instantiate(template, container));

        for (int i = count; i < existing.Count; i++)
        {
            if (existing[i] == template) existing[i].gameObject.SetActive(false); // 템플릿은 지우지 않고 숨긴다
            else Object.Destroy(existing[i].gameObject);
        }
        return items;
    }
}
