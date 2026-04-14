using UnityEngine;

[CreateAssetMenu(fileName = "NewIngredient", menuName = "Barista/Ingredient")]
public class IngredientData : ScriptableObject
{
    public string ingredientName;
    public Sprite icon;              // optional icon for UI later
}
