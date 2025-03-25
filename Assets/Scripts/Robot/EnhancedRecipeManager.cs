using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enhanced Recipe Manager with detailed operation logging to match the 
/// RobotStepTask and RobotOperationLog format from the database
/// </summary>
public class EnhancedRecipeManager : MonoBehaviour
{
    [System.Serializable]
    public class Recipe
    {
        public string recipeName;
        public int recipeId;
        public string[] ingredients;
    }

    [System.Serializable]
    public class OperationStep
    {
        public string description;
        public float estimatedTime; // in seconds
        public int repeatCount = 1;
    }

    [System.Serializable]
    public class IngredientOperations
    {
        public string ingredientName;
        public OperationStep[] steps;
    }

    /// <summary>
    /// Container class to hold success value since we can't use ref params in coroutines
    /// </summary>
    private class TaskResult
    {
        public bool success;
        public float taskDuration;
    }

    [Header("References")]
    [SerializeField] private RobotMovementSequencer movementSequencer;
    [SerializeField] private RobotArmController robotController;

    [Header("Recipes")]
    [SerializeField] private Recipe[] recipes;

    [Header("Operations")]
    [SerializeField] private IngredientOperations[] ingredientOperations;

    [Header("UI Elements")]
    [SerializeField] private Button[] recipeButtons;
    [SerializeField] private Text statusText;
    [SerializeField] private TMPro.TextMeshProUGUI operationLogText;
    [SerializeField] private ScrollRect logScrollRect;

    [Header("Robot Settings")]
    [SerializeField] private int robotId = 1;
    [SerializeField] private float moveDelayFactor = 1.0f; // Scale actual times vs. expected

    // Operation logging
    private StringBuilder operationLog = new StringBuilder();
    private Dictionary<string, IngredientOperations> operationsLookup = new Dictionary<string, IngredientOperations>();
    private Dictionary<string, int> ingredientTaskCounter = new Dictionary<string, int>();
    private int currentOrderId = 0;
    private DateTime startTime;
    private float totalPreparationTime = 0f;
    private string currentStatus = "Waiting";
    private int globalTaskCounter = 0;

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

        // Create lookup dictionary for quick ingredient operations access
        operationsLookup.Clear();
        foreach (var op in ingredientOperations)
        {
            if (op != null && !string.IsNullOrEmpty(op.ingredientName))
            {
                //Debug.Log($"Adding ingredient to lookup: {op.ingredientName}");
                operationsLookup[op.ingredientName] = op;
            }
        }

        // Log all available operations
        //Debug.Log($"Available operations: {operationsLookup.Count}");
        //foreach (var key in operationsLookup.Keys)
        //{
        //    Debug.Log($"- {key}");
        //}

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

            // Add click listener
            recipeButtons[i].onClick.RemoveAllListeners();
            recipeButtons[i].onClick.AddListener(() => {
                ProcessRecipe(recipeIndex);
            });
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

