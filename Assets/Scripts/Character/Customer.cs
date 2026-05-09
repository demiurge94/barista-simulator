using System.Collections;
using UnityEngine;

public class Customer : MonoBehaviour
{
    public enum State
    {
        Idle,
        WalkingToKiosk,
        AtKiosk,
        KioskFinished,
        WalkingToSlot,
        WaitingAtCounter,
        WalkingToExit
    }

    [Header("Visuals")]
    public int customerTexture = 1;
    public GameObject model;
    public string customerName;
    public Material[] customerMaterials = new Material[4];

    [Header("Item handoff (used on fulfillment)")]
    public Transform itemSpawnTransform;
    public GameObject test_item;
    public bool testItemSpawning = false;

    [Header("Walk timings (seconds)")]
    public float walkToKioskSeconds = 4f;
    public float walkToSlotSeconds = 2f;
    public float walkToExitSeconds = 6f;
    public float kioskDwellSeconds = 3f;

    [System.NonSerialized] public State state = State.Idle;
    [System.NonSerialized] public Order Order;
    [System.NonSerialized] public OrderManager orderManager;

    void Start()
    {
        if (testItemSpawning)
            GiveItem(test_item);

        int number = Random.Range(1, 5);
        if (model != null)
        {
            Renderer modelRenderer = model.GetComponent<Renderer>();
            if (modelRenderer != null && customerMaterials.Length >= number)
                modelRenderer.material = customerMaterials[number - 1];
        }

        switch (number)
        {
            case 1: customerName = "Jessica"; break;
            case 2: customerName = "Mark"; break;
            case 3: customerName = "Morning Zombie"; break;
            case 4: customerName = "Morning Zombie"; break;
        }
    }

    public void GiveItem(GameObject item)
    {
        if (item == null || itemSpawnTransform == null) return;
        GameObject temp = Instantiate(item, itemSpawnTransform);
        temp.transform.SetParent(itemSpawnTransform);
        temp.transform.localPosition = Vector3.zero;
    }

    /// <summary>Walks to the kiosk Transform, then runs the dwell coroutine.
    /// On dwell completion, calls OrderManager.OnCustomerOrderPlaced(this).</summary>
    public void GoToKioskAndOrder(Transform kiosk)
    {
        if (kiosk == null) return;
        StartCoroutine(GoToKioskRoutine(kiosk));
    }

    IEnumerator GoToKioskRoutine(Transform kiosk)
    {
        state = State.WalkingToKiosk;
        yield return WalkTo(kiosk.position, kiosk.rotation, walkToKioskSeconds);
        state = State.AtKiosk;
        yield return new WaitForSeconds(kioskDwellSeconds);
        state = State.KioskFinished;
        if (orderManager != null) orderManager.OnCustomerOrderPlaced(this);
    }

    /// <summary>Walks to a wait slot. Caller (OrderManager) is responsible for
    /// having added this customer to its waiters list at the matching index.</summary>
    public void GoToSlot(Transform slot)
    {
        if (slot == null) return;
        StartCoroutine(GoToSlotRoutine(slot));
    }

    IEnumerator GoToSlotRoutine(Transform slot)
    {
        state = State.WalkingToSlot;
        yield return WalkTo(slot.position, slot.rotation, walkToSlotSeconds);
        state = State.WaitingAtCounter;
    }

    /// <summary>Walks to the exit Transform and disables the GameObject on arrival.</summary>
    public void GoToExit(Transform exit)
    {
        if (exit == null) return;
        StartCoroutine(GoToExitRoutine(exit));
    }

    IEnumerator GoToExitRoutine(Transform exit)
    {
        state = State.WalkingToExit;
        yield return WalkTo(exit.position, exit.rotation, walkToExitSeconds);
        gameObject.SetActive(false);
    }

    IEnumerator WalkTo(Vector3 target, Quaternion targetRotation, float duration)
    {
        // If we're already there, snap and skip the walk animation. Customer 0
        // starts at the kiosk wait point, so their "walk to kiosk" is a no-op.
        if (Vector3.Distance(transform.position, target) < 0.05f &&
            Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            transform.position = target;
            transform.rotation = targetRotation;
            yield break;
        }

        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("can_walk", true);
        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.Clamp01(elapsed / 1.0f));
            yield return null;
        }
        if (anim != null) anim.SetBool("can_walk", false);
    }
}
