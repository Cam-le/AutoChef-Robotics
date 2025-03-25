using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using static UnityEngine.GraphicsBuffer;
using UnityEditor;

/// <summary>
/// Helper utility for documenting and testing robot arm joint positions
/// for reaching specific ingredients
/// </summary>
public class IngredientPositionHelper : MonoBehaviour
{
    [System.Serializable]
    public class PositionEntry
    {
        public string positionName;
        public Vector3 targetPosition;
        public float[] jointAngles = new float[6];
    }

    [Header("Robot Reference")]
    [SerializeField] private RobotArmController robotController;

    [Header("Ingredient Setup")]
    [SerializeField] private Transform[] ingredientPositions;
    [SerializeField] private string[] ingredientNames;
    [SerializeField] private Transform servingBowlPosition;

    [Header("Saved Positions")]
    [SerializeField] private List<PositionEntry> savedPositions = new List<PositionEntry>();

    [Header("Testing")]
    [SerializeField] public int currentTestIndex = 0;
    [SerializeField] private bool autoTestPositions = false;
    [SerializeField] private float testDelay = 2.0f;

    private Coroutine testCoroutine;

    void Start()
    {
        // Auto-find robot controller if not specified
        if (robotController == null)
        {
            robotController = GetComponentInParent<RobotArmController>();
            if (robotController == null)
            {
                robotController = FindObjectOfType<RobotArmController>();
            }
        }

        // Map ingredients to positions
        MapIngredientPositions();

        // Optionally start testing all positions
        if (autoTestPositions && Application.isPlaying)
        {
            StartPositionTesting();
        }
    }

    /// <summary>
    /// Automatically map ingredient positions and add them to saved positions
    /// </summary>
    public void MapIngredientPositions()
    {
        // Check if we have valid positions and names
        if (ingredientPositions.Length != ingredientNames.Length)
        {
            Debug.LogError("Ingredient positions and names arrays must be the same length");
            return;
        }

        // Add serving bowl position
        if (servingBowlPosition != null)
        {
            AddOrUpdatePosition("ServingBowl", servingBowlPosition.position);
        }

        // Map ingredient positions
        for (int i = 0; i < ingredientPositions.Length; i++)
        {
            if (ingredientPositions[i] != null && !string.IsNullOrEmpty(ingredientNames[i]))
            {
                AddOrUpdatePosition(ingredientNames[i], ingredientPositions[i].position);
            }
        }
    }

    /// <summary>
    /// Add or update a position entry
    /// </summary>
    public void AddOrUpdatePosition(string name, Vector3 position)
    {
        // Check if position already exists
        int existingIndex = savedPositions.FindIndex(p => p.positionName == name);

        if (existingIndex >= 0)
        {
            // Update existing entry
            savedPositions[existingIndex].targetPosition = position;
        }
        else
        {
            // Create new entry
            PositionEntry entry = new PositionEntry
            {
                positionName = name,
                targetPosition = position,
                jointAngles = new float[6] { 0, 0, 0, 0, 0, 0 } // Default angles
            };

            savedPositions.Add(entry);
        }
    }

    /// <summary>
    /// Update joint angles for a specific position
    /// </summary>
    public void UpdateJointAngles(string name, float[] angles)
    {
        int index = savedPositions.FindIndex(p => p.positionName == name);

        if (index >= 0 && angles.Length == 6)
        {
            System.Array.Copy(angles, savedPositions[index].jointAngles, 6);
        }
        else
        {
            Debug.LogError($"Position {name} not found or angles array incorrect length");
        }
    }

    /// <summary>
    /// Capture current joint angles for a position
    /// </summary>
    public void CaptureCurrentJointAngles(string name)
    {
        if (robotController == null || robotController.jointArticulationBodies == null)
        {
            Debug.LogError("Robot controller not set up correctly");
            return;
        }

        int index = savedPositions.FindIndex(p => p.positionName == name);

        if (index >= 0)
        {
            float[] angles = new float[6];

            for (int i = 0; i < 6 && i < robotController.jointArticulationBodies.Length; i++)
            {
                if (robotController.jointArticulationBodies[i] != null)
                {
                    angles[i] = robotController.jointArticulationBodies[i].xDrive.target;
                }
            }

            System.Array.Copy(angles, savedPositions[index].jointAngles, 6);
            Debug.Log($"Captured joint angles for position {name}");
        }
        else
        {
            Debug.LogError($"Position {name} not found");
        }
    }

