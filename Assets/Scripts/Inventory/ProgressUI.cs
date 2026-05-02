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


    public void ServeCustomer(float payment)
    {
        customersServed++;
        money += payment;
        customerCountText.text = $"{customersServed}/{totalCustomers}";
        moneyText.text = $"Total Money: ${money:F2}";
    }
}