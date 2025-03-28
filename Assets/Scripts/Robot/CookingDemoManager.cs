using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This script manages a cooking demo by connecting the RobotArmTcpServer
/// with the RobotMovementSequencer. It handles the integration between
/// order processing and physical robot movements.
/// </summary>
public class CookingDemoManager : MonoBehaviour
{
    [Header("Robot References")]
    [SerializeField] private AutoChefApiClient tcpServer;
    [SerializeField] private RobotMovementSequencer movementSequencer;

    [Header("Ingredient Setup")]
    [SerializeField] private Transform[] ingredientPositions;
    [SerializeField] private string[] ingredientNames;

    [Header("Recipe Definitions")]
    [SerializeField] private RecipeDefinition[] recipes;

    [Header("UI References")]
    [SerializeField] private Text statusText;
    [SerializeField] private Button[] demoButtons;

    // Dictionary mapping recipe IDs to ingredient lists
    private Dictionary<int, string[]> recipeIngredients = new Dictionary<int, string[]>();

    private void Start()
    {
        // Initialize references if not set in inspector
        if (tcpServer == null)
        {
            tcpServer = GetComponent<AutoChefApiClient>();
        }

        if (movementSequencer == null)
        {
            movementSequencer = GetComponent<RobotMovementSequencer>();
        }

        // Register ingredients with the sequencer
        if (movementSequencer != null)
        {
            for (int i = 0; i < Mathf.Min(ingredientPositions.Length, ingredientNames.Length); i++)
            {
                if (ingredientPositions[i] != null && !string.IsNullOrEmpty(ingredientNames[i]))
                {
                    movementSequencer.AddIngredientPosition(ingredientNames[i], ingredientPositions[i]);
                }
            }
        }

        // Initialize recipe dictionary
        foreach (var recipe in recipes)
        {
            recipeIngredients[recipe.recipeId] = recipe.ingredients;
        }

        // Set up demo buttons
        SetupDemoButtons();
    }

    /// <summary>
    /// Called by the TCP server when a new order is received
    /// </summary>
    public void ProcessOrder(AutoChefApiClient.Order order)
    {
        // Only process if we're not already running a sequence
        if (!movementSequencer.IsRunning())
        {
            // Check if we have a recipe for this order
            if (recipeIngredients.TryGetValue(order.RecipeId, out string[] ingredients))
            {
                // Start cooking process
                StartCoroutine(CookRecipe(order, ingredients));
            }
            else
            {
                Debug.LogWarning($"No recipe found for RecipeId: {order.RecipeId}");
                UpdateStatus($"Unknown recipe: {order.RecipeId}");
            }
        }
        else
        {
            Debug.LogWarning("Cannot process new order: Robot is already running a sequence");
            UpdateStatus("Robot busy - order queued");
        }
    }

    /// <summary>
    /// Cooking process coroutine
    /// </summary>
    private IEnumerator CookRecipe(AutoChefApiClient.Order order, string[] ingredients)
    {
        UpdateStatus($"Starting recipe {order.RecipeId}: {string.Join(", ", ingredients)}");

        // Use the movement sequencer to cook the recipe
        movementSequencer.ProcessRecipe(ingredients);

        // Wait until the sequencer is done
        while (movementSequencer.IsRunning())
        {
            yield return null;
        }

        UpdateStatus($"Completed recipe {order.RecipeId}");
    }

    /// <summary>
    /// Set up demo buttons for manually triggering recipes
    /// </summary>
    private void SetupDemoButtons()
    {
        for (int i = 0; i < demoButtons.Length && i < recipes.Length; i++)
        {
            int recipeIndex = i; // Capture for lambda

            if (demoButtons[i] != null)
            {
                // Clear existing listeners
                demoButtons[i].onClick.RemoveAllListeners();

                // Set button text to recipe name
                Text buttonText = demoButtons[i].GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = $"Make Recipe {recipes[recipeIndex].recipeId}";
                }

                // Add new listener
                demoButtons[i].onClick.AddListener(() => {
                    // Create a simulated order
                    AutoChefApiClient.Order demoOrder = new AutoChefApiClient.Order
                    {
                        OrderId = -1, // Negative ID indicates a demo order
                        RecipeId = recipes[recipeIndex].recipeId,
                        Status = "Demo"
                    };

                    // Process it
                    ProcessOrder(demoOrder);
                });
            }
        }
    }

    /// <summary>
    /// Updates the UI status text
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log(message);
    }

    /// <summary>
    /// Run a simple pick and place demo with a single ingredient
    /// </summary>
    public void RunSimpleDemo(string ingredientName)
    {
        if (!movementSequencer.IsRunning())
        {
            StartCoroutine(SimpleDemo(ingredientName));
        }
    }

    /// <summary>
    /// Simple demo coroutine
    /// </summary>
    private IEnumerator SimpleDemo(string ingredientName)
    {
        UpdateStatus($"Picking up {ingredientName}...");

        // Pick up the ingredient and move it to cooking position
        movementSequencer.AddIngredientToServing(ingredientName);

        // Wait until the sequencer is done
        while (movementSequencer.IsRunning())
        {
            yield return null;
        }

        // Now serve it
        //UpdateStatus($"Serving {ingredientName}...");
        //movementSequencer.ServeDish();

        // Wait until complete
        while (movementSequencer.IsRunning())
        {
            yield return null;
        }

        UpdateStatus("Demo complete!");
    }
}

/// <summary>
/// Defines a recipe with an ID and list of ingredients
/// </summary>
[System.Serializable]
public class RecipeDefinition
{
    public int recipeId;
    public string[] ingredients;
}