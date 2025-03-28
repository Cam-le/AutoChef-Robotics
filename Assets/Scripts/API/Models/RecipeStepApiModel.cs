using Newtonsoft.Json;
using System;

namespace AutoChef.API.Models
{
    [Serializable]
    public class RecipeStepApiModel
    {
        [JsonProperty("stepId")]
        public int StepId { get; set; }

        [JsonProperty("recipeId")]
        public int RecipeId { get; set; }

        [JsonProperty("stepDescription")]
        public string StepDescription { get; set; }

        [JsonProperty("stepNumber")]
        public int StepNumber { get; set; }
    }
}