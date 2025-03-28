using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AutoChef.API.Client;
using TMPro;
using AutoChef;

/// <summary>
/// Displays current order processing information including Order ID, Recipe Name, and Status
/// </summary>
public class OrderProcessingDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI orderIdText;
    [SerializeField] private TextMeshProUGUI recipeNameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image statusBackground;

    [Header("Status Colors")]
    [SerializeField] private Color waitingColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color processingColor = new Color(0.2f, 0.6f, 1.0f);
    [SerializeField] private Color completedColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color failedColor = new Color(0.8f, 0.2f, 0.2f);

    [Header("References")]
    [SerializeField] private AutoChefApiClient apiClient;
    [SerializeField] private AutoChefRecipeManager recipeManager;

    // Reference to the current order (obtained via reflection from ApiClient)
    private object currentOrder;
    private string currentStatus = "Waiting";
    private int currentOrderId = -1;
    private string currentRecipeName = "None";

    private void Start()
    {
        // Auto-find references if not set
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<AutoChefApiClient>();
        }

        if (recipeManager == null)
        {
            recipeManager = FindObjectOfType<AutoChefRecipeManager>();
        }

        // Initialize UI
        UpdateOrderDisplay(-1, "None", "Waiting");
    }

    private void Update()
    {
        // Check if we have an API client
        if (apiClient != null)
        {
            // Use reflection to get current order info from ApiClient
            var currentOrderField = apiClient.GetType().GetField("currentOrder",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (currentOrderField != null)
            {
                var orderObj = currentOrderField.GetValue(apiClient);

                if (orderObj != null && orderObj != currentOrder)
                {
                    // Order has changed, update the reference
                    currentOrder = orderObj;

                    // Get the order ID
                    var orderIdProp = orderObj.GetType().GetProperty("OrderId");
                    int orderId = (int)orderIdProp.GetValue(orderObj);

                    // Get the recipe ID
                    var recipeIdProp = orderObj.GetType().GetProperty("RecipeId");
                    int recipeId = (int)recipeIdProp.GetValue(orderObj);

                    // Get recipe name from manager using recipeId
                    string recipeName = GetRecipeNameById(recipeId);

                    // Update display with new order information
                    UpdateOrderDisplay(orderId, recipeName, "Processing");
                }
                else if (orderObj == null && currentOrder != null)
                {
                    // Order has been completed or cleared
                    currentOrder = null;

                    // Get status from recipe manager
                    if (recipeManager != null)
                    {
                        string status = recipeManager.GetStatus();

                        if (status == "Completed")
                        {
                            // Keep the last order info visible but update status
                            UpdateStatus("Completed");
                        }
                        else if (status == "Failed")
                        {
                            UpdateStatus("Failed");
                        }
                        else
                        {
                            // Reset display for next order
                            UpdateOrderDisplay(-1, "None", "Waiting");
                        }
                    }
                    else
                    {
                        // No recipe manager, just reset display
                        UpdateOrderDisplay(-1, "None", "Waiting");
                    }
                }
            }
        }

        // If we have a recipe manager, continuously update status from it
        if (recipeManager != null && currentOrder != null)
        {
            string status = recipeManager.GetStatus();
            if (status != currentStatus)
            {
                UpdateStatus(status);
            }
        }
    }

    private string GetRecipeNameById(int recipeId)
    {
        if (recipeManager != null)
        {
            // Access the recipes array via reflection
            var recipesField = recipeManager.GetType().GetField("recipes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (recipesField != null)
            {
                var recipes = recipesField.GetValue(recipeManager) as AutoChef.AutoChefRecipeManager.Recipe[];

                if (recipes != null)
                {
                    foreach (var recipe in recipes)
                    {
                        if (recipe.recipeId == recipeId)
                        {
                            return recipe.recipeName;
                        }
                    }
                }
            }
        }

        return $"Recipe #{recipeId}";
    }

    private void UpdateOrderDisplay(int orderId, string recipeName, string status)
    {
        currentOrderId = orderId;
        currentRecipeName = recipeName;

        // Update UI elements
        if (orderIdText != null)
        {
            orderIdText.text = orderId > 0 ? $"Order #{orderId}" : "No Active Order";
        }

        if (recipeNameText != null)
        {
            recipeNameText.text = recipeName;
        }

        // Also update status
        UpdateStatus(status);
    }

    private void UpdateStatus(string status)
    {
        currentStatus = status;

        if (statusText != null)
        {
            statusText.text = status;
        }

        if (statusBackground != null)
        {
            // Set color based on status
            switch (status.ToLower())
            {
                case "waiting":
                    statusBackground.color = waitingColor;
                    break;
                case "processing":
                    statusBackground.color = processingColor;
                    break;
                case "completed":
                    statusBackground.color = completedColor;
                    break;
                case "failed":
                    statusBackground.color = failedColor;
                    break;
                default:
                    statusBackground.color = processingColor;
                    break;
            }
        }
    }
}