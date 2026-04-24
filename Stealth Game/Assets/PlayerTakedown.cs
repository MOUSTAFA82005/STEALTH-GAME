using System.Collections;
using UnityEngine;

public class PlayerTakedown : MonoBehaviour
{
    public float takedownRange = 2f;
    public float fallDuration = 0.4f;
    public float fallDownOffset = 0.5f;
    public float waitBeforeDisappear = 0.5f;
    public float disappearDuration = 0.5f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryTakedownGuard();
        }
    }

    void TryTakedownGuard()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, takedownRange);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Guard"))
                continue;

            GuardAI guardAI = hit.GetComponent<GuardAI>();
            Rigidbody guardRb = hit.GetComponent<Rigidbody>();
            CapsuleCollider guardCollider = hit.GetComponent<CapsuleCollider>();
            Animator guardAnimator = hit.GetComponentInChildren<Animator>();

            if (guardAI != null)
            {
                if (guardAI.isDead)
                    return;

                guardAI.isDead = true;

                if (KillCounter.Instance != null)
                    KillCounter.Instance.AddKill();
            }

            if (GuardCounterUI.Instance != null)
            {
                GuardCounterUI.Instance.RegisterKill();
            }

            if (guardAnimator != null)
            {
                guardAnimator.SetBool("isRunning", false);
                guardAnimator.speed = 1f;
            }

            if (guardRb != null)
            {
                guardRb.linearVelocity = Vector3.zero;
                guardRb.angularVelocity = Vector3.zero;
                guardRb.isKinematic = true;
            }

            if (guardCollider != null)
            {
                guardCollider.enabled = false;
            }

            StartCoroutine(FallAndDisappear(hit.transform));
            break;
        }
    }

    IEnumerator FallAndDisappear(Transform guard)
    {
        Quaternion startRotation = guard.rotation;
        Quaternion endRotation = Quaternion.Euler(0f, guard.eulerAngles.y, 90f);

        Vector3 startPosition = guard.position;
        Vector3 endPosition = startPosition + Vector3.down * fallDownOffset;

        float time = 0f;

        while (time < fallDuration)
        {
            time += Time.deltaTime;
            float t = time / fallDuration;

            guard.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            guard.position = Vector3.Lerp(startPosition, endPosition, t);

            yield return null;
        }

        guard.rotation = endRotation;
        guard.position = endPosition;

        yield return new WaitForSeconds(waitBeforeDisappear);

        Vector3 startScale = guard.localScale;
        Vector3 endScale = Vector3.zero;

        time = 0f;

        while (time < disappearDuration)
        {
            time += Time.deltaTime;
            float t = time / disappearDuration;
            guard.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        guard.localScale = Vector3.zero;
        guard.gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, takedownRange);
    }
}