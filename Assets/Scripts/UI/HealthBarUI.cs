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

        float targetRatio = fillImage != null ? fillImage.rectTransform.anchorMax.x : 0f;
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

        float oldRatio = fillImage.rectTransform.anchorMax.x;
        float ratio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(ratio, 1f);

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

        ghostRatio = fillImage.rectTransform.anchorMax.x;
        ghostHoldTimer = 0f;
        SetGhostRatio(ghostRatio);
    }

    private void SetGhostRatio(float ratio)
    {
        if (ghostFillImage == null)
            return;

        RectTransform ghostRect = ghostFillImage.rectTransform;
        ghostRect.anchorMin = new Vector2(0f, 0f);
        ghostRect.anchorMax = new Vector2(ratio, 1f);
    }
}
