using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Image ghostFillImage;
    [SerializeField] private float ghostCatchUpSpeed = 0.4f;
    [SerializeField] private float ghostHoldTime = 0.4f;
    [SerializeField] private MonoBehaviour healthSource;

    private IHealth health;
    private float ghostRatio;
    private float ghostHoldTimer;

    private void Awake()
    {
        if (healthSource is IHealth target)
            Subscribe(target);
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (ghostFillImage == null)
            return;

        if (ghostHoldTimer > 0f)
        {
            ghostHoldTimer -= Time.deltaTime;
            return;
        }

        float targetRatio = fillImage != null ? GetRatio(fillImage) : 0f;
        if (ghostRatio > targetRatio)
        {
            ghostRatio = Mathf.MoveTowards(ghostRatio, targetRatio, ghostCatchUpSpeed * Time.deltaTime);
            SetGhostRatio(ghostRatio);
        }
    }

    public void Initialize(IHealth target)
    {
        Subscribe(target);
        Refresh();
        SnapGhost();
    }

    private void Subscribe(IHealth target)
    {
        if (health != null)
        {
            health.OnDamaged -= HandleHealthChanged;
            health.OnDeath -= HandleHealthChanged;
        }

        health = target;

        if (health != null)
        {
            health.OnDamaged += HandleHealthChanged;
            health.OnDeath += HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float amount) => Refresh();
    private void HandleHealthChanged() => Refresh();

    private void Refresh()
    {
        if (fillImage == null || health == null || health.MaxHealth <= 0f)
            return;

        float oldRatio = GetRatio(fillImage);
        float ratio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
        SetRatio(fillImage, ratio);

        if (ghostFillImage == null)
            return;

        if (ratio < oldRatio)
        {
            // took damage: ghost holds at the pre-damage ratio, then catches down
            ghostRatio = Mathf.Max(ghostRatio, oldRatio);
            ghostHoldTimer = ghostHoldTime;
            SetGhostRatio(ghostRatio);
        }
        else
        {
            // healed or reset: snap ghost up immediately, no lag on gains
            SnapGhost();
        }
    }

    private void SnapGhost()
    {
        if (fillImage == null)
            return;

        ghostRatio = GetRatio(fillImage);
        ghostHoldTimer = 0f;
        SetGhostRatio(ghostRatio);
    }

    private void SetGhostRatio(float ratio)
    {
        if (ghostFillImage == null)
            return;

        SetRatio(ghostFillImage, ratio);
    }

    // 붓 획 그림(Filled)은 왼쪽부터 잘라 보여 주고, 단색 막대는 예전처럼 폭을 줄인다 (그림이 찌그러지지 않게)
    private static float GetRatio(Image img) =>
        img.type == Image.Type.Filled ? img.fillAmount : img.rectTransform.anchorMax.x;

    private static void SetRatio(Image img, float ratio)
    {
        if (img.type == Image.Type.Filled) { img.fillAmount = ratio; return; }
        RectTransform rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(ratio, 1f);
    }
}
