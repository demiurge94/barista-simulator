using UnityEngine;
using System.Collections;

public class Link : MonoBehaviour
{
    public Transform currentPosition;
    public Vector3 nextPosition;
    public GameObject customer;

    void Start()
    {
        currentPosition = this.transform;
    }

    public bool IsLastNode() { return currentPosition == null; }

    public void MoveToNext()
    {
        StartCoroutine(MoveToPoint(nextPosition, 3.0f));
    }

    public void MoveToCounter(Vector3 counterPosition)
    {
        if (customer != null)
        {
            StartCoroutine(MoveToPoint(counterPosition, 3.0f));
        }
    }

    public void MoveToExitPoint()
    {
        this.transform.position = new Vector3(10.0f, 2.0f, 8.0f);
    }


    IEnumerator MoveToPoint(Vector3 target, float duration)
    {
        Animator anim = customer.GetComponent<Animator>();
        anim.SetBool("can_walk", true);
        Vector3 start = this.transform.position;

        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            this.transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        anim.SetBool("can_walk", false);


    }
}
