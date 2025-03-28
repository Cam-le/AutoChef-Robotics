using Newtonsoft.Json;
using System;

namespace AutoChef.API.Models
{
    [Serializable]
    public class OrderApiModel
    {
        [JsonProperty("orderId")]
        public int OrderId { get; set; }

        [JsonProperty("recipeId")]
        public int RecipeId { get; set; }

        [JsonProperty("robotId")]
        public int RobotId { get; set; }

        [JsonProperty("locationId")]
        public int LocationId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("orderedTime")]
        public DateTime OrderedTime { get; set; }
    }
}