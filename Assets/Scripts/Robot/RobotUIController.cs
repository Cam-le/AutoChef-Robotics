using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RobotUIController : MonoBehaviour
{
    [Header("Robot Reference")]
    public RobotArmController robotController;

    [Header("Joint Sliders")]
    public Slider[] jointSliders;

    [Header("Buttons")]
    public Button homeButton;
    public Button readyButton;
    public Button openGripperButton;
    public Button closeGripperButton;

    // Text displays for joint angles
    [Header("Joint Angle Displays")]
    public Text[] jointAngleDisplays;

    void Start()
    {
        if (robotController == null)
        {
            Debug.LogError("Robot Controller not assigned to UI Controller!");
            return;
        }

        // Setup joint sliders
        SetupJointSliders();

        // Setup buttons
        SetupButtons();
    }

    void Update()
    {
        // Update joint angle displays
        UpdateJointAngleDisplays();
    }

    private void SetupJointSliders()
    {
        if (jointSliders.Length == 0)
        {
            Debug.LogWarning("No joint sliders assigned.");
            return;
        }

        // Ensure we don't have more sliders than joints
        int numJoints = Mathf.Min(jointSliders.Length, robotController.jointArticulationBodies.Length);

        for (int i = 0; i < numJoints; i++)
        {
            if (jointSliders[i] != null)
            {
                // Store the joint index for the lambda
                int jointIndex = i;

                // Add listener that calls SetJointAngle with the appropriate index
                jointSliders[i].onValueChanged.AddListener((float value) => {
                    robotController.SetJointAngle(jointIndex, value);
                });
            }
        }
    }

    private void SetupButtons()
    {
        // Home position button
        if (homeButton != null)
        {
            homeButton.onClick.AddListener(() => {
                robotController.MoveToHomePosition();
                UpdateSliderPositions();
            });
        }

        // Ready position button
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(() => {
                robotController.MoveToReadyPosition();
                UpdateSliderPositions();
            });
        }

        // Gripper control buttons
        if (openGripperButton != null)
        {
            openGripperButton.onClick.AddListener(() => {
                robotController.OpenGripper();
            });
        }

        if (closeGripperButton != null)
        {
            closeGripperButton.onClick.AddListener(() => {
                robotController.CloseGripper();
            });
        }
    }

    // Update slider positions to match robot's actual joint angles
    private void UpdateSliderPositions()
    {
        if (jointSliders.Length == 0)
        {
            return;
        }

        // Make sure we don't exceed the number of available joints
        int numJoints = Mathf.Min(jointSliders.Length, robotController.jointArticulationBodies.Length);

        for (int i = 0; i < numJoints; i++)
        {
            if (jointSliders[i] != null && robotController.jointArticulationBodies[i] != null)
            {
                // Update slider without triggering the value changed event
                float currentAngle = robotController.jointArticulationBodies[i].xDrive.target;
                jointSliders[i].SetValueWithoutNotify(currentAngle);
            }
        }
    }

    // Update text displays showing current joint angles
    private void UpdateJointAngleDisplays()
    {
        if (jointAngleDisplays.Length == 0)
        {
            return;
        }

        int numDisplays = Mathf.Min(jointAngleDisplays.Length, robotController.jointArticulationBodies.Length);

        for (int i = 0; i < numDisplays; i++)
        {
            if (jointAngleDisplays[i] != null && robotController.jointArticulationBodies[i] != null)
            {
                float currentAngle = robotController.jointArticulationBodies[i].xDrive.target;
                jointAngleDisplays[i].text = $"Joint {i + 1}: {currentAngle:F1}°";
            }
        }
    }
    // Add these to RobotUIController
    public void OnJoint1SliderChanged(float value)
    {
        robotController.SetJointAngle(0, value);
    }

    public void OnJoint2SliderChanged(float value)
    {
        robotController.SetJointAngle(1, value);
    }

    public void OnJoint3SliderChanged(float value)
    {
        robotController.SetJointAngle(2, value);
    }

    public void OnJoint4SliderChanged(float value)
    {
        robotController.SetJointAngle(3, value);
    }

    public void OnJoint5SliderChanged(float value)
    {
        robotController.SetJointAngle(4, value);
    }

    public void OnJoint6SliderChanged(float value)
    {
        robotController.SetJointAngle(5, value);
    }
}