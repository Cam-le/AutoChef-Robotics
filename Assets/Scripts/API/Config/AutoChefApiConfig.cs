using Newtonsoft.Json;
using System;
using UnityEngine;

namespace AutoChef.API.Client
{
    /// <summary>
    /// Configuration class for the AutoChef API connections.
    /// Supports loading from JSON files and Resources.
    /// </summary>
    [Serializable]
    public class AutoChefApiConfig
    {
        public string ApiBaseUrl = "";
        public ApiEndpointConfig Endpoints = new ApiEndpointConfig();
        public ApiRequestSettings Settings = new ApiRequestSettings();

        /// <summary>
        /// Load configuration from a JSON string
        /// </summary>
        public static AutoChefApiConfig LoadFromJson(string jsonText)
        {
            try
            {
                var config = JsonConvert.DeserializeObject<AutoChefApiConfig>(jsonText);
                return config ?? new AutoChefApiConfig();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load API config from JSON: {ex.Message}");
                return new AutoChefApiConfig();
            }
        }

        /// <summary>
        /// Load configuration from a TextAsset
        /// </summary>
        public static AutoChefApiConfig LoadFromTextAsset(TextAsset configAsset)
        {
            try
            {
                if (configAsset == null)
                {
                    Debug.LogWarning("Config TextAsset is null");
                    return new AutoChefApiConfig();
                }

                return LoadFromJson(configAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load API config from TextAsset: {ex.Message}");
                return new AutoChefApiConfig();
            }
        }

        /// <summary>
        /// Load configuration from Resources folder
        /// </summary>
        public static AutoChefApiConfig LoadFromResources(string resourcePath)
        {
            try
            {
                TextAsset configAsset = Resources.Load<TextAsset>(resourcePath);
                if (configAsset == null)
                {
                    Debug.LogWarning($"Config file not found at Resources/{resourcePath}");
                    return new AutoChefApiConfig();
                }

                return LoadFromTextAsset(configAsset);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load API config from Resources: {ex.Message}");
                return new AutoChefApiConfig();
            }
        }

        /// <summary>
        /// Convert the configuration to JSON
        /// </summary>
        public string ToJson(bool prettyPrint = true)
        {
            return JsonConvert.SerializeObject(this,
                prettyPrint ? Formatting.Indented : Formatting.None);
        }
    }

    /// <summary>
    /// Configuration for API endpoints
    /// </summary>
    [Serializable]
    public class ApiEndpointConfig
    {
        public string RecipesEndpoint = "Recipe/all";
        public string RecipeStepsEndpoint = "recipesteps/recipe/{0}"; // {0} will be replaced with recipeId
        public string RobotStepTasksEndpoint = "robot-step-tasks";
        public string OrderQueueEndpoint = "Order/receive-from-queue";
        public string OrderStatusUpdateEndpoint = "Order/update-order-status";
        public string OrderCancellationCheckEndpoint = "Order/check-cancelled/{0}"; // {0} will be replaced with orderId
        public string RobotOperationLogsEndpoint = "robot-operation-logs";
    }

    /// <summary>
    /// Configuration for API request settings
    /// </summary>
    [Serializable]
    public class ApiRequestSettings
    {
        public int MaxRetries = 3;
        public int InitialRetryDelayMs = 1000;
        public int RecipePageSize = 20;
        public int RobotStepTasksPageSize = 1000;
        public int PollIntervalSeconds = 5;
    }
}