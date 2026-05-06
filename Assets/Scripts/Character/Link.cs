using UnityEngine;
using System.Collections;

public class Link : MonoBehaviour
{
    public Transform currentTransform;

    public Vector3 nextPosition;
    public Quaternion nextRotation;

    public GameObject customer;

    void Start()
    {
        currentTransform = this.transform;
    }

    public bool IsLastNode() { return currentTransform == null; }

    public void MoveToNext()
    {
        StartCoroutine(MoveToPoint(nextPosition, nextRotation, 1.0f));
    }

    public void MoveToCounter(Transform counterTransform)
    {
        if (customer != null)
        {
            StartCoroutine(MoveToPoint(counterTransform.position, counterTransform.rotation, 1.0f));
        }
    }

    public void MoveToExitPoint()
    {
        this.transform.position = new Vector3(10.0f, 2.0f, 8.0f);
    }

    IEnumerator MoveToPoint(Vector3 target, Quaternion targetRotation, float duration)
    {
        Animator anim = customer.GetComponent<Animator>();
        anim.SetBool("can_walk", true);
        Vector3 start = this.transform.position;
        Quaternion startRotation = this.transform.rotation;

        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            this.transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            this.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        anim.SetBool("can_walk", false);
    }
}
