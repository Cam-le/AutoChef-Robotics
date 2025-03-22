using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// This utility helps you record and store joint positions for your robot.
/// It allows you to manually position the robot and save those joint angles
/// for later use in automation sequences.
/// </summary>
#if UNITY_EDITOR
[ExecuteInEditMode]
public class RobotPositionTeacher : MonoBehaviour
{
    [Header("Robot Reference")]
    [SerializeField] private RobotArmController robotController;

    [Header("Position Settings")]
    [SerializeField] private List<RobotPose> savedPoses = new List<RobotPose>();
    [SerializeField] private string newPoseName = "New Position";

    [Header("Teaching Controls")]
    [SerializeField] private int selectedJoint = 0;
    [SerializeField] private float jointAdjustSpeed = 5.0f;

    // Current joint values for teaching mode
    private float[] currentJointValues = new float[6];
    private ArticulationBody[] jointBodies;

    private void OnEnable()
    {
        if (robotController == null)
        {
            robotController = GetComponent<RobotArmController>();
        }

        if (robotController != null && robotController.jointArticulationBodies.Length > 0)
        {
            jointBodies = robotController.jointArticulationBodies;
            // Initialize current joint values
            UpdateCurrentJointValues();
        }
    }

    private void UpdateCurrentJointValues()
    {
        for (int i = 0; i < jointBodies.Length && i < currentJointValues.Length; i++)
        {
            if (jointBodies[i] != null)
            {
                currentJointValues[i] = jointBodies[i].xDrive.target;
            }
        }
    }

    /// <summary>
    /// Apply the currently edited joint values to the robot
    /// </summary>
    public void ApplyJointValues()
    {
        if (robotController != null)
        {
            robotController.MoveToJointAngles(currentJointValues);
        }
    }

    /// <summary>
    /// Save the current joint configuration as a named pose
    /// </summary>
    public void SaveCurrentPose()
    {
        if (string.IsNullOrEmpty(newPoseName))
        {
            Debug.LogWarning("Please provide a name for the pose.");
            return;
        }

        // Make a copy of the current values
        float[] jointValuesCopy = new float[currentJointValues.Length];
        Array.Copy(currentJointValues, jointValuesCopy, currentJointValues.Length);

        // Create a new pose
        RobotPose newPose = new RobotPose
        {
            poseName = newPoseName,
            jointAngles = jointValuesCopy
        };

        // Add to the list
        savedPoses.Add(newPose);

        // Reset the name field
        newPoseName = "New Position";

        Debug.Log($"Saved pose: {newPose.poseName}");
    }

    /// <summary>
    /// Load a saved pose by name
    /// </summary>
    public void LoadPose(string poseName)
    {
        RobotPose pose = savedPoses.Find(p => p.poseName == poseName);
        if (pose != null)
        {
            Array.Copy(pose.jointAngles, currentJointValues,
                       Math.Min(pose.jointAngles.Length, currentJointValues.Length));
            ApplyJointValues();
        }
    }

    /// <summary>
    /// Load a saved pose by index
    /// </summary>
    public void LoadPose(int index)
    {
        if (index >= 0 && index < savedPoses.Count)
        {
            RobotPose pose = savedPoses[index];
            Array.Copy(pose.jointAngles, currentJointValues,
                       Math.Min(pose.jointAngles.Length, currentJointValues.Length));
            ApplyJointValues();
        }
    }

    /// <summary>
    /// Delete a saved pose by name
    /// </summary>
    public void DeletePose(string poseName)
    {
        int index = savedPoses.FindIndex(p => p.poseName == poseName);
        if (index >= 0)
        {
            savedPoses.RemoveAt(index);
        }
    }

    /// <summary>
    /// Export all saved poses to a JSON string
    /// </summary>
    public string ExportPosesToJson()
    {
        return JsonUtility.ToJson(new PoseCollection { poses = savedPoses });
    }

    /// <summary>
    /// Import poses from a JSON string
    /// </summary>
    public void ImportPosesFromJson(string json)
    {
        PoseCollection collection = JsonUtility.FromJson<PoseCollection>(json);
        if (collection != null && collection.poses != null)
        {
            savedPoses = collection.poses;
        }
    }
}

/// <summary>
/// Custom Editor for the RobotPositionTeacher component that provides
/// a user-friendly interface for teaching robot positions
/// </summary>
[CustomEditor(typeof(RobotPositionTeacher))]
public class RobotPositionTeacherEditor : Editor
{
    private RobotPositionTeacher teacher;
    private SerializedProperty robotControllerProp;
    private SerializedProperty savedPosesProp;
    private SerializedProperty newPoseNameProp;
    private SerializedProperty selectedJointProp;
    private SerializedProperty jointAdjustSpeedProp;

    private void OnEnable()
    {
        teacher = (RobotPositionTeacher)target;

        robotControllerProp = serializedObject.FindProperty("robotController");
        savedPosesProp = serializedObject.FindProperty("savedPoses");
        newPoseNameProp = serializedObject.FindProperty("newPoseName");
        selectedJointProp = serializedObject.FindProperty("selectedJoint");
        jointAdjustSpeedProp = serializedObject.FindProperty("jointAdjustSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(robotControllerProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Teaching Controls", EditorStyles.boldLabel);

        // Joint selection
        EditorGUILayout.PropertyField(selectedJointProp, new GUIContent("Selected Joint"));
        EditorGUILayout.PropertyField(jointAdjustSpeedProp, new GUIContent("Adjustment Speed"));

        EditorGUILayout.Space(5);

        // Joint adjustment buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("-", GUILayout.Width(30)))
        {
            // Decrease joint angle
            // Implementation would adjust the joint angle in the teacher
        }

        EditorGUILayout.LabelField("Adjust Joint Angle", EditorStyles.miniLabel);

        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            // Increase joint angle
            // Implementation would adjust the joint angle in the teacher
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Save position controls
        EditorGUILayout.LabelField("Save Current Position", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(newPoseNameProp, new GUIContent("Position Name"));

        if (GUILayout.Button("Save Current Position"))
        {
            teacher.SaveCurrentPose();
            serializedObject.Update(); // Refresh to show the new saved pose
        }

        EditorGUILayout.Space(10);

        // Saved positions list
        EditorGUILayout.LabelField("Saved Positions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(savedPosesProp);

        EditorGUILayout.Space(5);

        // Export/Import buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export Poses"))
        {
            string json = teacher.ExportPosesToJson();
            EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log("Poses exported to clipboard");
        }

        if (GUILayout.Button("Import Poses"))
        {
            string json = EditorGUIUtility.systemCopyBuffer;
            try
            {
                teacher.ImportPosesFromJson(json);
                serializedObject.Update();
                Debug.Log("Poses imported from clipboard");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to import poses: {e.Message}");
            }
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}

/// <summary>
/// Represents a saved robot pose (position)
/// </summary>
[System.Serializable]
public class RobotPose
{
    public string poseName;
    public float[] jointAngles = new float[6]; // 6-DOF robot
}

/// <summary>
/// Collection of poses for serialization
/// </summary>
[System.Serializable]
public class PoseCollection
{
    public List<RobotPose> poses = new List<RobotPose>();
}
#endif