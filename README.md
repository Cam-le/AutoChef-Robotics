# AutoChef: Automated Food Preparation System

## Overview

AutoChef is a Unity-based simulation system for automated food preparation that demonstrates the application of robotic arms in Vietnamese cuisine. The system automates the preparation of traditional Vietnamese dishes like **phở**, **hủ tiếu**, **bánh canh**, and **nui** through a web/app ordering interface and robot arm simulation.

## Features

- **Order Management System**: Web/app interface for customers to place food orders with customization options
- **Robot Arm Simulation**: Unity-based simulation of a 6-DOF robotic arm for food preparation
- **Recipe System**: Configurable recipes with step-by-step instructions for robot execution
- **API Integration**: Complete backend integration for orders, recipes, and robot operations
- **Real-time Status Updates**: Live monitoring of order status and robot operations
- **Admin Dashboard**: Comprehensive management interface for restaurant operators

## System Architecture

The AutoChef system consists of three main components:

1. **Customer Interface**: Web/mobile application for ordering
2. **Restaurant Management**: Dashboard for monitoring orders and robot performance
3. **Robot Simulation**: Unity-based simulation of robotic food preparation

## Key Components

### Robot Arm Controller

The `RobotArmController` manages the 6-DOF robot arm simulation, handling:
- Joint angle positioning
- Gripper operations
- Movement sequencing

```csharp
// Example of robot arm movement
robotController.MoveToJointAngles(new float[] { 0, -45, 45, 0, 0, 0 });
```

### Recipe Manager

The `AutoChefRecipeManager` coordinates recipe execution:
- Processes ingredient operations
- Manages cooking sequences
- Tracks operation logs

### API Client

The `AutoChefApiClient` handles communication with the backend:
- Fetches recipes and orders
- Updates order status
- Reports operation logs

## Usage

### Simulating Food Preparation

1. Start the Unity simulation
2. Orders will automatically populate from the API
3. Watch as the robot arm assembles dishes based on recipes
4. Monitor status through the UI

### Testing Without API

Use the demo buttons to manually trigger recipes:

```csharp
// Manually process a recipe
recipeManager.ProcessRecipe(recipeIndex, -1); // -1 indicates a test order
```

## Development

### Adding New Recipes

1. Create a new entry in `RecipeApiModel`
2. Define the ingredient list
3. Create corresponding step instructions in `RecipeStepApiModel`
4. Associate robot operations with `RobotStepApiModel`

### Teaching Robot Positions

The project includes utilities for teaching robot positions:

1. Use `IngredientPositionHelper` to map new ingredient positions
2. Manually adjust and save joint angles
3. Export position code for the `RobotMovementSequencer`

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- This project was developed to demonstrate robotics applications in the food service industry
- Unity Robotics Hub for the robot simulation framework