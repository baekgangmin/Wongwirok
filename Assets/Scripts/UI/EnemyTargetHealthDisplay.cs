using UnityEngine;

public class EnemyTargetHealthDisplay : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private HealthBarUI healthBarUI;
    [SerializeField] private float hideDelay = 4f;

    private float hideTimer;

    private void Start()
    {
        if (panel != null)
            panel.SetActive(false);

        foreach (var enemyHealth in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            enemyHealth.OnDamaged += _ => ShowFor(enemyHealth);
        }
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf)
            return;

        hideTimer -= Time.deltaTime;
        if (hideTimer <= 0f)
            panel.SetActive(false);
    }

    private void ShowFor(IHealth target)
    {
        if (panel != null)
            panel.SetActive(true);

        healthBarUI.Initialize(target);
        hideTimer = hideDelay;
    }
}