    /// <summary>
    /// Export positions to C# code that can be used in the CalculateJointAngles method
    /// </summary>
    public string ExportPositionsToCode()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("// Auto-generated joint angle positions");
        sb.AppendLine("private float[] CalculateJointAngles(Vector3 targetPosition)");
        sb.AppendLine("{");
        sb.AppendLine("    // Simple position lookup table");
        sb.AppendLine("    Dictionary<string, float[]> positionMap = new Dictionary<string, float[]>");
        sb.AppendLine("    {");

        foreach (var pos in savedPositions)
        {
            // Format position as a string key (rounded to 2 decimal places)
            string posKey = $"{pos.targetPosition.x:F2},{pos.targetPosition.y:F2},{pos.targetPosition.z:F2}";

            // Format joint angles array
            string anglesStr = string.Join(", ", pos.jointAngles);

            sb.AppendLine($"        // {pos.positionName}");
            sb.AppendLine($"        {{\"{{posKey}}\", new float[] {{{{{anglesStr}}}}}}},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    // Convert position to string key for lookup (rounded to 2 decimal places)");
        sb.AppendLine("    string posKey = $\"{targetPosition.x:F2},{targetPosition.y:F2},{targetPosition.z:F2}\";");
        sb.AppendLine();
        sb.AppendLine("    // Try to find the position in our lookup");
        sb.AppendLine("    if (positionMap.TryGetValue(posKey, out float[] angles))");
        sb.AppendLine("    {");
        sb.AppendLine("        return angles;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Not an exact match, try to find the closest position");
        sb.AppendLine("    float closestDistance = float.MaxValue;");
        sb.AppendLine("    string closestKey = null;");
        sb.AppendLine();
        sb.AppendLine("    foreach (var key in positionMap.Keys)");
        sb.AppendLine("    {");
        sb.AppendLine("        string[] parts = key.Split(',');");
        sb.AppendLine("        Vector3 pos = new Vector3(");
        sb.AppendLine("            float.Parse(parts[0]), ");
        sb.AppendLine("            float.Parse(parts[1]), ");
        sb.AppendLine("            float.Parse(parts[2]));");
        sb.AppendLine();
        sb.AppendLine("        float distance = Vector3.Distance(pos, targetPosition);");
        sb.AppendLine("        if (distance < closestDistance)");
        sb.AppendLine("        {");
        sb.AppendLine("            closestDistance = distance;");
        sb.AppendLine("            closestKey = key;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (closestKey != null && closestDistance < 0.1f)");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"Using approximate position. Distance: {closestDistance}\");");
        sb.AppendLine("        return positionMap[closestKey];");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Fallback - return a safe position");
        sb.AppendLine("    Debug.LogWarning($\"No joint angles found for position {targetPosition}\");");
        sb.AppendLine("    return new float[] {0, 0, 0, 0, 0, 0};");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Save positions to a JSON file
    /// </summary>
    public void SavePositionsToFile(string filename = "robot_positions.json")
    {
        string json = JsonUtility.ToJson(new PositionList { positions = savedPositions }, true);
        File.WriteAllText(Path.Combine(Application.persistentDataPath, filename), json);
        Debug.Log($"Saved positions to {Path.Combine(Application.persistentDataPath, filename)}");
    }

