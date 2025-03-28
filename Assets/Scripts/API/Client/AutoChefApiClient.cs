using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using AutoChef.API.Models;
using TMPro;

namespace AutoChef.API.Client
{
    /// <summary>
    /// Handles all API communication for the AutoChef system.
    /// Fetches recipes, steps, and robot tasks from the API and coordinates with the recipe manager.
    /// </summary>
    public class AutoChefApiClient : MonoBehaviour
    {
        public enum LogType
        {
            Log,
            Warning,
            Error
        }

        [Header("Configuration")]
        [SerializeField] private bool loadConfigFromJson = false;
        [SerializeField] private TextAsset configurationFile;
        [SerializeField] private string configResourcePath = "Configs/default_api_config";

        [Header("API Configuration")]
        [SerializeField] private string apiBaseUrl = "https://autochefsystem.azurewebsites.net/api";

        [Header("API Endpoints")]
        [SerializeField] private string recipesEndpoint = "Recipe/all";
        [SerializeField] private string recipeStepsEndpoint = "recipesteps/recipe/{0}";
        [SerializeField] private string robotStepTasksEndpoint = "robot-step-tasks";
        [SerializeField] private string orderQueueEndpoint = "Order/receive-from-queue";
        [SerializeField] private string orderStatusUpdateEndpoint = "Order/update-order-status";
        [SerializeField] private string orderCancellationCheckEndpoint = "Order/check-cancelled/{0}";
        [SerializeField] private string robotOperationLogsEndpoint = "robot-operation-logs";
        
        [Header("API Settings")]
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private int initialRetryDelayMs = 1000;
        [SerializeField] private int recipePageSize = 20;
        [SerializeField] private int robotStepTasksPageSize = 1000;
        [SerializeField] private int pollIntervalSeconds = 5;

        [Header("Logging UI")]
        [SerializeField] private TextMeshProUGUI logTextArea;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private int maxLogLines = 100;

        [Header("Robot Settings")]
        [SerializeField] private Transform robotArm;
        [SerializeField] private Text statusText;
        [SerializeField] private AutoChefRecipeManager recipeManager;
        [SerializeField] private bool useRandomRecipe = false;

        private HttpClient httpClient;
        private bool isSystemOperational = false;
        private bool isProcessingOrder = false;
        private CancellationTokenSource cancellationToken;
        private OrderApiModel currentOrder;
        private List<string> logEntries = new List<string>();

        [SerializeField] private ServingBowlManager servingBowlManager;

        void Start()
        {
            httpClient = new HttpClient();

            // Load configuration from JSON if enabled
            if (loadConfigFromJson)
            {
                LoadConfigurationFromJson();
            }

            // Find servingBowlManager if not set
            if (servingBowlManager == null)
            {
                servingBowlManager = FindObjectOfType<ServingBowlManager>();
            }

            // Find recipe manager if not set
            if (recipeManager == null)
            {
                recipeManager = FindObjectOfType<AutoChefRecipeManager>();
                if (recipeManager == null)
                {
                    AddLog("AutoChefRecipeManager not found. Recipe processing may not work correctly.", LogType.Warning);
                }
            }

            // Initialize recipe data
            _ = InitializeRecipeDataAsync();

            // Start polling for orders
            StartOrderPolling();

            if (statusText != null)
            {
                statusText.text = "Initializing system...";
            }
        }

        private void LoadConfigurationFromJson()
        {
            AutoChefApiConfig config = null;

            if (configurationFile != null)
            {
                config = AutoChefApiConfig.LoadFromTextAsset(configurationFile);
                AddLog("Loaded API configuration from provided TextAsset", LogType.Log);
            }
            else if (!string.IsNullOrEmpty(configResourcePath))
            {
                config = AutoChefApiConfig.LoadFromResources(configResourcePath);
                AddLog($"Loaded API configuration from Resources/{configResourcePath}", LogType.Log);
            }

            if (config != null)
            {
                // Apply configuration
                apiBaseUrl = config.ApiBaseUrl;

                // Apply endpoints
                recipesEndpoint = config.Endpoints.RecipesEndpoint;
                recipeStepsEndpoint = config.Endpoints.RecipeStepsEndpoint;
                robotStepTasksEndpoint = config.Endpoints.RobotStepTasksEndpoint;
                orderQueueEndpoint = config.Endpoints.OrderQueueEndpoint;
                orderStatusUpdateEndpoint = config.Endpoints.OrderStatusUpdateEndpoint;
                orderCancellationCheckEndpoint = config.Endpoints.OrderCancellationCheckEndpoint;

                // Apply settings
                maxRetries = config.Settings.MaxRetries;
                initialRetryDelayMs = config.Settings.InitialRetryDelayMs;
                recipePageSize = config.Settings.RecipePageSize;
                robotStepTasksPageSize = config.Settings.RobotStepTasksPageSize;
                pollIntervalSeconds = config.Settings.PollIntervalSeconds;

                AddLog("Successfully applied API configuration", LogType.Log);
            }
            else
            {
                AddLog("Failed to load configuration, using default values", LogType.Warning);
            }
        }

        // ----- API INITIALIZATION METHODS -----

        private async Task InitializeRecipeDataAsync()
        {
            AddLog("Initializing recipe data from API...");
            UpdateStatusText("Initializing recipe data...");

            bool success = false;
            int retryCount = 0;
            int retryDelayMs = initialRetryDelayMs;

            while (!success && retryCount < maxRetries)
            {
                try
                {
                    // Fetch active recipes
                    var recipes = await FetchRecipesAsync();
                    if (recipes == null || recipes.Count == 0)
                    {
                        AddLog("No active recipes found.", LogType.Warning);
                        UpdateStatusText($"No recipes found. Retrying... ({retryCount + 1}/{maxRetries})");
                        retryCount++;
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Exponential backoff
                        continue;
                    }

                    AddLog($"Found {recipes.Count} active recipes");

                    // Fetch all robot step tasks
                    var robotStepTasks = await FetchRobotStepTasksAsync();
                    if (robotStepTasks == null)
                    {
                        AddLog("Failed to fetch robot step tasks.", LogType.Warning);
                        UpdateStatusText($"Failed to load robot tasks. Retrying... ({retryCount + 1}/{maxRetries})");
                        retryCount++;
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2;
                        continue;
                    }

                    AddLog($"Fetched {robotStepTasks.Count} robot step tasks");

                    // Process each recipe
                    Dictionary<int, List<RecipeStepApiModel>> allStepTasks = new Dictionary<int, List<RecipeStepApiModel>>();
                    foreach (var recipe in recipes)
                    {
                        var stepTasks = await FetchStepTasksForRecipeAsync(recipe.RecipeId);
                        if (stepTasks != null && stepTasks.Count > 0)
                        {
                            allStepTasks[recipe.RecipeId] = stepTasks;
                            AddLog($"Fetched {stepTasks.Count} step tasks for recipe {recipe.RecipeName}");
                        }
                    }

                    // Transfer all data to RecipeManager
                    if (recipeManager != null)
                    {
                        // Convert API data to RecipeManager format
                        var recipeData = ConvertApiDataToRecipeFormat(recipes, allStepTasks, robotStepTasks);

                        // Set the data in RecipeManager
                        recipeManager.SetRecipeData(recipeData.recipes, recipeData.operations);

                        AddLog("Successfully initialized recipe data");
                        UpdateStatusText("Recipe data loaded successfully!");
                        success = true;
                    }
                    else
                    {
                        AddLog("RecipeManager not found. Cannot initialize recipe data.", LogType.Error);
                        UpdateStatusText($"Recipe manager not found. Retrying... ({retryCount + 1}/{maxRetries})");
                        retryCount++;
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2;
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"Error initializing recipe data: {ex.Message}", LogType.Error);
                    UpdateStatusText($"API error. Retrying... ({retryCount + 1}/{maxRetries})");
                    retryCount++;
                    await Task.Delay(retryDelayMs);
                    retryDelayMs *= 2;
                }
            }

            if (!success)
            {
                AddLog("Failed to initialize recipe data after multiple attempts. System cannot operate.", LogType.Error);
                UpdateStatusText("Failed to load recipe data. System cannot operate.");
                isSystemOperational = false;
            }
            else
            {
                isSystemOperational = true;
                UpdateStatusText("Robot sẵn sàng - đang chờ đơn hàng...");
            }
        }

        // ----- API DATA FETCHING METHODS -----

        private async Task<List<RecipeApiModel>> FetchRecipesAsync()
        {
            try
            {
                AddLog("Fetching recipes from API...");

                string url = $"{apiBaseUrl}/{recipesEndpoint}?page=1&pageSize={recipePageSize}";
                AddLog($"Request URL: {url}", LogType.Log);

                HttpResponseMessage response = await httpClient.GetAsync(url);

                response.EnsureSuccessStatusCode();

                string responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<RecipeListResponse>(responseContent);

                if (apiResponse != null && apiResponse.Recipes != null)
                {
                    // Filter for active recipes only
                    return apiResponse.Recipes.Where(r => r.IsActive).ToList();
                }

                return new List<RecipeApiModel>();
            }
            catch (Exception ex)
            {
                AddLog($"Error fetching recipes: {ex.Message}", LogType.Error);
                return null;
            }
        }

        private async Task<List<RecipeStepApiModel>> FetchStepTasksForRecipeAsync(int recipeId)
        {
            try
            {
                AddLog($"Fetching step tasks for recipe ID {recipeId}...");

                // Format the endpoint with the recipe ID
                string formattedEndpoint = string.Format(recipeStepsEndpoint, recipeId);
                string url = $"{apiBaseUrl}/{formattedEndpoint}";
                AddLog($"Request URL: {url}", LogType.Log);

                HttpResponseMessage response = await httpClient.GetAsync(url);

                response.EnsureSuccessStatusCode();

                string responseContent = await response.Content.ReadAsStringAsync();
                var stepTasks = JsonConvert.DeserializeObject<List<RecipeStepApiModel>>(responseContent);

                return stepTasks ?? new List<RecipeStepApiModel>();
            }
            catch (Exception ex)
            {
                AddLog($"Error fetching step tasks for recipe {recipeId}: {ex.Message}", LogType.Error);
                return null;
            }
        }

        private async Task<List<RobotStepApiModel>> FetchRobotStepTasksAsync()
        {
            try
            {
                AddLog("Fetching robot step tasks...");

                string url = $"{apiBaseUrl}/{robotStepTasksEndpoint}?pageNumber=1&pageSize={robotStepTasksPageSize}";
                AddLog($"Request URL: {url}", LogType.Log);

                HttpResponseMessage response = await httpClient.GetAsync(url);

                response.EnsureSuccessStatusCode();

                string responseContent = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonConvert.DeserializeObject<RobotStepTaskListResponse>(responseContent);

                if (apiResponse?.Data?.Tasks != null)
                {
                    return apiResponse.Data.Tasks;
                }

                return new List<RobotStepApiModel>();
            }
            catch (Exception ex)
            {
                AddLog($"Error fetching robot step tasks: {ex.Message}", LogType.Error);
                return null;
            }
        }

        // ----- DATA CONVERSION METHODS -----

        private (AutoChefRecipeManager.Recipe[] recipes, AutoChefRecipeManager.IngredientOperations[] operations)
            ConvertApiDataToRecipeFormat(
                List<RecipeApiModel> apiRecipes,
                Dictionary<int, List<RecipeStepApiModel>> stepTasksByRecipe,
                List<RobotStepApiModel> robotStepTasks)
        {
            var recipes = new List<AutoChefRecipeManager.Recipe>();
            var operations = new List<AutoChefRecipeManager.IngredientOperations>();

            // First, create the recipe objects
            foreach (var apiRecipe in apiRecipes)
            {
                // Parse the ingredients comma-separated string
                string[] ingredientsArray = apiRecipe.GetIngredientsArray();

                var recipe = new AutoChefRecipeManager.Recipe
                {
                    recipeId = apiRecipe.RecipeId,
                    recipeName = apiRecipe.RecipeName,
                    ingredients = ingredientsArray
                };

                recipes.Add(recipe);
            }

            // Create a lookup for robot step tasks by step ID
            var robotStepTasksByStepId = robotStepTasks
                .GroupBy(rst => rst.StepId)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.TaskOrder).ToList());

            // Now create the operations for each ingredient
            var ingredientStepMap = MapIngredientsToSteps(apiRecipes, stepTasksByRecipe);

            foreach (var ingredientEntry in ingredientStepMap)
            {
                string ingredient = ingredientEntry.Key;
                List<RecipeStepApiModel> steps = ingredientEntry.Value;

                var operationSteps = new List<AutoChefRecipeManager.OperationStep>();

                foreach (var step in steps)
                {
                    // Find all robot step tasks for this step
                    if (robotStepTasksByStepId.TryGetValue(step.StepId, out var robotTasks))
                    {
                        foreach (var robotTask in robotTasks)
                        {
                            // Parse estimated time
                            float estimatedSeconds = robotTask.GetEstimatedTimeInSeconds();

                            var operationStep = new AutoChefRecipeManager.OperationStep
                            {
                                description = robotTask.TaskDescription,
                                estimatedTime = estimatedSeconds,
                                repeatCount = robotTask.RepeatCount
                            };

                            operationSteps.Add(operationStep);
                        }
                    }
                }

                if (operationSteps.Count > 0)
                {
                    var ingredientOperations = new AutoChefRecipeManager.IngredientOperations
                    {
                        ingredientName = ingredient,
                        steps = operationSteps.ToArray()
                    };

                    operations.Add(ingredientOperations);
                }
            }

