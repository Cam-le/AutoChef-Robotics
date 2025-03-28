using Newtonsoft.Json;
using System;

namespace AutoChef.API.Models
{
    [Serializable]
    public class RobotStepApiModel
    {
        [JsonProperty("stepTaskId")]
        public int StepTaskId { get; set; }

        [JsonProperty("stepId")]
        public int StepId { get; set; }

        [JsonProperty("taskDescription")]
        public string TaskDescription { get; set; }

        [JsonProperty("taskOrder")]
        public int TaskOrder { get; set; }

        [JsonProperty("estimatedTime")]
        public string EstimatedTime { get; set; }

        [JsonProperty("repeatCount")]
        public int RepeatCount { get; set; }

        // Helper method to convert EstimatedTime to seconds
        public float GetEstimatedTimeInSeconds()
        {
            if (TimeSpan.TryParse(EstimatedTime, out TimeSpan timeSpan))
                return (float)timeSpan.TotalSeconds;

            return 1.0f; // Default to 1 second if parsing fails
        }
    }
}