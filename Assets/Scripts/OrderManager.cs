using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class OrderManager : MonoBehaviour
{
    public TMP_Text orderListText;

    private List<string> possibleDrinks = new List<string>
    {
        "Caramel Machiato",
        "Caramel Frapuchino",
        "Dragonfruit Drink",
        "Pink Drink"
    };

    private List<string> currentOrders = new List<string>();
    private int orderNumber = 1;

    void Start()
    {
        // Generate 4 starting orders
        for (int i = 0; i < 4; i++)
        {
            AddRandomOrder();
        }
        UpdateDisplay();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // Remove a random order
            if (currentOrders.Count > 0)
            {
                int removeIndex = Random.Range(0, currentOrders.Count);
                currentOrders.RemoveAt(removeIndex);
            }

            // Add a new one
            AddRandomOrder();
            UpdateDisplay();
        }
    }

    void AddRandomOrder()
    {
        string drink = possibleDrinks[Random.Range(0, possibleDrinks.Count)];
        currentOrders.Add($"Order #{orderNumber} {drink}");
        orderNumber++;
    }

    void UpdateDisplay()
    {
        orderListText.text = string.Join("\n", currentOrders);
    }
}