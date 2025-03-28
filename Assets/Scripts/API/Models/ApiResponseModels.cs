using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AutoChef.API.Models
{
    [Serializable]
    public class RecipeListResponse
    {
        [JsonProperty("recipes")]
        public List<RecipeApiModel> Recipes { get; set; } = new List<RecipeApiModel>();

        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
    }

    [Serializable]
    public class RobotStepTaskListResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public RobotStepTaskResponseData Data { get; set; }
    }

    [Serializable]
    public class RobotStepTaskResponseData
    {
        [JsonProperty("tasks")]
        public List<RobotStepApiModel> Tasks { get; set; } = new List<RobotStepApiModel>();

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
    }

    [Serializable]
    public class GenericApiResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    [Serializable]
    public class OrderCancellationResponse
    {
        [JsonProperty("isCancelled")]
        public bool IsCancelled { get; set; }
    }
}