            return (recipes.ToArray(), operations.ToArray());
        }

        // Helper method to map ingredients to their step tasks
        private Dictionary<string, List<RecipeStepApiModel>> MapIngredientsToSteps(
            List<RecipeApiModel> recipes,
            Dictionary<int, List<RecipeStepApiModel>> stepTasksByRecipe)
        {
            var ingredientStepMap = new Dictionary<string, List<RecipeStepApiModel>>(StringComparer.OrdinalIgnoreCase);

            foreach (var recipe in recipes)
            {
                if (!stepTasksByRecipe.TryGetValue(recipe.RecipeId, out var steps))
                {
                    continue;
                }

                string[] ingredients = recipe.GetIngredientsArray();

                foreach (var step in steps)
                {
                    // Try to find which ingredient this step is for based on description
                    string matchedIngredient = null;
                    foreach (var ingredient in ingredients)
                    {
                        if (step.StepDescription.ToLower().Contains(ingredient.ToLower()))
                        {
                            matchedIngredient = ingredient;
                            break;
                        }
                    }

                    // If we couldn't match it to an ingredient, try to guess based on common words
                    if (matchedIngredient == null)
                    {
                        if (step.StepDescription.ToLower().Contains("nước dùng".ToLower()) ||
                            step.StepDescription.ToLower().Contains("broth".ToLower()))
                        {
                            matchedIngredient = "nước dùng";
                        }
                        else if (step.StepDescription.ToLower().Contains("rau".ToLower()) ||
                                 step.StepDescription.ToLower().Contains("vegetable".ToLower()))
                        {
                            matchedIngredient = ingredients.FirstOrDefault(i =>
                                i.ToLower().Contains("rau".ToLower()));
                        }
                        // Add more heuristics as needed
                    }

                    // If we still couldn't match it, use the first ingredient as default
                    matchedIngredient ??= ingredients.FirstOrDefault();

                    if (matchedIngredient != null)
                    {
                        if (!ingredientStepMap.TryGetValue(matchedIngredient, out var stepList))
                        {
                            stepList = new List<RecipeStepApiModel>();
                            ingredientStepMap[matchedIngredient] = stepList;
                        }

                        stepList.Add(step);
                    }
                }
            }

            return ingredientStepMap;
        }

        // ----- ORDER PROCESSING METHODS -----

        private void StartOrderPolling()
        {
            AddLog("Starting order polling...");
            cancellationToken = new CancellationTokenSource();
            _ = PollForOrdersAsync(cancellationToken.Token);
        }

        private async Task PollForOrdersAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!isProcessingOrder && isSystemOperational)
                {
                    try
                    {
                        await FetchAndProcessNextOrder();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"Error during order polling: {ex.Message}", LogType.Error);
                    }
                }

                // Wait before checking again
                await Task.Delay(pollIntervalSeconds * 1000, token);
            }
        }

        private async Task FetchAndProcessNextOrder()
        {
            try
            {
                // Check if we can receive new orders
                if (servingBowlManager != null && !servingBowlManager.CanReceiveOrders())
                {
                    AddLog("Cannot receive new orders right now, bowl is being served", LogType.Log);
                    return;
                }

                AddLog("Fetching new orders from API...");

                string url = $"{apiBaseUrl}/{orderQueueEndpoint}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Check if there are new orders
                    if (!string.IsNullOrEmpty(responseContent) && responseContent != "null")
                    {
                        // Deserialize the response
                        var apiResponse = JsonConvert.DeserializeObject<GenericApiResponse>(responseContent);

                        if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Message))
                        {
                            // Deserialize the order from the message field
                            OrderApiModel order = JsonConvert.DeserializeObject<OrderApiModel>(apiResponse.Message);

                            if (order != null && order.OrderId > 0)
                            {
                                AddLog($"Received new order: {order.OrderId}");

                                // Check if order is cancelled before processing
                                bool isCancelled = await IsOrderCancelled(order.OrderId);

                                if (isCancelled)
                                {
                                    AddLog($"Order {order.OrderId} is cancelled. Skipping processing.", LogType.Warning);
                                }
                                else
                                {
                                    AddLog($"Order {order.OrderId} is not cancelled. Processing order...");
                                    await ProcessOrderAsync(order);
                                }
                            }
                        }
                    }
                    else
                    {
                        AddLog("No new orders in queue", LogType.Log);
                    }
                }
                else if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    // Only log error if not NoContent (no orders available)
                    AddLog($"Failed to fetch orders: {response.StatusCode}", LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error fetching orders: {ex.Message}", LogType.Error);
            }
        }

        private async Task<bool> IsOrderCancelled(int orderId)
        {
            try
            {
                AddLog($"Checking if order {orderId} is cancelled...");

                // Format the endpoint with the order ID
                string formattedEndpoint = string.Format(orderCancellationCheckEndpoint, orderId);
                string url = $"{apiBaseUrl}/{formattedEndpoint}";

                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Parse the response - may be a direct boolean or a JSON object
                    bool isCancelled = false;

                    // Try to parse as direct boolean
                    if (bool.TryParse(responseContent, out bool directResult))
                    {
                        isCancelled = directResult;
                    }
                    else
                    {
                        // Try to parse as JSON object
                        try
                        {
                            var result = JsonConvert.DeserializeObject<OrderCancellationResponse>(responseContent);
                            if (result != null)
                            {
                                isCancelled = result.IsCancelled;
                            }
                        }
                        catch
                        {
                            // If parsing fails, assume not cancelled
                            AddLog($"Could not parse cancellation response for order {orderId}. Assuming not cancelled.", LogType.Warning);
                            return false;
                        }
                    }

                    AddLog($"Order {orderId} cancellation status: {(isCancelled ? "Cancelled" : "Not Cancelled")}");
                    return isCancelled;
                }
                else
                {
                    // If API call fails, log error and assume not cancelled
                    AddLog($"Failed to check cancellation for order {orderId}: {response.StatusCode}", LogType.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error checking cancellation for order {orderId}: {ex.Message}", LogType.Error);
                return false; // Assume not cancelled in case of error
            }
        }

        private async Task ProcessOrderAsync(OrderApiModel order)
        {
            if (!isSystemOperational)
            {
                AddLog($"Cannot process order {order.OrderId}: System is not operational", LogType.Error);
                return;
            }

            isProcessingOrder = true;
            currentOrder = order;

            try
            {
                // Update status to "Processing"
                AddLog($"Setting order {order.OrderId} status to Processing");
                await UpdateOrderStatus(order.OrderId, "Processing");
                UpdateStatusText($"Processing order: {order.OrderId}");

                // Process the order with the robot
                AddLog($"Preparing food for order {order.OrderId}");
                await PrepareFood(order);

                // Update status to "Completed"
                AddLog($"Setting order {order.OrderId} status to Completed");
                await UpdateOrderStatus(order.OrderId, "Completed");
                UpdateStatusText($"Completed order: {order.OrderId}");

                AddLog($"Order {order.OrderId} completed successfully");
            }
            catch (Exception ex)
            {
                AddLog($"Error processing order {order.OrderId}: {ex.Message}", LogType.Error);
                await UpdateOrderStatus(order.OrderId, "Failed");
                UpdateStatusText($"Error processing order: {order.OrderId}");
            }
            finally
            {
                isProcessingOrder = false;
                currentOrder = null;
            }
        }

        /// <summary>
        /// Posts the operation log to the database 
        /// </summary>
        /// <param name="orderId">ID of the order</param>
        /// <param name="robotId">ID of the robot</param>
        /// <param name="startTime">When the operation started</param>
        /// <param name="endTime">When the operation ended</param>
        /// <param name="completionStatus">Status (e.g., "Completed", "Failed")</param>
        /// <param name="operationLog">The full operation log text</param>
        /// <returns>Task representing the async operation</returns>
        public async Task<bool> PostOperationLogAsync(int orderId, int robotId, DateTime startTime, DateTime endTime,
                                           string completionStatus, string operationLog)
        {
            try
            {
                AddLog($"Posting operation log for order {orderId}...");

                var logRequest = new RobotOperationLogRequest
                {
                    OrderId = orderId,
                    RobotId = robotId,
                    StartTime = startTime,
                    EndTime = endTime,
                    CompletionStatus = completionStatus,
                    OperationLog = operationLog
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(logRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                string url = $"{apiBaseUrl}/{robotOperationLogsEndpoint}";
                AddLog($"Request URL: {url}", LogType.Log);

                HttpResponseMessage response = await httpClient.PostAsync(url, content);

                response.EnsureSuccessStatusCode();

                AddLog($"Successfully posted operation log for order {orderId}");
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Error posting operation log: {ex.Message}", LogType.Error);
                return false;
            }
        }
        private async Task PrepareFood(OrderApiModel order)
        {
            if (recipeManager == null)
            {
                AddLog("Recipe manager not found. Cannot process food preparation.", LogType.Error);
                throw new Exception("Recipe manager not found");
            }

            // Track start time for the log
            DateTime startTime = DateTime.Now;

            // Find the target Recipe based on order.RecipeId from the recipeManager's list
            AutoChefRecipeManager.Recipe targetRecipe = null;
            int targetRecipeIndex = -1;

            // Access the recipes array safely using the helper method
            var currentRecipes = GetRecipeManagerRecipes();

            if (currentRecipes != null)
            {
                for (int i = 0; i < currentRecipes.Length; i++)
                {
                    // Ensure the recipe at this index is not null before accessing its ID
                    if (currentRecipes[i] != null && currentRecipes[i].recipeId == order.RecipeId)
                    {
                        targetRecipe = currentRecipes[i];
                        targetRecipeIndex = i;
                        break;
                    }
                }
            }
            else
            {
                AddLog("Could not retrieve recipes from RecipeManager.", LogType.Warning);
                // Decide how to proceed - maybe try random if allowed?
            }

            // Determine which recipe index to use
            int recipeIndexToProcess = -1;
            int recipeCount = currentRecipes?.Length ?? 0;

            if (!useRandomRecipe && targetRecipeIndex != -1)
            {
                // Use the specified recipe index if found and not using random
                recipeIndexToProcess = targetRecipeIndex;
                // Ensure targetRecipe is not null before accessing its name
                string recipeName = targetRecipe?.recipeName ?? "Unknown Name";
                AddLog($"Using specified recipe: ID={order.RecipeId}, Index={recipeIndexToProcess}, Name={recipeName}");
            }
            else
            {
                // Use random recipe if enabled OR if the specified RecipeId wasn't found OR if recipe list is empty/null
                if (recipeCount > 0)
                {
                    recipeIndexToProcess = UnityEngine.Random.Range(0, recipeCount);
                    string reason = useRandomRecipe ? "Random recipe enabled" : $"Specified RecipeId {order.RecipeId} not found or invalid";
                    // Check if targetRecipeIndex was -1 and provide more specific reason
                    if (!useRandomRecipe && targetRecipeIndex == -1 && currentRecipes != null)
                    {
                        reason = $"Specified RecipeId {order.RecipeId} not found among {recipeCount} loaded recipes";
                    }
                    else if (currentRecipes == null)
                    {
                        reason = "Recipe list unavailable, using random index";
                    }

                    AddLog($"Using random recipe index: {recipeIndexToProcess} ({reason})");
                }
                else
                {
                    AddLog("No recipes available in RecipeManager. Cannot prepare food.", LogType.Error);
                    // Update status immediately and throw exception
                    //UpdateStatus($"Error: No recipes loaded for Order {order.OrderId}");
                    throw new Exception($"No recipes available to process order {order.OrderId}.");
                }
            }

            // Double-check if recipeIndexToProcess is valid before proceeding
            if (recipeIndexToProcess < 0 || recipeIndexToProcess >= recipeCount)
            {
                AddLog($"Invalid recipe index {recipeIndexToProcess} determined. Cannot proceed with Order {order.OrderId}.", LogType.Error);
                //UpdateStatus($"Error: Invalid recipe index for Order {order.OrderId}");
                throw new Exception($"Invalid recipe index determined for Order {order.OrderId}.");
            }


            // Process the recipe using the determined index and the actual Order ID
            AddLog($"Starting recipe processing for Order ID: {order.OrderId} using Recipe Index: {recipeIndexToProcess}");
            // --- MODIFIED CALL ---
            // Ensure recipeManager is still valid before calling
            if (recipeManager != null)
            {
                recipeManager.ProcessRecipe(recipeIndexToProcess, order.OrderId);
            }
            else
            {
                AddLog($"RecipeManager became unavailable before starting ProcessRecipe for Order {order.OrderId}.", LogType.Error);
                throw new Exception($"RecipeManager unavailable for Order {order.OrderId}.");
            }
            // ---------------------

            AddLog($"Waiting for robot to cook Order {order.OrderId}...");

            // Monitor the recipe processing status
            string status = "Starting"; // Initial status before first check
            int checkIntervalMs = 500; // Check every 500ms
            int maxWaitTimeMs = 180000; // Maximum wait time: 3 minutes
            int elapsedTimeMs = 0;

            while (elapsedTimeMs < maxWaitTimeMs)
            {
                // Check recipeManager availability in the loop
                if (recipeManager == null)
                {
                    AddLog($"RecipeManager became unavailable during processing of Order {order.OrderId}.", LogType.Error);
                    status = "Failed"; // Mark as failed if manager disappears
                    break;
                }

                status = recipeManager.GetStatus();

                // Check if processing is complete or failed
                if (status == "Completed" || status == "Failed")
                {
                    break;
                }

                // Update status text for user feedback
                UpdateStatusText($"Order {order.OrderId}: Cooking... ({elapsedTimeMs / 1000}s / {maxWaitTimeMs / 1000}s) - Status: {status}");


                // Wait before checking again
                // Use try-catch for Task.Delay in case cancellation is requested during delay
                try
                {
                    await Task.Delay(checkIntervalMs, cancellationToken?.Token ?? CancellationToken.None);
                    // Check for cancellation immediately after delay
                    if (cancellationToken != null && cancellationToken.IsCancellationRequested)
                    {
                        AddLog($"Operation cancelled while waiting for recipe completion (Order {order.OrderId}).", LogType.Warning);
                        status = "Cancelled"; // Or handle as needed
                        break;
                    }
                }
                catch (TaskCanceledException)
                {
                    AddLog($"Task cancelled during delay for Order {order.OrderId}.", LogType.Warning);
                    status = "Cancelled"; // Or handle as needed
                    break;
                }

                elapsedTimeMs += checkIntervalMs;

                // Log progress periodically (e.g., every 5 seconds)
                if (elapsedTimeMs % 5000 < checkIntervalMs)
                {
                    AddLog($"Still processing recipe for Order {order.OrderId}... ({elapsedTimeMs / 1000}s elapsed, Status: {status})");
                }
            }

            // After loop finishes (completion, failure, timeout, or cancellation)
            // Get operation log regardless of outcome
            string operationLog = "Log unavailable";
            if (recipeManager != null)
            {
                try
                {
                    operationLog = recipeManager.GetOperationLog();
                    
                }
                catch (Exception logEx)
                {
                    AddLog($"Error getting operation log from RecipeManager: {logEx.Message}", LogType.Warning);
                }
            }
            else
            {
                operationLog = "Recipe Manager unavailable post-processing.";
            }


            // Handle final status
            if (status == "Completed")
            {
                AddLog($"Recipe for Order {order.OrderId} completed successfully!");
                AddLog($"Operation log captured ({operationLog.Length} characters)");
                operationLog = recipeManager.GetOperationLog();
                // Post the operation log to the database
                DateTime endTime = DateTime.Now;
                await PostOperationLogAsync(
                    order.OrderId,
                    order.RobotId,
                    startTime,
                    endTime,
                    status,
                    operationLog
                );
            }
            else if (status == "Failed")
            {
                AddLog($"Recipe processing failed for Order {order.OrderId}!", LogType.Error);
                AddLog($"Failed Operation log captured ({operationLog.Length} characters)");
                throw new Exception($"Recipe processing failed for Order {order.OrderId}. Final Status: {status}");
            }
            else if (status == "Cancelled")
            {
                AddLog($"Recipe processing was cancelled for Order {order.OrderId}.", LogType.Warning);
                // Decide if cancellation should be treated as an error or a specific state
                // For now, throw exception to indicate it didn't complete normally
                throw new OperationCanceledException($"Recipe processing cancelled for Order {order.OrderId}.");
            }
            else // Timeout case (status wasn't Completed or Failed within maxWaitTimeMs)
            {
                AddLog($"Recipe processing timed out for Order {order.OrderId}! (Status: {status})", LogType.Warning);
                AddLog($"Timeout Operation log captured ({operationLog.Length} characters)");
                // Optionally force status to Failed if timeout occurs
                // Consider calling a method on recipeManager to force stop/fail if it exists
                throw new TimeoutException($"Recipe processing timed out after {maxWaitTimeMs / 1000}s for Order {order.OrderId}. Last known status: {status}");
            }
        }

        // ----- HELPER METHODS -----

        /// <summary>
        /// Safely retrieves the recipes array from the AutoChefRecipeManager.
        /// Uses reflection as a fallback if a direct getter isn't available.
        /// </summary>
        /// <returns>The array of Recipe objects from the manager, or null if unavailable.</returns>
        private AutoChefRecipeManager.Recipe[] GetRecipeManagerRecipes()
        {
            if (recipeManager == null)
            {
                AddLog("GetRecipeManagerRecipes called, but RecipeManager is null.", LogType.Warning);
                return null;
            }

            // OPTION 1: Ideal - Use a public getter if you add one to AutoChefRecipeManager
            // Example: if (recipeManager.TryGetRecipes(out var recipes)) { return recipes; }
            // Example: return recipeManager.GetRecipes(); // If GetRecipes() is added

            // OPTION 2: Reflection (Current implementation, less robust)
            try
            {
                // Cache the FieldInfo for performance if this is called often
                var recipesField = typeof(AutoChefRecipeManager).GetField("recipes",
                   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (recipesField != null)
                {
                    var recipesValue = recipesField.GetValue(recipeManager);
                    if (recipesValue is AutoChefRecipeManager.Recipe[] recipesArray)
                    {
                        return recipesArray;
                    }
                    else
                    {
                        AddLog($"Field 'recipes' in RecipeManager is not of type Recipe[]. Actual type: {recipesValue?.GetType().Name ?? "null"}", LogType.Warning);
                        return null;
                    }
                }
                else
                {
                    AddLog("Could not find private field 'recipes' in RecipeManager via reflection.", LogType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error accessing recipes via reflection: {ex.Message}", LogType.Error);
                return null; // Return null if reflection fails
            }
        }

        private async Task UpdateOrderStatus(int orderId, string status)
        {
            try
            {
                AddLog($"Updating order {orderId} status to {status}");

                var statusUpdate = new
                {
                    OrderId = orderId,
                    Status = status
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(statusUpdate),
                    Encoding.UTF8,
                    "application/json"
                );

                string url = $"{apiBaseUrl}/{orderStatusUpdateEndpoint}";
                var response = await httpClient.PutAsync(url, content);

                response.EnsureSuccessStatusCode();
                AddLog($"Successfully updated order {orderId} status to {status}");
            }
            catch (Exception ex)
            {
                AddLog($"Error updating order status: {ex.Message}", LogType.Error);
                throw;
            }
        }

        // ----- UI AND LOGGING METHODS -----

        private void AddLog(string message, LogType logType = LogType.Log)
        {
            // Format timestamp
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMessage = $"[{timestamp}] {message}";

            // Log to console based on type
            switch (logType)
            {
                case LogType.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogType.Error:
                    Debug.LogError(formattedMessage);
                    break;
                default:
                    Debug.Log(formattedMessage);
                    break;
            }

            // Add to log entries
            logEntries.Add(formattedMessage);

            // Trim log if it gets too long
            if (logEntries.Count > maxLogLines)
            {
                logEntries.RemoveAt(0);
            }

            // Update UI if available
            if (logTextArea != null)
            {
                logTextArea.text = string.Join("\n", logEntries);
            }
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null; // Wait for next frame
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 0f;
        }

        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        void OnDestroy()
        {
            AddLog("API client shutting down");
            // Cancel polling task when GameObject is destroyed
            cancellationToken?.Cancel();
        }

        // ----- EDITOR METHODS -----

#if UNITY_EDITOR
        public void ValidateApiEndpoints()
        {
            // Trim any leading or trailing slashes from the base URL
            apiBaseUrl = apiBaseUrl.TrimEnd('/');

            // Ensure recipe steps endpoint has the formatting placeholder
            if (!recipeStepsEndpoint.Contains("{0}"))
            {
                Debug.LogWarning("Recipe steps endpoint should contain a {0} placeholder for the recipe ID.");
            }

            // Log the complete URLs for verification
            Debug.Log($"Recipes URL: {apiBaseUrl}/{recipesEndpoint}?page=1&pageSize={recipePageSize}");
            Debug.Log($"Recipe Steps URL format: {apiBaseUrl}/{recipeStepsEndpoint}");
            Debug.Log($"Robot Step Tasks URL: {apiBaseUrl}/{robotStepTasksEndpoint}?pageNumber=1&pageSize={robotStepTasksPageSize}");
            Debug.Log($"Order Queue URL: {apiBaseUrl}/{orderQueueEndpoint}");
            Debug.Log($"Order Status Update URL: {apiBaseUrl}/{orderStatusUpdateEndpoint}");
            Debug.Log($"Order Cancellation Check URL format: {apiBaseUrl}/{orderCancellationCheckEndpoint}");
        }

        public void CreateDefaultConfigFile()
        {
            AutoChefApiConfig defaultConfig = new AutoChefApiConfig
            {
                ApiBaseUrl = apiBaseUrl,
                Endpoints = new ApiEndpointConfig
                {
                    RecipesEndpoint = recipesEndpoint,
                    RecipeStepsEndpoint = recipeStepsEndpoint,
                    RobotStepTasksEndpoint = robotStepTasksEndpoint,
                    OrderQueueEndpoint = orderQueueEndpoint,
                    OrderStatusUpdateEndpoint = orderStatusUpdateEndpoint,
                    OrderCancellationCheckEndpoint = orderCancellationCheckEndpoint
                },
                Settings = new ApiRequestSettings
                {
                    MaxRetries = maxRetries,
                    InitialRetryDelayMs = initialRetryDelayMs,
                    RecipePageSize = recipePageSize,
                    RobotStepTasksPageSize = robotStepTasksPageSize,
                    PollIntervalSeconds = pollIntervalSeconds
                }
            };

            string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);

            string path = UnityEditor.EditorUtility.SaveFilePanel(
                "Save API Configuration",
                Application.dataPath,
                "api_config.json",
                "json");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                UnityEditor.AssetDatabase.Refresh();
                Debug.Log($"Configuration file created at: {path}");
            }
        }
#endif
    }

    // ----- API DATA MODELS -----

    [System.Serializable]
    public class RecipeApiModel
    {
        [JsonProperty("recipeId")]
        public int RecipeId { get; set; }

        [JsonProperty("recipeName")]
        public string RecipeName { get; set; }

        [JsonProperty("ingredients")]
        public string Ingredients { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        // Helper method to parse ingredients string to array
        public string[] GetIngredientsArray()
        {
            if (string.IsNullOrEmpty(Ingredients))
                return new string[0];

            return Ingredients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .ToArray();
        }
    }

    [System.Serializable]
    public class RecipeStepApiModel
    {
        [JsonProperty("stepId")]
        public int StepId { get; set; }

        [JsonProperty("recipeId")]
        public int RecipeId { get; set; }

        [JsonProperty("stepDescription")]
        public string StepDescription { get; set; }

        [JsonProperty("stepNumber")]
        public int StepNumber { get; set; }
    }

    [System.Serializable]
    public class RobotStepApiModel
    {
        [JsonProperty("stepTaskId")]
        public int StepTaskId { get; set; }

        [JsonProperty("stepId")]
        public int StepId { get; set; }

        [JsonProperty("taskDescription")]
        public string TaskDescription { get; set; }

        [JsonProperty("taskOrder")]
        public int TaskOrder { get; set; }

        [JsonProperty("estimatedTime")]
        public string EstimatedTime { get; set; }

        [JsonProperty("repeatCount")]
        public int RepeatCount { get; set; }

        // Helper method to convert EstimatedTime to seconds
        public float GetEstimatedTimeInSeconds()
        {
            if (TimeSpan.TryParse(EstimatedTime, out TimeSpan timeSpan))
                return (float)timeSpan.TotalSeconds;

            return 1.0f; // Default to 1 second if parsing fails
        }
    }

    [System.Serializable]
    public class OrderApiModel
    {
        [JsonProperty("orderId")]
        public int OrderId { get; set; }

        [JsonProperty("recipeId")]
        public int RecipeId { get; set; }

        [JsonProperty("robotId")]
        public int RobotId { get; set; }

        [JsonProperty("locationId")]
        public int LocationId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("orderedTime")]
        public DateTime OrderedTime { get; set; }
    }

    [System.Serializable]
    public class RecipeListResponse
    {
        [JsonProperty("recipes")]
        public List<RecipeApiModel> Recipes { get; set; } = new List<RecipeApiModel>();

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
    }

    [System.Serializable]
    public class RobotStepTaskListResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public RobotStepTaskResponseData Data { get; set; }
    }

    [System.Serializable]
    public class RobotStepTaskResponseData
    {
        [JsonProperty("tasks")]
        public List<RobotStepApiModel> Tasks { get; set; } = new List<RobotStepApiModel>();

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
    }

    [System.Serializable]
    public class GenericApiResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    [System.Serializable]
    public class OrderCancellationResponse
    {
        [JsonProperty("isCancelled")]
        public bool IsCancelled { get; set; }
    }
}