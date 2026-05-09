using UnityEngine;

public class Chain : MonoBehaviour
{
    public Link[] links;
    public Transform counterTransform;
    public Transform exitPointTransform;

    public int remaining = 8;

    Vector3 _pendingHeadPos;
    Quaternion _pendingHeadRot;
    bool _hasPendingShift;

    /// <summary>
    /// Detaches the head customer and returns it. Does NOT shift the rest of
    /// the chain visually — call AdvanceLine() when the popped customer has
    /// actually left their old spot, so the line shifts forward at that moment
    /// rather than the moment of detachment.
    /// </summary>
    public Customer PopHead()
    {
        if (remaining <= 0) return null;
        int headIdx = remaining - 1;
        if (headIdx < 0 || headIdx >= links.Length) return null;

        Link headLink = links[headIdx];
        if (headLink == null || headLink.customer == null) return null;

        GameObject customerGO = headLink.customer;
        Customer customer = customerGO.GetComponent<Customer>();
        if (customer == null) return null;

        // Remember where the head was so the new head can walk into that spot
        // when AdvanceLine is called.
        _pendingHeadPos = headLink.transform.position;
        _pendingHeadRot = headLink.transform.rotation;
        _hasPendingShift = true;

        customerGO.transform.SetParent(null, true);
        headLink.customer = null;

        remaining--;

        return customer;
    }

    /// <summary>
    /// Shifts the chain forward by one slot. The new head walks into the spot
    /// the popped customer occupied. Idempotent — only does work if there's a
    /// pending shift from a recent PopHead.
    /// </summary>
    public void AdvanceLine()
    {
        if (!_hasPendingShift) return;
        _hasPendingShift = false;

        SetBackTrackPositions();

        if (remaining > 0)
        {
            int newHeadIdx = remaining - 1;
            if (newHeadIdx >= 0 && newHeadIdx < links.Length && links[newHeadIdx] != null)
            {
                links[newHeadIdx].nextPosition = _pendingHeadPos;
                links[newHeadIdx].nextRotation = _pendingHeadRot;
            }
        }

        UpdateTail();
    }

    void SetBackTrackPositions()
    {
        for (int i = links.Length - 2 - (links.Length - remaining); i >= 0; i--)
        {
            if (i + 1 >= links.Length) continue;
            if (links[i] == null || links[i + 1] == null) continue;
            links[i].nextPosition = links[i + 1].currentTransform.position;
            links[i].nextRotation = links[i + 1].currentTransform.rotation;
        }
    }

    void UpdateTail()
    {
        for (int i = 0; i < links.Length - (links.Length - remaining); i++)
        {
            if (links[i] == null) continue;
            links[i].MoveToNext();
        }
    }
}
