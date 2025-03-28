using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AutoChef;
using AutoChef.API.Client;

/// <summary>
/// Manages the serving bowl hierarchy and ingredients visibility 
/// according to the recipe processing flow
/// </summary>
public class ServingBowlManager : MonoBehaviour
{
    [Header("Bowl Components")]
    [SerializeField] private GameObject ramenBowl;
    [SerializeField] private GameObject nuocDung;
    [SerializeField] private GameObject rauHanh;
    [SerializeField] private GameObject banhPho;
    [SerializeField] private GameObject thitGa;
    [SerializeField] private GameObject thitBo;

    [Header("Configuration")]
    [SerializeField] private float servingWaitTime = 3f;
    [SerializeField] private bool canReceiveOrders = true;

    [Header("References")]
    [SerializeField] private AutoChefRecipeManager recipeManager;
    [SerializeField] private AutoChefApiClient apiClient;

    // Dictionary to track ingredients
    private Dictionary<string, GameObject> ingredientMap = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

    // Tracking variables
    private bool isProcessingRecipe = false;
    private string[] currentIngredients;
    private int currentIngredientIndex = 0;

    private void Awake()
    {
        // Disable all ingredients initially
        if (nuocDung) nuocDung.SetActive(false);
        if (rauHanh) rauHanh.SetActive(false);
        if (banhPho) banhPho.SetActive(false);
        if (thitGa) thitGa.SetActive(false);
        if (thitBo) thitBo.SetActive(false);

        // Map ingredient names to GameObjects based on the recipe ingredients
        if (nuocDung) ingredientMap["nuocdung"] = nuocDung;
        if (nuocDung) ingredientMap["nước dùng"] = nuocDung;

        // RauHanh is used for both "rau thơm" and "hành"
        if (rauHanh) ingredientMap["rauhanh"] = rauHanh;
        if (rauHanh) ingredientMap["rau thơm"] = rauHanh;
        if (rauHanh) ingredientMap["hành"] = rauHanh;

        if (banhPho) ingredientMap["banhpho"] = banhPho;
        if (banhPho) ingredientMap["bánh phở"] = banhPho;

        // Map meat ingredients to their respective GameObjects
        if (thitBo) ingredientMap["thịt bò"] = thitBo;
        if (thitGa) ingredientMap["thịt gà"] = thitGa;
    }

    private void Start()
    {
        // Auto-find references if not set
        if (recipeManager == null)
            recipeManager = FindObjectOfType<AutoChefRecipeManager>();

        if (apiClient == null)
            apiClient = FindObjectOfType<AutoChefApiClient>();
    }

    private void Update()
    {
        // Check if recipe manager exists
        if (recipeManager == null) return;

        // Get current status from recipe manager
        string status = recipeManager.GetStatus();

        // If we're not already processing and status is "Processing", start monitoring
        if (!isProcessingRecipe && status == "Processing")
        {
            StartRecipeProcessing();
        }
        // If we were processing and status is now "Completed", finish up
        else if (isProcessingRecipe && status == "Completed")
        {
            StartCoroutine(FinishServingSequence());
        }
    }

    private void StartRecipeProcessing()
    {
        isProcessingRecipe = true;
        currentIngredientIndex = 0;

        // Get the current recipe ingredients using reflection
        currentIngredients = GetCurrentRecipeIngredients();

        if (currentIngredients != null && currentIngredients.Length > 0)
        {
            Debug.Log($"Starting to monitor recipe with {currentIngredients.Length} ingredients");

            // Start monitoring for ingredient changes
            StartCoroutine(MonitorIngredientProgress());
        }
        else
        {
            Debug.LogWarning("Could not retrieve ingredients for current recipe");
            isProcessingRecipe = false;
        }
    }

