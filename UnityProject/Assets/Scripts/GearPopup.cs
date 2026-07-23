using UnityEngine;

// Flies a gear-change popup toward the speed panel, then destroys itself —
// purely cosmetic, unlike ScorePopup/TrickPopup which also apply their
// effect on arrival (the gear itself already changed the instant it did;
// nothing left to "apply").
public class GearPopup : MonoBehaviour
{
    public RectTransform target;

    [SerializeField] private float duration = 1.1f;

    private RectTransform _rect;
    private Vector2 _startPos;
    private float _timer;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _startPos = _rect.anchoredPosition;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / duration);
        float eased = t * t;

        Vector2 targetPos = target != null ? target.anchoredPosition : _startPos;
        _rect.anchoredPosition = Vector2.Lerp(_startPos, targetPos, eased);
        _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);

        if (t >= 1f)
            Destroy(gameObject);
    }
}
