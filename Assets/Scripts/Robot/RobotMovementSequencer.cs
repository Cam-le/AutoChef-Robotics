using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// This script handles high-level movement sequences for the robot arm
/// to interact with ingredients in the scene. It builds upon the existing
/// RobotArmController.
/// </summary>
public class RobotMovementSequencer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RobotArmController robotController;

    [Header("Ingredient Positions")]
    [SerializeField] private Transform[] ingredientPositions;
    [SerializeField] private string[] ingredientNames;

    [Header("Movement Settings")]
    [SerializeField] private float approachHeight = 0.1f; // Height above ingredient to approach from
    [SerializeField] private float graspDelay = 0.5f; // Time to wait after closing gripper
    [SerializeField] private float moveDelay = 0.5f; // Time to wait between major movements

    [Header("Predefined Positions")]
    [SerializeField] private Transform servingBowlPosition; // Where to place ingredients

    // Dictionary to map ingredient names to their transforms
    private Dictionary<string, Transform> ingredientMap = new Dictionary<string, Transform>();

    // Flag to check if a sequence is currently running
    private bool isSequenceRunning = false;

    private void Start()
    {
        // Validate references
        if (robotController == null)
        {
            robotController = GetComponent<RobotArmController>();
            if (robotController == null)
            {
                Debug.LogError("No RobotArmController found. Please assign one in the inspector.");
                enabled = false;
                return;
            }
        }

        // Initialize ingredient map
        if (ingredientPositions.Length != ingredientNames.Length)
        {
            Debug.LogError("Ingredient positions and names arrays must be the same length!");
            return;
        }

        for (int i = 0; i < ingredientPositions.Length; i++)
        {
            if (ingredientPositions[i] != null && !string.IsNullOrEmpty(ingredientNames[i]))
            {
                ingredientMap[ingredientNames[i]] = ingredientPositions[i];
            }
        }
    }

    /// <summary>
    /// Picks up an ingredient by name and moves it to the serving bowl
    /// </summary>
    public void AddIngredientToServing(string ingredientName)
    {
        Debug.Log($"AddIngredientToServing called with: {ingredientName}");

        // Check if ingredient exists in map
        if (!ingredientMap.ContainsKey(ingredientName))
        {
            Debug.LogError($"Ingredient not found in ingredient map: {ingredientName}");
            Debug.Log("Current ingredient map contains:");
            foreach (var key in ingredientMap.Keys)
            {
                Debug.Log(key);
            }
            return;
        }

        if (!isSequenceRunning && ingredientMap.ContainsKey(ingredientName) && servingBowlPosition != null)
        {
            StartCoroutine(PickAndPlaceSequence(ingredientMap[ingredientName], servingBowlPosition));
        }
        else if (isSequenceRunning)
        {
            Debug.LogWarning($"Cannot add ingredient '{ingredientName}': a sequence is already running.");
        }
        else if (!ingredientMap.ContainsKey(ingredientName))
        {
            Debug.LogWarning($"Cannot add ingredient '{ingredientName}': ingredient not found.");
        }
        else
        {
            Debug.LogWarning("Cannot add ingredient: serving bowl position not defined.");
        }
    }

    /// <summary>
    /// Executes a complete cooking sequence involving multiple ingredients
    /// </summary>
    public void ProcessRecipe(string[] ingredients)
    {
        if (!isSequenceRunning)
        {
            StartCoroutine(CookingSequence(ingredients));
        }
        else
        {
            Debug.LogWarning("Cannot process recipe: a sequence is already running.");
        }
    }

    /// <summary>
    /// The main pick and place sequence that moves an object from source to destination
    /// </summary>
    private IEnumerator PickAndPlaceSequence(Transform sourcePosition, Transform destinationPosition)
    {
        isSequenceRunning = true;

        // No longer moving to ready position first

        // Calculate joint angles for hovering above the source
        Vector3 hoverPosition = sourcePosition.position + Vector3.up * approachHeight;
        float[] hoverAngles = CalculateJointAngles(hoverPosition);

        // Move directly to hover position
        robotController.MoveToJointAngles(hoverAngles);
        yield return new WaitForSeconds(moveDelay);

        // Calculate joint angles for the source position
        float[] sourceAngles = CalculateJointAngles(sourcePosition.position);

        // Move down to grasp
        robotController.MoveToJointAngles(sourceAngles);
        yield return new WaitForSeconds(moveDelay);

        // Close gripper
        robotController.CloseGripper();
        yield return new WaitForSeconds(graspDelay);

        // Move back up to hover position
        robotController.MoveToJointAngles(hoverAngles);
        yield return new WaitForSeconds(moveDelay);

        // Calculate joint angles for hovering above the destination
        Vector3 destHoverPosition = destinationPosition.position + Vector3.up * approachHeight;
        float[] destHoverAngles = CalculateJointAngles(destHoverPosition);

        // Move directly to hover above destination (no intermediate ready position)
        robotController.MoveToJointAngles(destHoverAngles);
        yield return new WaitForSeconds(moveDelay);

        // Calculate joint angles for the destination position
        float[] destAngles = CalculateJointAngles(destinationPosition.position);

        // Move down to place
        robotController.MoveToJointAngles(destAngles);
        yield return new WaitForSeconds(moveDelay);

        // Open gripper
        robotController.OpenGripper();
        yield return new WaitForSeconds(graspDelay);

        // Move back up to hover position
        robotController.MoveToJointAngles(destHoverAngles);
        yield return new WaitForSeconds(moveDelay);

        // No longer returning to home position after each ingredient

        isSequenceRunning = false;
    }

    /// <summary>
    /// Full cooking sequence that processes multiple ingredients
    /// </summary>
    private IEnumerator CookingSequence(string[] ingredients)
    {
        isSequenceRunning = true;

        foreach (string ingredient in ingredients)
        {
            if (ingredientMap.ContainsKey(ingredient))
            {
                yield return StartCoroutine(PickAndPlaceSequence(ingredientMap[ingredient], servingBowlPosition));
                // Small pause between ingredients
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                Debug.LogWarning($"Ingredient '{ingredient}' not found. Skipping.");
            }
        }

        // Only return home once at the very end of the recipe
        robotController.MoveToHomePosition();

        isSequenceRunning = false;
    }

    /// <summary>
    /// Calculates joint angles required to reach a specific position
    /// This needs to be customized for your specific robot and scene setup
    /// </summary>
    // Auto-generated joint angle positions
    private float[] CalculateJointAngles(Vector3 targetPosition)
    {
        // Simple position lookup table
        Dictionary<string, float[]> positionMap = new Dictionary<string, float[]>
        {
        // ServingBowl
        {"0.49,-1.68,5.94", new float[] {-4.546044f, -68.19195f, 22.7305f, 0, 0, 0}},
        
        // Bánh phở
        {"1.22,-2.65,4.88", new float[] {-45.46017f, -59.1003f, 4.547074f, 0, 0, 0}},
        
        // thịt bò
        {"-0.78,-1.64,4.44", new float[] {86.37861f, -40.91458f, -27.27603f, 0, 0, 0}},
        
        // thịt gà
        {"-0.78,-1.64,5.57", new float[] {31.8245f, -59.09568f, 22.73101f, 0, 0, 0}},
        
        // hành
        {"-0.78,-1.64,3.30", new float[] {136.3855f, -77.2788f, 13.638f, 0, 0, 0}},
        
        // rau thơm
        {"-0.78,-1.64,3.301", new float[] {136.3855f, -77.2788f, 13.638f, 0, 0, 0}},
        
        // nước dùng
        {"1.26,-2.65,3.43", new float[] {-136.3857f, -77.2788f, 13.638f, 0, 0, 0}},
        };

        // Convert position to string key for lookup (rounded to 2 decimal places)
        string posKey = $"{targetPosition.x:F2},{targetPosition.y:F2},{targetPosition.z:F2}";

        // Try to find the position in our lookup
        if (positionMap.TryGetValue(posKey, out float[] angles))
        {
            return angles;
        }

        // Not an exact match, try to find the closest position
        float closestDistance = float.MaxValue;
        string closestKey = null;

        foreach (var key in positionMap.Keys)
        {
            string[] parts = key.Split(',');
            Vector3 pos = new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                float.Parse(parts[2]));

            float distance = Vector3.Distance(pos, targetPosition);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestKey = key;
            }
        }

        if (closestKey != null && closestDistance < 0.1f)
        {
            Debug.Log($"Using approximate position. Distance: {closestDistance}");
            return positionMap[closestKey];
        }

        // Fallback - return a safe position
        Debug.LogWarning($"No joint angles found for position {targetPosition}");
        return new float[] { 0, 0, 0, 0, 0, 0 };
    }


    /// <summary>
    /// Public method to check if the sequencer is currently running a sequence
    /// </summary>
    public bool IsRunning()
    {
        return isSequenceRunning;
    }

    /// <summary>
    /// Add a specific ingredient position at runtime
    /// </summary>
    public void AddIngredientPosition(string name, Transform position)
    {
        if (!string.IsNullOrEmpty(name) && position != null)
        {
            ingredientMap[name] = position;
            Debug.Log($"Added ingredient position: {name}");
        }
    }
}