    /// <summary>
    /// Load positions from a JSON file
    /// </summary>
    public void LoadPositionsFromFile(string filename = "robot_positions.json")
    {
        string path = Path.Combine(Application.persistentDataPath, filename);

        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            PositionList list = JsonUtility.FromJson<PositionList>(json);

            if (list != null && list.positions != null)
            {
                savedPositions = list.positions;
                Debug.Log($"Loaded {savedPositions.Count} positions from file");
            }
        }
        else
        {
            Debug.LogError($"File not found: {path}");
        }
    }

    /// <summary>
    /// Test robot movement to a specific position
    /// </summary>
    public void TestPosition(string name)
    {
        int index = savedPositions.FindIndex(p => p.positionName == name);

        if (index >= 0 && robotController != null)
        {
            robotController.MoveToJointAngles(savedPositions[index].jointAngles);
            Debug.Log($"Testing position {name}");
        }
        else
        {
            Debug.LogError($"Position {name} not found or robot controller not available");
        }
    }

    /// <summary>
    /// Test robot movement to a specific position by index
    /// </summary>
    public void TestPosition(int index)
    {
        if (index >= 0 && index < savedPositions.Count && robotController != null)
        {
            robotController.MoveToJointAngles(savedPositions[index].jointAngles);
            Debug.Log($"Testing position {savedPositions[index].positionName}");
        }
    }

    /// <summary>
    /// Start testing all positions sequentially
    /// </summary>
    public void StartPositionTesting()
    {
        if (testCoroutine != null)
        {
            StopCoroutine(testCoroutine);
        }

        testCoroutine = StartCoroutine(TestAllPositions());
    }

    /// <summary>
    /// Stop position testing
    /// </summary>
    public void StopPositionTesting()
    {
        if (testCoroutine != null)
        {
            StopCoroutine(testCoroutine);
            testCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine to test all positions
    /// </summary>
    private IEnumerator TestAllPositions()
    {
        currentTestIndex = 0;

        while (currentTestIndex < savedPositions.Count)
        {
            TestPosition(currentTestIndex);
            yield return new WaitForSeconds(testDelay);
            currentTestIndex++;
        }

        // Return to home position
        if (robotController != null)
        {
            robotController.MoveToHomePosition();
        }

        testCoroutine = null;
    }

    // Helper class for serialization
    [System.Serializable]
    private class PositionList
    {
        public List<PositionEntry> positions = new List<PositionEntry>();
    }
}

#if UNITY_EDITOR


[CustomEditor(typeof(IngredientPositionHelper))]
public class IngredientPositionHelperEditor : Editor
{
    private IngredientPositionHelper helper;
    private string generatedCode = "";
    private Vector2 scrollPosition;
    private bool showGeneratedCode = false;

    private void OnEnable()
    {
        helper = (IngredientPositionHelper)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Position capture controls
        EditorGUILayout.LabelField("Position Capture Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Map Ingredient Positions"))
        {
            helper.MapIngredientPositions();
        }

        if (GUILayout.Button("Capture Current Joint Angles for Selected Position"))
        {
            // Get selected position index from the helper
            int index = helper.currentTestIndex;

            // Access the savedPositions list through reflection (since it's private)
            var fieldInfo = helper.GetType().GetField("savedPositions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldInfo != null)
            {
                var savedPositions = fieldInfo.GetValue(helper) as List<IngredientPositionHelper.PositionEntry>;

                if (savedPositions != null && index >= 0 && index < savedPositions.Count)
                {
                    helper.CaptureCurrentJointAngles(savedPositions[index].positionName);
                }
            }
        }

        EditorGUILayout.Space(10);

        // Testing controls
        EditorGUILayout.LabelField("Position Testing", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Test Selected Position"))
            {
                helper.TestPosition(helper.currentTestIndex);
            }

            if (GUILayout.Button("Test All Positions"))
            {
                helper.StartPositionTesting();
            }

            if (GUILayout.Button("Stop Testing"))
            {
                helper.StopPositionTesting();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test positions", MessageType.Info);
        }

        EditorGUILayout.Space(10);

        // Export controls
        EditorGUILayout.LabelField("Export Options", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Position Code"))
        {
            generatedCode = helper.ExportPositionsToCode();
            showGeneratedCode = true;
        }

        if (GUILayout.Button("Save Positions to File"))
        {
            helper.SavePositionsToFile();
        }

        if (GUILayout.Button("Load Positions from File"))
        {
            helper.LoadPositionsFromFile();
        }

        // Show generated code
        if (showGeneratedCode && !string.IsNullOrEmpty(generatedCode))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generated Code (Copy to RobotMovementSequencer.cs)", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            EditorGUILayout.TextArea(generatedCode, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Copy to Clipboard"))
            {
                EditorGUIUtility.systemCopyBuffer = generatedCode;
                Debug.Log("Code copied to clipboard");
            }

            if (GUILayout.Button("Hide Code"))
            {
                showGeneratedCode = false;
            }
        }
    }
}
#endif