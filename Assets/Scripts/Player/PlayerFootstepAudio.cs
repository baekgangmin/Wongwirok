using UnityEngine;

// Starter Assets 이동 애니메이션에 들어 있는 발소리 이벤트(OnFootstep / OnLand)를 받아 소리를 재생한다.
// Animator가 붙은 오브젝트(Visual_KoreanWarrior)에 붙여야 이벤트를 받을 수 있다.
public class PlayerFootstepAudio : MonoBehaviour
{
    [Header("발소리")]
    [SerializeField] private bool playSounds = true;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioClip landClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

    // 블렌드 중 비중이 낮은 동작의 이벤트까지 울려 소리가 겹치는 것을 막는다.
    private const float MinClipWeight = 0.5f;

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (!playSounds || footstepClips == null || footstepClips.Length == 0)
            return;
        if (animationEvent.animatorClipInfo.weight < MinClipWeight)
            return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (!playSounds || landClip == null)
            return;
        if (animationEvent.animatorClipInfo.weight < MinClipWeight)
            return;

        AudioSource.PlayClipAtPoint(landClip, transform.position, volume);
    }
}
