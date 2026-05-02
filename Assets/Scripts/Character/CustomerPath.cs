using UnityEngine;
using System.Collections.Generic;

public class CustomerPath : MonoBehaviour
{
    [Header("Customer Path")]
    public Transform[] waypoints;
    public float spacing = 1.0f;

    public List<Customer> customers = new List<Customer>();

    [Header("Config")]
    public float moveSpeed = 5.0f;
    public float rotationSpeed = 10.0f;

    public Vector3 GetPoint(int index) => waypoints[index].position;
    public int PointCount => waypoints.Length;

    public Vector3 GetTargetPositionForCustomer(int queueIndex)
    {
        if (waypoints.Length < 2)
        {
            return waypoints[0].position;
        }

        Vector3 start = waypoints[0].position;
        Vector3 next = waypoints[1].position;
        Vector3 dir = (next - start).normalized;
        return start - dir * spacing * queueIndex;
    }

    public void Register(Customer c)
    {
        if (!customers.Contains(c))
        {
            customers.Add(c);
        }
    }

    public void Unregister(Customer c)
    {
        customers.Remove(c);
    }
}
