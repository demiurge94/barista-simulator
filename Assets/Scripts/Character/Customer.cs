using UnityEngine;

public class Customer : MonoBehaviour
{
    public CustomerPath path;

    public float moveSpeedOverride = 0.0f;
    int myIndex => path.customers.IndexOf(this);

    int targetPoint = 0;

    void OnEnable()
    {
        path?.Register(this);
        SnapToQueuePosition();
    }

    void OnDisable()
    {
        path?.Unregister(this);
    }

    void SnapToQueuePosition()
    {
        if (path == null)
        {
            return;
        }

        transform.position = path.GetTargetPositionForCustomer(myIndex);
    }

    void Update()
    {
        if (path == null || path.PointCount == 0) return;

        if (myIndex == 0)
        {
            Vector3 target = path.GetPoint(targetPoint);
            MoveTowards(target);

            if(Vector3.Distance(transform.position, target) < 0.1f)
            {
                targetPoint = Mathf.Min(targetPoint + 1, path.PointCount - 1);
            }
        }
        else
        {
            Vector3 slot = path.GetTargetPositionForCustomer(myIndex);
            MoveTowards(slot);
        }

        Vector3 vel = (GetVelocityApprox());

        if (vel.sqrMagnitude > 0.0001f) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(vel), 10.0f * Time.deltaTime);
        }
    }

    void MoveTowards(Vector3 target)
    {
        float spd = moveSpeedOverride > 0f ? moveSpeedOverride : path.moveSpeed;
        transform.position = Vector3.MoveTowards(transform.position, target, spd * Time.deltaTime);
    }


    Vector3 lastPos;

    Vector3 GetVelocityApprox()
    {
        Vector3 v = (transform.position - lastPos) / Time.deltaTime;
        lastPos = transform.position;
        return v;
    }


}
