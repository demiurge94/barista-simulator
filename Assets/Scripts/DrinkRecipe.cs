using UnityEngine;

public enum DrinkCategory
{
    Hot,
    Cold
}

[System.Serializable]
public class RecipeIngredient
{
    public IngredientData ingredient;   // drag an Ingredient asset here
    public int quantity = 1;
}

[CreateAssetMenu(fileName = "NewDrink", menuName = "Barista/Drink Recipe")]
public class DrinkRecipe : ScriptableObject
{
    public string drinkName;
    public DrinkCategory category;
    public RecipeIngredient[] ingredients;  // what you need to make it
    public float craftTime = 3f;
    public float price = 5.50f;

    [TextArea] public string description;
}
