using System;
using UnityEngine;
using TMPro;

public class ProgressUI : MonoBehaviour
{
    public TMP_Text customerCountText;
    public TMP_Text moneyText;

    private int customersServed = 0;
    private int totalCustomers = 200;
    private float money = 0f;


    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log($"Clicked on {Input.mousePosition}");
            ServeCustomer(5.50f);
        }
    }

    public void ServeCustomer(float payment)
    {
        customersServed++;
        money += payment;
        customerCountText.text = $"{customersServed}/{totalCustomers}";
        moneyText.text = $"Total Money: ${money:F2}";
    }
}