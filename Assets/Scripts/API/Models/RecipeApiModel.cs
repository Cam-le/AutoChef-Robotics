using Newtonsoft.Json;
using System;
using System.Linq;

namespace AutoChef.API.Models
{
    [Serializable]
    public class RecipeApiModel
    {
        [JsonProperty("recipeId")]
        public int RecipeId { get; set; }

        [JsonProperty("recipeName")]
        public string RecipeName { get; set; }

        [JsonProperty("ingredients")]
        public string Ingredients { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        // Helper method to parse ingredients string to array
        public string[] GetIngredientsArray()
        {
            if (string.IsNullOrEmpty(Ingredients))
                return new string[0];

            return Ingredients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .ToArray();
        }
    }
}