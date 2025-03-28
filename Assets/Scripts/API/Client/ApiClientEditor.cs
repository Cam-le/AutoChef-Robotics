#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using AutoChef.API.Client;

namespace AutoChef.API.Editor
{
    [CustomEditor(typeof(AutoChefApiClient))]
    public class ApiClientEditor : UnityEditor.Editor
    {
        private bool showApiSettings = true;
        private bool showNetworkSettings = false;
        private bool showRobotSettings = false;
        private bool showJsonConfig = true;
        private bool showLoggingSettings = false;

        public override void OnInspectorGUI()
        {
            AutoChefApiClient client = (AutoChefApiClient)target;

            // Draw script field
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(client), typeof(MonoScript), false);
            }

            EditorGUILayout.Space(10);

            // JSON Configuration Foldout
            showJsonConfig = EditorGUILayout.Foldout(showJsonConfig, "JSON Configuration", true, EditorStyles.foldoutHeader);
            if (showJsonConfig)
            {
                EditorGUI.indentLevel++;

                SerializedProperty loadConfigFromJsonProp = serializedObject.FindProperty("loadConfigFromJson");
                SerializedProperty configurationFileProp = serializedObject.FindProperty("configurationFile");
                SerializedProperty configResourcePathProp = serializedObject.FindProperty("configResourcePath");

                EditorGUILayout.PropertyField(loadConfigFromJsonProp, new GUIContent("Load From JSON", "Load API configuration from a JSON file"));

                EditorGUI.BeginDisabledGroup(!loadConfigFromJsonProp.boolValue);
                EditorGUILayout.PropertyField(configurationFileProp, new GUIContent("Configuration File", "JSON file containing API configuration"));
                EditorGUILayout.PropertyField(configResourcePathProp, new GUIContent("Resource Path", "Path to config in Resources folder if file not specified"));
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Create Default Config File"))
                {
                    client.CreateDefaultConfigFile();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Only show manual API settings if not using JSON config
            SerializedProperty useJsonConfig = serializedObject.FindProperty("loadConfigFromJson");

            using (new EditorGUI.DisabledGroupScope(useJsonConfig.boolValue))
            {
                // API Settings Foldout
                showApiSettings = EditorGUILayout.Foldout(showApiSettings, "API Settings", true, EditorStyles.foldoutHeader);
                if (showApiSettings)
                {
                    EditorGUI.indentLevel++;

                    // API Base URL
                    SerializedProperty apiBaseUrlProp = serializedObject.FindProperty("apiBaseUrl");
                    EditorGUILayout.PropertyField(apiBaseUrlProp, new GUIContent("API Base URL", "The base URL for all API calls"));

                    EditorGUILayout.Space(5);

                    // API Endpoints
                    EditorGUILayout.LabelField("API Endpoints", EditorStyles.boldLabel);
                    SerializedProperty recipesEndpointProp = serializedObject.FindProperty("recipesEndpoint");
                    SerializedProperty recipeStepsEndpointProp = serializedObject.FindProperty("recipeStepsEndpoint");
                    SerializedProperty robotStepTasksEndpointProp = serializedObject.FindProperty("robotStepTasksEndpoint");
                    SerializedProperty orderQueueEndpointProp = serializedObject.FindProperty("orderQueueEndpoint");
                    SerializedProperty orderStatusUpdateEndpointProp = serializedObject.FindProperty("orderStatusUpdateEndpoint");
                    SerializedProperty orderCancellationCheckEndpointProp = serializedObject.FindProperty("orderCancellationCheckEndpoint");

                    EditorGUILayout.PropertyField(recipesEndpointProp, new GUIContent("Recipes Endpoint", "Endpoint to fetch all recipes"));
                    EditorGUILayout.PropertyField(recipeStepsEndpointProp, new GUIContent("Recipe Steps Endpoint", "Endpoint to fetch steps for a recipe (use {0} for recipe ID)"));
                    EditorGUILayout.PropertyField(robotStepTasksEndpointProp, new GUIContent("Robot Step Tasks Endpoint", "Endpoint to fetch all robot step tasks"));
                    EditorGUILayout.PropertyField(orderQueueEndpointProp, new GUIContent("Order Queue Endpoint", "Endpoint to fetch orders from the queue"));
                    EditorGUILayout.PropertyField(orderStatusUpdateEndpointProp, new GUIContent("Order Status Update Endpoint", "Endpoint to update order status"));
                    EditorGUILayout.PropertyField(orderCancellationCheckEndpointProp, new GUIContent("Order Cancellation Check Endpoint", "Endpoint to check if an order is cancelled (use {0} for order ID)"));

                    EditorGUILayout.Space(5);

                    // API Settings
                    EditorGUILayout.LabelField("API Request Settings", EditorStyles.boldLabel);
                    SerializedProperty maxRetriesProp = serializedObject.FindProperty("maxRetries");
                    SerializedProperty initialRetryDelayMsProp = serializedObject.FindProperty("initialRetryDelayMs");
                    SerializedProperty recipePageSizeProp = serializedObject.FindProperty("recipePageSize");
                    SerializedProperty robotStepTasksPageSizeProp = serializedObject.FindProperty("robotStepTasksPageSize");

                    EditorGUILayout.PropertyField(maxRetriesProp, new GUIContent("Max Retries", "Maximum number of retry attempts for failed API calls"));
                    EditorGUILayout.PropertyField(initialRetryDelayMsProp, new GUIContent("Initial Retry Delay (ms)", "Initial delay before retrying a failed API call"));
                    EditorGUILayout.PropertyField(recipePageSizeProp, new GUIContent("Recipe Page Size", "Number of recipes to fetch per page"));
                    EditorGUILayout.PropertyField(robotStepTasksPageSizeProp, new GUIContent("Robot Tasks Page Size", "Number of robot step tasks to fetch per page"));

                    EditorGUILayout.Space(5);

                    // Test button
                    if (GUILayout.Button("Validate API Endpoints"))
                    {
                        client.ValidateApiEndpoints();
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(5);

            // Network Settings Foldout
            showNetworkSettings = EditorGUILayout.Foldout(showNetworkSettings, "Network Settings", true, EditorStyles.foldoutHeader);
            if (showNetworkSettings)
            {
                EditorGUI.indentLevel++;

                SerializedProperty pollIntervalProp = serializedObject.FindProperty("pollIntervalSeconds");
                EditorGUILayout.PropertyField(pollIntervalProp, new GUIContent("Poll Interval (seconds)", "Interval between API polling for new orders"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Logging Settings Foldout
            showLoggingSettings = EditorGUILayout.Foldout(showLoggingSettings, "Logging Settings", true, EditorStyles.foldoutHeader);
            if (showLoggingSettings)
            {
                EditorGUI.indentLevel++;

                SerializedProperty logTextAreaProp = serializedObject.FindProperty("logTextArea");
                SerializedProperty logScrollRectProp = serializedObject.FindProperty("logScrollRect");
                SerializedProperty maxLogLinesProp = serializedObject.FindProperty("maxLogLines");

                EditorGUILayout.PropertyField(logTextAreaProp, new GUIContent("Log Text Area", "UI Text component for displaying logs"));
                EditorGUILayout.PropertyField(logScrollRectProp, new GUIContent("Log Scroll Rect", "ScrollRect containing the log text"));
                EditorGUILayout.PropertyField(maxLogLinesProp, new GUIContent("Max Log Lines", "Maximum number of log lines to keep in memory"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);

            // Robot Settings Foldout
            showRobotSettings = EditorGUILayout.Foldout(showRobotSettings, "Robot Settings", true, EditorStyles.foldoutHeader);
            if (showRobotSettings)
            {
                EditorGUI.indentLevel++;

                SerializedProperty robotArmProp = serializedObject.FindProperty("robotArm");
                SerializedProperty statusTextProp = serializedObject.FindProperty("statusText");
                SerializedProperty recipeManagerProp = serializedObject.FindProperty("recipeManager");
                SerializedProperty useRandomRecipeProp = serializedObject.FindProperty("useRandomRecipe");

                EditorGUILayout.PropertyField(robotArmProp);
                EditorGUILayout.PropertyField(statusTextProp);
                EditorGUILayout.PropertyField(recipeManagerProp);
                EditorGUILayout.PropertyField(useRandomRecipeProp);

                EditorGUI.indentLevel--;
            }

            // Apply changes
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif