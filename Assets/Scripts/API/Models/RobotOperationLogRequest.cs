using Newtonsoft.Json;

using System;

/// <summary>
/// Model for sending robot operation logs to the database
/// </summary>
[System.Serializable]
public class RobotOperationLogRequest
{
    [JsonProperty("orderId")]
    public int OrderId { get; set; }

    [JsonProperty("robotId")]
    public int RobotId { get; set; }

    [JsonProperty("startTime")]
    public DateTime StartTime { get; set; }

    [JsonProperty("endTime")]
    public DateTime EndTime { get; set; }

    [JsonProperty("completionStatus")]
    public string CompletionStatus { get; set; }

    [JsonProperty("operationLog")]
    public string OperationLog { get; set; }
}