        // Debug the recipe ingredients
        Debug.Log($"Processing recipe: {recipe.recipeName} with {recipe.ingredients.Length} ingredients:");
        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            Debug.Log($"  {i + 1}. {recipe.ingredients[i]}");
        }

        // Generate a new order ID
        currentOrderId++;

        // Reset operation log and counters
        operationLog.Clear();
        ingredientTaskCounter.Clear();
        globalTaskCounter = 0;

        // Start time tracking
        startTime = DateTime.Now;
        totalPreparationTime = 0f;

        // Update status
        UpdateStatus($"Processing recipe: {recipe.recipeName}");

        // Begin log entry
        operationLog.AppendLine($"Robot #{robotId} processing Order #{currentOrderId} ({recipe.recipeName}):");

        // Start the cooking process
        StartCoroutine(ProcessRecipeWithLogging(recipe));
    }

    /// <summary>
    /// Process a recipe with detailed logging
    /// </summary>
    private IEnumerator ProcessRecipeWithLogging(Recipe recipe)
    {
        currentStatus = "Processing";

        // Start time of the entire recipe
        DateTime recipeStartTime = DateTime.Now;

        // Disable all buttons while the robot is moving
        SetButtonsInteractable(false);

        int ingredientCount = recipe.ingredients.Length;
        //Debug.Log($"Starting to process {ingredientCount} ingredients for {recipe.recipeName}");

        // Process each ingredient one by one
        for (int i = 0; i < ingredientCount; i++)
        {
            string ingredient = recipe.ingredients[i];
            //Debug.Log($"Processing ingredient {i + 1}/{ingredientCount}: {ingredient}");

            // Check if we have operations for this ingredient
            if (operationsLookup.ContainsKey(ingredient))
            {
                //Debug.Log($"Found operations for {ingredient}");
                IngredientOperations operations = operationsLookup[ingredient];

                // Initialize task counter for this ingredient if not exists
                if (!ingredientTaskCounter.ContainsKey(ingredient))
                {
                    ingredientTaskCounter[ingredient] = 0;
                }

                // Create a flag to track coroutine completion
                bool stepProcessingComplete = false;

                // Start the ingredient steps processing
                StartCoroutine(ProcessIngredientStepsWithCompletion(ingredient, operations, () => {
                    stepProcessingComplete = true;
                }));

                // Wait until the steps are fully processed
                yield return new WaitUntil(() => stepProcessingComplete);

                // Small delay between ingredients
                yield return new WaitForSeconds(2.5f);
            }
            else
            {
                // Default handling for ingredients without defined operations
                globalTaskCounter++;
                LogOperationStep($"Processing {ingredient} (default operations)",
                                 5.0f, true, globalTaskCounter);

                // Use the basic movement sequence
                if (movementSequencer != null)
                {
                    yield return StartCoroutine(ExecuteMovementWithErrorHandling(ingredient));
                }

                yield return new WaitForSeconds(2.5f);
            }
        }

        // Calculate total preparation time
        totalPreparationTime = (float)(DateTime.Now - recipeStartTime).TotalSeconds;

        // Add completion log entry
        operationLog.AppendLine($"Recipe completed in {FormatTime(totalPreparationTime)} [Success]");

        // Update operation log UI
        if (operationLogText != null)
        {
            operationLogText.text = operationLog.ToString();

            // Scroll to bottom
            if (logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        // Update the status
        currentStatus = "Completed";
        UpdateStatus($"Recipe {recipe.recipeName} completed in {FormatTime(totalPreparationTime)}");

        // Re-enable buttons
        SetButtonsInteractable(true);
    }

    // New method to handle ingredient steps with a completion callback
    private IEnumerator ProcessIngredientStepsWithCompletion(string ingredient, IngredientOperations operations, System.Action onComplete)
    {
        yield return StartCoroutine(ProcessIngredientSteps(ingredient, operations));
        onComplete?.Invoke();
    }
    private IEnumerator ExecuteMovementWithErrorHandling(string ingredient)
    {
        bool errorOccurred = false;

        try
        {
            // This part doesn't have yield inside the try block
            movementSequencer.AddIngredientToServing(ingredient);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error moving ingredient {ingredient}: {e.Message}");
            errorOccurred = true;
        }

        // Only wait for completion if we didn't have an error
        if (!errorOccurred)
        {
            // Wait for completion outside the try block
            while (movementSequencer.IsRunning())
            {
                yield return null;
            }
        }
    }

    /// <summary>
    /// Process all steps for a specific ingredient
    /// </summary>
    private IEnumerator ProcessIngredientSteps(string ingredient, IngredientOperations operations)
    {
        //Debug.Log($"Processing steps for ingredient: {ingredient}");
        //Debug.Log($"Total steps: {operations.steps.Length}");

        // Process each step sequentially
        for (int stepIndex = 0; stepIndex < operations.steps.Length; stepIndex++)
        {
            var step = operations.steps[stepIndex];

            //Debug.Log($"Processing step {stepIndex + 1}: {step.description} (Repeat: {step.repeatCount})");

            // For each repeat of this step
            for (int repeatIndex = 0; repeatIndex < step.repeatCount; repeatIndex++)
            {
                // Increment the global task counter
                globalTaskCounter++;

                // Increment the ingredient-specific task counter
                if (!ingredientTaskCounter.ContainsKey(ingredient))
                {
                    ingredientTaskCounter[ingredient] = 0;
                }
                ingredientTaskCounter[ingredient]++;

                // Determine if this needs a repeat suffix
                string repeatSuffix = step.repeatCount > 1 ? $" ({repeatIndex + 1}/{step.repeatCount})" : "";

                // Log the step beginning
                string description = $"{step.description}{repeatSuffix}";
                UpdateStatus($"Task {globalTaskCounter}: {description}");

                // Create result object to capture duration and success
                TaskResult result = new TaskResult { success = true, taskDuration = 0 };

                // Execute the robot step
                yield return StartCoroutine(ExecuteRobotStep(ingredient, step.description, repeatIndex, result));

                // Log the completed step with the actual duration after execution
                LogOperationStep(description, result.taskDuration, result.success, globalTaskCounter);

                // Small delay between repeats
                if (repeatIndex < step.repeatCount - 1)
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }
    }
    /// <summary>
    /// Execute a specific robot step based on description
    /// </summary>
    private IEnumerator ExecuteRobotStep(string ingredient, string stepDescription, int repeatIndex, TaskResult result)
    {
        // Start timing for this step
        float startTaskTime = Time.time;
        bool movementWait = false;

        try
        {
            // Parse the step description to determine the action
            if (stepDescription.Contains("Move arm to"))
            {
                // This is a movement step
                if (movementSequencer != null)
                {
                    // Add additional delay to simulate movement
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.3f, 0.8f) * moveDelayFactor);
                }
            }
            else if (stepDescription.Contains("Pick up"))
            {
                // This is a pickup step
                if (robotController != null)
                {
                    robotController.CloseGripper();

                    // Simulate pickup time
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 1.0f) * moveDelayFactor);
                }
            }
            else if (stepDescription.Contains("Place") || stepDescription.Contains("Pour"))
            {
                // This is a placement step
                if (movementSequencer != null)
                {
                    // For demonstration, we'll actually move the ingredient if it's the first repeat
                    if (repeatIndex == 0)
                    {
                        movementSequencer.AddIngredientToServing(ingredient);
                        movementWait = true;
                    }
                    else
                    {
                        // For subsequent repeats, just simulate the time
                        yield return new WaitForSeconds(UnityEngine.Random.Range(0.8f, 1.2f) * moveDelayFactor);
                    }

                    // Open gripper after placement
                    if (robotController != null)
                    {
                        robotController.OpenGripper();
                    }
                }
            }
            else
            {
                // Generic action for other descriptions
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 1.5f) * moveDelayFactor);
            }

            // Wait for the movement to complete if using actual robot movement
            if (movementWait && movementSequencer != null)
            {
                // Wait until the movement is complete
                while (movementSequencer.IsRunning())
                {
                    yield return null;
                }
            }

            // Small random chance of failure for demonstration
            //if (UnityEngine.Random.value < 0.02f) // 2% chance of failure
            //{
            //    result.success = false;

            //    // Log the failure
            //    operationLog.AppendLine($"- ERROR: Failed to {stepDescription.ToLower()} [Failed]");
            //    operationLog.AppendLine($"- ERROR: Unable to complete remaining steps due to failure");

            //    // Update status
            //    UpdateStatus($"Error: Failed to {stepDescription.ToLower()}");

            //    // Update completion status
            //    currentStatus = "Failed";
            //}
        }
        finally
        {
            // Calculate the duration - always done, even if there's an exception
            result.taskDuration = Time.time - startTaskTime;
        }
    }
    /// <summary>
    /// Log an operation step with timing information
    /// </summary>
    private void LogOperationStep(string description, float duration, bool success, int taskNumber)
    {
        string status = success ? "Success" : "Failed";
        string taskPrefix = taskNumber > 0 ? $"- Task {taskNumber}: " : "- ";

        // Format the log entry
        operationLog.AppendLine($"{taskPrefix}{description} [{status}] - {(int)duration}s");

        // Update the UI if available
        if (operationLogText != null)
        {
            operationLogText.text = operationLog.ToString();

            // Scroll to bottom
            if (logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }
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
        //Debug.Log(message);
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
    }

    /// <summary>
    /// Format seconds into a human-readable time string
    /// </summary>
    private string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60);
        int remainingSeconds = (int)(seconds % 60);

        if (minutes > 0)
        {
            return $"{minutes} minute{(minutes != 1 ? "s" : "")} {remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";
        }
        else
        {
            return $"{remainingSeconds} second{(remainingSeconds != 1 ? "s" : "")}";
        }
    }

    /// <summary>
    /// Get the current operation log as a string - can be used to save to database
    /// </summary>
    public string GetOperationLog()
    {
        return operationLog.ToString();
    }

    /// <summary>
    /// Get the current status of the operation
    /// </summary>
    public string GetStatus()
    {
        return currentStatus;
    }

    /// <summary>
    /// Get the total preparation time in seconds
    /// </summary>
    public float GetTotalPreparationTime()
    {
        return totalPreparationTime;
    }
}