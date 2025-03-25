using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages recipes and provides a simple UI for testing robot arm movement
/// without requiring the TCP server
/// </summary>
public class RecipeManager : MonoBehaviour
{
    [System.Serializable]
    public class Recipe
    {
        public string recipeName;
        public int recipeId;
        public string[] ingredients;
    }

    [Header("References")]
    [SerializeField] private RobotMovementSequencer movementSequencer;
    [SerializeField] private RobotArmController robotController;

    [Header("Recipes")]
    [SerializeField] private Recipe[] recipes;

    [Header("UI Elements")]
    [SerializeField] private Button[] recipeButtons;
    [SerializeField] private Text statusText;
    [SerializeField] private Button[] ingredientButtons;
    [SerializeField] private Text[] buttonLabels;

    [Header("Testing")]
    [SerializeField] private int selectedRecipe = 0;

    private void Start()
    {
        // Auto-find references if not set
        if (movementSequencer == null)
        {
            movementSequencer = FindObjectOfType<RobotMovementSequencer>();
        }

        if (robotController == null)
        {
            robotController = FindObjectOfType<RobotArmController>();
        }

        // Set up UI
        SetupUI();

        UpdateStatus("System ready");
    }

    /// <summary>
    /// Set up UI elements
    /// </summary>
    private void SetupUI()
    {
        // Set up recipe buttons
        for (int i = 0; i < recipeButtons.Length && i < recipes.Length; i++)
        {
            int recipeIndex = i; // Capture for lambda

            // Set button text if we have a Text component
            Text buttonText = recipeButtons[i].GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = recipes[i].recipeName;
            }
            else if (i < buttonLabels.Length && buttonLabels[i] != null)
            {
                buttonLabels[i].text = recipes[i].recipeName;
            }

            // Add click listener
            recipeButtons[i].onClick.RemoveAllListeners();
            recipeButtons[i].onClick.AddListener(() => {
                ProcessRecipe(recipeIndex);
            });
        }

        // Set up ingredient buttons (if any)
        if (ingredientButtons != null && ingredientButtons.Length > 0)
        {
            // Get all ingredient names from all recipes
            HashSet<string> allIngredients = new HashSet<string>();
            foreach (var recipe in recipes)
            {
                foreach (var ingredient in recipe.ingredients)
                {
                    allIngredients.Add(ingredient);
                }
            }

            // Sort ingredients
            List<string> sortedIngredients = new List<string>(allIngredients);
            sortedIngredients.Sort();

            // Set up buttons
            for (int i = 0; i < ingredientButtons.Length && i < sortedIngredients.Count; i++)
            {
                string ingredient = sortedIngredients[i];

                // Set button text
                Text buttonText = ingredientButtons[i].GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = ingredient;
                }
                else if (i + recipeButtons.Length < buttonLabels.Length && buttonLabels[i + recipeButtons.Length] != null)
                {
                    buttonLabels[i + recipeButtons.Length].text = ingredient;
                }

                // Add click listener
                ingredientButtons[i].onClick.RemoveAllListeners();
                ingredientButtons[i].onClick.AddListener(() => {
                    PickIngredient(ingredient);
                });
            }
        }
    }

    /// <summary>
    /// Process a recipe by index
    /// </summary>
    public void ProcessRecipe(int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= recipes.Length)
        {
            Debug.LogError($"Recipe index {recipeIndex} out of range");
            return;
        }

        if (movementSequencer == null)
        {
            Debug.LogError("Movement sequencer not assigned");
            return;
        }

        if (movementSequencer.IsRunning())
        {
            UpdateStatus("Robot is busy, please wait");
            return;
        }

        Recipe recipe = recipes[recipeIndex];
        UpdateStatus($"Processing recipe: {recipe.recipeName}");

        selectedRecipe = recipeIndex;
        movementSequencer.ProcessRecipe(recipe.ingredients);

        StartCoroutine(WaitForCompletion($"Completed recipe: {recipe.recipeName}"));
    }

    /// <summary>
    /// Pick a single ingredient and move it to the serving bowl
    /// </summary>
    public void PickIngredient(string ingredient)
    {
        if (movementSequencer == null)
        {
            Debug.LogError("Movement sequencer not assigned");
            return;
        }

        if (movementSequencer.IsRunning())
        {
            UpdateStatus("Robot is busy, please wait");
            return;
        }

        UpdateStatus($"Adding ingredient: {ingredient}");
        movementSequencer.AddIngredientToServing(ingredient);

        StartCoroutine(WaitForCompletion($"Added {ingredient}"));
    }

    /// <summary>
    /// Wait for the robot to complete its movement
    /// </summary>
    private IEnumerator WaitForCompletion(string completionMessage)
    {
        // Disable all buttons while the robot is moving
        SetButtonsInteractable(false);

        // Wait until the sequencer is done
        while (movementSequencer != null && movementSequencer.IsRunning())
        {
            yield return null;
        }

        // Update status and re-enable buttons
        UpdateStatus(completionMessage);
        SetButtonsInteractable(true);
    }

    /// <summary>
    /// Update the status text
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
    /// Enable/disable all buttons
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        foreach (var button in recipeButtons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        foreach (var button in ingredientButtons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }

    /// <summary>
    /// Reset the robot to home position
    /// </summary>
    public void ResetRobot()
    {
        if (robotController != null && !movementSequencer.IsRunning())
        {
            robotController.MoveToHomePosition();
            UpdateStatus("Robot reset to home position");
        }
    }

    /// <summary>
    /// Open the gripper
    /// </summary>
    public void OpenGripper()
    {
        if (robotController != null && !movementSequencer.IsRunning())
        {
            robotController.OpenGripper();
            UpdateStatus("Gripper opened");
        }
    }

    /// <summary>
    /// Close the gripper
    /// </summary>
    public void CloseGripper()
    {
        if (robotController != null && !movementSequencer.IsRunning())
        {
            robotController.CloseGripper();
            UpdateStatus("Gripper closed");
        }
    }
}