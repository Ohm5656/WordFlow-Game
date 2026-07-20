using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets a word-assembly group resize its authored children from the RectTransform Width/Height
/// fields in edit mode. Runtime puzzle motion still reads the already-authored child transforms.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class WordAssemblyResizeHandle : MonoBehaviour
{
    private const float Epsilon = 0.01f;

    [SerializeField, HideInInspector] private Vector2 referenceSize = new Vector2(100f, 100f);
    [SerializeField, HideInInspector] private ChildRectReference[] childReferences = Array.Empty<ChildRectReference>();
    [SerializeField, HideInInspector] private Vector2 lastAppliedSize;

    private RectTransform rectTransform;
    private bool applying;

    private void OnEnable()
    {
        CacheRectTransform();
        if (Application.isPlaying)
        {
            return;
        }

        if (!HasUsableReference())
        {
            CaptureCurrentLayoutAsReference();
        }

        lastAppliedSize = GetCurrentSize();
    }

    private void OnValidate()
    {
        if (Application.isPlaying || applying)
        {
            return;
        }

        CacheRectTransform();
        if (!HasUsableReference())
        {
            CaptureCurrentLayoutAsReference();
        }

        ApplyCurrentSizeToChildren(true);
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            return;
        }

        CacheRectTransform();
        ApplyCurrentSizeToChildren(false);
    }

    [ContextMenu("Capture Current Layout as Reference")]
    public void CaptureCurrentLayoutAsReference()
    {
        CacheRectTransform();
        if (rectTransform == null)
        {
            return;
        }

        referenceSize = GetCurrentSize();
        referenceSize.x = Mathf.Max(Epsilon, Mathf.Abs(referenceSize.x));
        referenceSize.y = Mathf.Max(Epsilon, Mathf.Abs(referenceSize.y));

        List<ChildRectReference> references = new List<ChildRectReference>();
        for (int i = 0; i < rectTransform.childCount; i++)
        {
            if (rectTransform.GetChild(i) is RectTransform child)
            {
                references.Add(ChildRectReference.Capture(child));
            }
        }

        childReferences = references.ToArray();
        lastAppliedSize = GetCurrentSize();
        MarkDirty(rectTransform);
    }

    private void CacheRectTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }
    }

    private bool HasUsableReference()
    {
        return referenceSize.x > Epsilon
            && referenceSize.y > Epsilon
            && childReferences != null
            && childReferences.Length > 0;
    }

    private Vector2 GetCurrentSize()
    {
        if (rectTransform == null)
        {
            return Vector2.zero;
        }

        Vector2 size = rectTransform.sizeDelta;
        size.x = Mathf.Max(Epsilon, Mathf.Abs(size.x));
        size.y = Mathf.Max(Epsilon, Mathf.Abs(size.y));
        return size;
    }

    private void ApplyCurrentSizeToChildren(bool force)
    {
        if (rectTransform == null || !HasUsableReference())
        {
            return;
        }

        Vector2 currentSize = GetCurrentSize();
        if (!force && Approximately(currentSize, lastAppliedSize))
        {
            return;
        }

        float xScale = currentSize.x / referenceSize.x;
        float yScale = currentSize.y / referenceSize.y;

        applying = true;
        for (int i = 0; i < childReferences.Length; i++)
        {
            childReferences[i].Apply(xScale, yScale);
        }

        lastAppliedSize = currentSize;
        applying = false;
        MarkDirty(rectTransform);
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) <= Epsilon && Mathf.Abs(a.y - b.y) <= Epsilon;
    }

    private static void MarkDirty(UnityEngine.Object target)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && target != null)
        {
            UnityEditor.EditorUtility.SetDirty(target);
        }
#endif
    }

    [Serializable]
    private sealed class ChildRectReference
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 anchorMin;
        [SerializeField] private Vector2 anchorMax;
        [SerializeField] private Vector2 anchoredPosition;
        [SerializeField] private Vector2 sizeDelta;
        [SerializeField] private Vector2 pivot;
        [SerializeField] private Vector3 localScale;

        public static ChildRectReference Capture(RectTransform target)
        {
            return new ChildRectReference
            {
                target = target,
                anchorMin = target.anchorMin,
                anchorMax = target.anchorMax,
                anchoredPosition = target.anchoredPosition,
                sizeDelta = target.sizeDelta,
                pivot = target.pivot,
                localScale = target.localScale
            };
        }

        public void Apply(float xScale, float yScale)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.pivot = pivot;
            target.anchoredPosition = new Vector2(anchoredPosition.x * xScale, anchoredPosition.y * yScale);
            target.sizeDelta = new Vector2(sizeDelta.x * xScale, sizeDelta.y * yScale);
            target.localScale = localScale;
            MarkDirty(target);
        }
    }
}