    private IEnumerator MonitorIngredientProgress()
    {
        // Monitor the ingredient task counter in recipe manager
        var ingredientTaskCounterField = recipeManager.GetType().GetField("ingredientTaskCounter",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (ingredientTaskCounterField != null)
        {
            while (isProcessingRecipe)
            {
                // Get the current ingredient task counter
                var ingredientTaskCounter = ingredientTaskCounterField.GetValue(recipeManager) as Dictionary<string, int>;

                if (ingredientTaskCounter != null)
                {
                    // Check each ingredient in our recipe
                    for (int i = 0; i < currentIngredients.Length; i++)
                    {
                        string ingredient = currentIngredients[i];

                        // If this ingredient has tasks completed and is past our current index
                        if (ingredientTaskCounter.ContainsKey(ingredient) &&
                            ingredientTaskCounter[ingredient] > 0 &&
                            i >= currentIngredientIndex)
                        {
                            // Update our tracking index and show this ingredient
                            currentIngredientIndex = i;
                            ShowIngredient(ingredient);

                            // Slight delay before checking for the next ingredient
                            yield return new WaitForSeconds(0.5f);
                        }
                    }
                }

                // Check again after a short delay
                yield return new WaitForSeconds(0.25f);
            }
        }
        else
        {
            Debug.LogWarning("Could not access ingredientTaskCounter field in RecipeManager");
        }
    }

    private void ShowIngredient(string ingredientName)
    {
        if (ingredientMap.TryGetValue(ingredientName, out GameObject ingredientObj))
        {
            Debug.Log($"Showing ingredient: {ingredientName}");
            ingredientObj.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"Ingredient not found in map: {ingredientName}");
        }
    }

    private IEnumerator FinishServingSequence()
    {
        Debug.Log("Recipe completed, finishing serving sequence");
        isProcessingRecipe = false;

        // Set flag to prevent new orders
        canReceiveOrders = false;

        // Delay before hiding the bowl
        yield return new WaitForSeconds(1.0f);

        // Hide the bowl
        if (ramenBowl) ramenBowl.SetActive(false);

        // Wait the configured serving time
        Debug.Log($"Serving wait time: {servingWaitTime} seconds");
        yield return new WaitForSeconds(servingWaitTime);

        // Show the bowl again, but hide ingredients
        if (ramenBowl) ramenBowl.SetActive(true);
        if (nuocDung) nuocDung.SetActive(false);
        if (rauHanh) rauHanh.SetActive(false);
        if (banhPho) banhPho.SetActive(false);
        if (thitGa) thitGa.SetActive(false);
        if (thitBo) thitBo.SetActive(false);

        // Allow new orders
        canReceiveOrders = true;
        Debug.Log("Ready for new orders");
    }

    private string[] GetCurrentRecipeIngredients()
    {
        try
        {
            // First attempt: Try to get from currently processing recipe in the recipe manager
            var processingRecipeField = recipeManager.GetType().GetField("processingRecipe",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (processingRecipeField != null)
            {
                var recipe = processingRecipeField.GetValue(recipeManager);
                if (recipe != null)
                {
                    var ingredientsField = recipe.GetType().GetField("ingredients");
                    if (ingredientsField != null)
                    {
                        return ingredientsField.GetValue(recipe) as string[];
                    }
                }
            }

            // Second attempt: Try to get from recipes list using the current order
            if (apiClient != null)
            {
                var currentOrderField = apiClient.GetType().GetField("currentOrder",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (currentOrderField != null)
                {
                    var currentOrder = currentOrderField.GetValue(apiClient);
                    if (currentOrder != null)
                    {
                        var recipeIdProp = currentOrder.GetType().GetProperty("RecipeId");
                        if (recipeIdProp != null)
                        {
                            int recipeId = (int)recipeIdProp.GetValue(currentOrder);

                            // Get the recipes array
                            var recipesField = recipeManager.GetType().GetField("recipes",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                            if (recipesField != null)
                            {
                                var recipes = recipesField.GetValue(recipeManager) as AutoChefRecipeManager.Recipe[];
                                if (recipes != null)
                                {
                                    foreach (var recipe in recipes)
                                    {
                                        if (recipe.recipeId == recipeId)
                                        {
                                            return recipe.ingredients;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Fallback: Return default ingredients 
            return new string[] { "bánh phở", "rau thơm", "nước dùng" };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error getting recipe ingredients: {e.Message}");
            return null;
        }
    }

    // Public method to check if new orders can be received
    public bool CanReceiveOrders()
    {
        return canReceiveOrders;
    }
}