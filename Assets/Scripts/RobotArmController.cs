using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics;
using UnityEngine.Events;

public class RobotArmController : MonoBehaviour
{
    // References to articulation bodies
    [Header("Robot Joints")]
    public ArticulationBody[] jointArticulationBodies;

    [Header("Movement Settings")]
    public float jointSpeed = 30.0f;
    public float jointAcceleration = 10.0f;

    // Constants
    private const int NUM_ROBOT_JOINTS = 6;
    private const float JOINT_ASSIGNMENT_WAIT = 0.1f;

    [Header("Gripper Settings")]
    public ArticulationBody leftGripper;
    public ArticulationBody rightGripper;
    public float gripperOpenAngle = 35.0f;
    public float gripperClosedAngle = 0.0f;

    // Unity Events for integration with UI
    public UnityEvent OnGripperOpen;
    public UnityEvent OnGripperClose;

    void Start()
    {
        // Initialize the robot
        if (jointArticulationBodies.Length == 0)
        {
            // Auto-populate joints if not assigned
            PopulateJoints();
        }

        ConfigureJoints();
    }

    // Find and assign robot joints automatically
    private void PopulateJoints()
    {
        jointArticulationBodies = new ArticulationBody[NUM_ROBOT_JOINTS];

        // Find ArticulationBodies in the robot hierarchy
        // This assumes the standard naming of the Niryo One joints
        string[] jointNames = { "joint_1", "joint_2", "joint_3", "joint_4", "joint_5", "joint_6" };

        for (int i = 0; i < NUM_ROBOT_JOINTS; i++)
        {
            // Find the joint in the hierarchy
            Transform joint = transform.Find($"world/base_link/{jointNames[i]}");
            if (joint != null)
            {
                jointArticulationBodies[i] = joint.GetComponent<ArticulationBody>();
                if (jointArticulationBodies[i] == null)
                {
                    Debug.LogError($"Could not find ArticulationBody component on {jointNames[i]}");
                }
            }
            else
            {
                Debug.LogError($"Could not find joint named {jointNames[i]}");
            }
        }

        // Find grippers
        Transform leftGripperTransform = transform.Find("world/base_link/joint_6/tool_link/gripper_base/servo_head/left_gripper");
        Transform rightGripperTransform = transform.Find("world/base_link/joint_6/tool_link/gripper_base/servo_head/right_gripper");

        if (leftGripperTransform != null && rightGripperTransform != null)
        {
            leftGripper = leftGripperTransform.GetComponent<ArticulationBody>();
            rightGripper = rightGripperTransform.GetComponent<ArticulationBody>();
        }
        else
        {
            Debug.LogWarning("Could not find gripper components. Gripper functionality will be disabled.");
        }
    }

    // Configure the joint settings
    private void ConfigureJoints()
    {
        foreach (ArticulationBody joint in jointArticulationBodies)
        {
            if (joint != null)
            {
                joint.jointFriction = 5.0f;

                var drive = joint.xDrive;
                drive.forceLimit = 1000;
                drive.stiffness = 10000;
                drive.damping = 100;
                joint.xDrive = drive;
            }
        }
    }

    // Move a specific joint to a target angle
    public void SetJointAngle(int jointIndex, float targetAngle)
    {
        if (jointIndex < 0 || jointIndex >= jointArticulationBodies.Length)
        {
            Debug.LogError($"Joint index {jointIndex} out of range");
            return;
        }

        if (jointArticulationBodies[jointIndex] == null)
        {
            Debug.LogError($"Joint at index {jointIndex} is not assigned");
            return;
        }

        var drive = jointArticulationBodies[jointIndex].xDrive;
        drive.target = targetAngle;
        jointArticulationBodies[jointIndex].xDrive = drive;
    }

    // Move all joints to specified angles
    public void MoveToJointAngles(float[] jointAngles)
    {
        if (jointAngles.Length != jointArticulationBodies.Length)
        {
            Debug.LogError($"Expected {jointArticulationBodies.Length} joint angles, got {jointAngles.Length}");
            return;
        }

        StartCoroutine(MoveToJointAnglesRoutine(jointAngles));
    }

    // Coroutine to smoothly move joints to target positions
    private IEnumerator MoveToJointAnglesRoutine(float[] jointAngles)
    {
        for (int i = 0; i < jointArticulationBodies.Length; i++)
        {
            SetJointAngle(i, jointAngles[i]);

            // Wait a bit between joint assignments to ensure stability
            yield return new WaitForSeconds(JOINT_ASSIGNMENT_WAIT);
        }
    }

    // Open the gripper
    public void OpenGripper()
    {
        if (leftGripper != null && rightGripper != null)
        {
            var leftDrive = leftGripper.xDrive;
            var rightDrive = rightGripper.xDrive;

            leftDrive.target = gripperOpenAngle;
            rightDrive.target = gripperOpenAngle;

            leftGripper.xDrive = leftDrive;
            rightGripper.xDrive = rightDrive;

            OnGripperOpen?.Invoke();
        }
    }

    // Close the gripper
    public void CloseGripper()
    {
        if (leftGripper != null && rightGripper != null)
        {
            var leftDrive = leftGripper.xDrive;
            var rightDrive = rightGripper.xDrive;

            leftDrive.target = gripperClosedAngle;
            rightDrive.target = gripperClosedAngle;

            leftGripper.xDrive = leftDrive;
            rightGripper.xDrive = rightDrive;

            OnGripperClose?.Invoke();
        }
    }

    // Example pose positions (you can create your own)
    public void MoveToHomePosition()
    {
        float[] homeAngles = { 0, 0, 0, 0, 0, 0 };
        MoveToJointAngles(homeAngles);
    }

    public void MoveToReadyPosition()
    {
        float[] readyAngles = { 0, -45, 45, 0, 0, 0 };
        MoveToJointAngles(readyAngles);
    }
}