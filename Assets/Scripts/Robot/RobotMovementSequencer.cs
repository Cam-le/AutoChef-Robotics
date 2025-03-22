using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// This script handles higher-level movement sequences for the robot arm
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
    [SerializeField] private Transform cookingPosition; // Where to drop ingredients for cooking
    [SerializeField] private Transform servingPosition; // Where to place final food

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
    /// Picks up an ingredient by name and moves it to the cooking position
    /// </summary>
    public void AddIngredientToCooking(string ingredientName)
    {
        if (!isSequenceRunning && ingredientMap.ContainsKey(ingredientName))
        {
            StartCoroutine(PickAndPlaceSequence(ingredientMap[ingredientName], cookingPosition));
        }
        else
        {
            Debug.LogWarning($"Cannot add ingredient '{ingredientName}': either a sequence is already running or the ingredient wasn't found.");
        }
    }

    /// <summary>
    /// Moves food from cooking position to serving position
    /// </summary>
    public void ServeDish()
    {
        if (!isSequenceRunning && cookingPosition != null && servingPosition != null)
        {
            StartCoroutine(PickAndPlaceSequence(cookingPosition, servingPosition));
        }
    }

    /// <summary>
    /// Executes a complete cooking sequence involving multiple ingredients
    /// </summary>
    public void CookRecipe(string[] ingredients)
    {
        if (!isSequenceRunning)
        {
            StartCoroutine(CookingSequence(ingredients));
        }
    }

    /// <summary>
    /// The main pick and place sequence that moves an object from source to destination
    /// </summary>
    private IEnumerator PickAndPlaceSequence(Transform sourcePosition, Transform destinationPosition)
    {
        isSequenceRunning = true;

        // Move to ready position first
        robotController.MoveToReadyPosition();
        yield return new WaitForSeconds(moveDelay);

        // Approach source from above
        yield return StartCoroutine(ApproachPosition(sourcePosition.position, approachHeight));

        // Move down to grasp
        yield return StartCoroutine(MoveToPosition(sourcePosition.position));

        // Close gripper
        robotController.CloseGripper();
        yield return new WaitForSeconds(graspDelay);

        // Move back up
        yield return StartCoroutine(ApproachPosition(sourcePosition.position, approachHeight));

        // Move to temporary position to avoid collisions
        robotController.MoveToReadyPosition();
        yield return new WaitForSeconds(moveDelay);

        // Approach destination from above
        yield return StartCoroutine(ApproachPosition(destinationPosition.position, approachHeight));

        // Move down to place
        yield return StartCoroutine(MoveToPosition(destinationPosition.position));

        // Open gripper
        robotController.OpenGripper();
        yield return new WaitForSeconds(graspDelay);

        // Move back up
        yield return StartCoroutine(ApproachPosition(destinationPosition.position, approachHeight));

        // Return to home position
        robotController.MoveToHomePosition();
        yield return new WaitForSeconds(moveDelay);

        isSequenceRunning = false;
    }

    /// <summary>
    /// Full cooking sequence that processes multiple ingredients
    /// </summary>
    private IEnumerator CookingSequence(string[] ingredients)
    {
        isSequenceRunning = true;

        // Add each ingredient
        foreach (string ingredient in ingredients)
        {
            if (ingredientMap.ContainsKey(ingredient))
            {
                yield return StartCoroutine(PickAndPlaceSequence(ingredientMap[ingredient], cookingPosition));
                // Simulate some processing time
                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                Debug.LogWarning($"Ingredient '{ingredient}' not found. Skipping.");
            }
        }

        // Move to serving position
        yield return StartCoroutine(PickAndPlaceSequence(cookingPosition, servingPosition));

        isSequenceRunning = false;
    }

    /// <summary>
    /// Moves the robot arm to approach a position from above
    /// </summary>
    private IEnumerator ApproachPosition(Vector3 targetPosition, float heightOffset)
    {
        // Create an elevated position above the target
        Vector3 elevatedPosition = new Vector3(
            targetPosition.x,
            targetPosition.y + heightOffset,
            targetPosition.z
        );

        // Calculate joint angles for this position
        float[] jointAngles = CalculateJointAngles(elevatedPosition);

        // Move to the calculated angles
        robotController.MoveToJointAngles(jointAngles);

        // Wait for movement to complete
        yield return new WaitForSeconds(moveDelay);
    }

    /// <summary>
    /// Moves the robot arm directly to a position (for final approach)
    /// </summary>
    private IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        // Calculate joint angles for this position
        float[] jointAngles = CalculateJointAngles(targetPosition);

        // Move to the calculated angles
        robotController.MoveToJointAngles(jointAngles);

        // Wait for movement to complete
        yield return new WaitForSeconds(moveDelay);
    }

    /// <summary>
    /// Calculates joint angles required to reach a specific position
    /// This is a placeholder for inverse kinematics - in a real implementation,
    /// you would implement proper IK here
    /// </summary>
    private float[] CalculateJointAngles(Vector3 targetPosition)
    {
        // This is a placeholder - in reality, you'd implement inverse kinematics here
        // For now, we'll provide some dummy values that you can replace with actual values
        // for specific positions in your scene

        // For a real implementation:
        // 1. You might use Unity's built-in IK system
        // 2. Or implement analytical IK for a 6-DOF arm
        // 3. Or use a lookup table with pre-calculated values for known positions

        // Return default values - these should be replaced with actual joints for your setup
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
        }
    }
}