using UnityEngine;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.UI;
using Newtonsoft.Json;

public class AutoChefApiClient : MonoBehaviour
{
    [Header("API Configuration")]
    //[SerializeField]
    //private string apiBaseUrl = "https://your-azure-api.azurewebsites.net/api";

    [SerializeField]
    private int pollIntervalSeconds = 5;

    //[Header("Processing Time Simulation")]
    //[SerializeField]
    //private float prepareIngredientsTime = 3.0f;

    //[SerializeField]
    //private float cookingTime = 5.0f;

    //[SerializeField]
    //private float finishingTime = 2.0f;

    [Header("Robot References")]
    [SerializeField]
    private Transform robotArm;

    [SerializeField]
    private Text statusText;

    // Recipe manager reference
    [Header("Recipe Configuration")]
    [SerializeField]
    private EnhancedRecipeManager recipeManager;

    // Enable random recipe selection
    [SerializeField]
    private bool useRandomRecipe = true;

    private HttpClient httpClient;
    private bool isProcessingOrder = false;
    private CancellationTokenSource cancellationToken;
    private Order currentOrder;

    void Start()
    {
        httpClient = new HttpClient();
        StartOrderPolling();

        // Find recipe manager if not set
        if (recipeManager == null)
        {
            recipeManager = FindObjectOfType<EnhancedRecipeManager>();
            if (recipeManager == null)
            {
                Debug.LogWarning("RecipeManager not found. Recipe processing may not work correctly.");
            }
        }


        if (statusText != null)
        {
            statusText.text = "Robot sẵn sàng - đang chờ đơn hàng...";
        }
    }

    private void StartOrderPolling()
    {
        Debug.Log("Bắt đầu kiểm tra đơn hàng mới...");
        cancellationToken = new CancellationTokenSource();
        _ = PollForOrdersAsync(cancellationToken.Token);
    }

    private async Task PollForOrdersAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!isProcessingOrder)
            {
                try
                {
                    // Gọi API để lấy đơn hàng tiếp theo từ hàng đợi
                    await FetchAndProcessNextOrder();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Lỗi khi kiểm tra đơn hàng: {ex.Message}");
                }
            }

            // Đợi trước khi kiểm tra lại
            await Task.Delay(pollIntervalSeconds * 1000, token);
        }
    }

    private async Task FetchAndProcessNextOrder()
    {
        try
        {
            // Gọi API để lấy đơn hàng tiếp theo
            HttpResponseMessage response = await httpClient.GetAsync($"https://autochefsystem.azurewebsites.net/api/Order/receive-from-queue");

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();

                // Kiểm tra nếu có đơn hàng mới
                if (!string.IsNullOrEmpty(responseContent) && responseContent != "null")
                {
                    // Deserializes chuỗi JSON để lấy message
                    var apiResponse = JsonConvert.DeserializeObject<ApiResponse>(responseContent); // Sử dụng Newtonsoft.Json

                    if (apiResponse != null && !string.IsNullOrEmpty(apiResponse.Message))
                    {
                        // Deserializes trường message để lấy đối tượng Order
                        Order order = JsonConvert.DeserializeObject<Order>(apiResponse.Message); // Sử dụng Newtonsoft.Json

                        if (order != null && order.OrderId > 0)
                        {
                            Debug.Log($"Nhận được đơn hàng mới: {order.OrderId}");

                            // Check if order is cancelled before processing
                            bool isCancelled = await IsOrderCancelled(order.OrderId);

                            if (isCancelled)
                            {
                                Debug.LogWarning($"Đơn hàng {order.OrderId} đã bị cancel.");
                            }
                            else
                            {
                                //Debug.Log($"Order {order.OrderId} is not cancelled. Processing order...");
                                await ProcessOrderAsync(order);
                            }

                            //await ProcessOrderAsync(order);
                        }
                    }
                }
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                // Chỉ log lỗi nếu không phải là NoContent (không có đơn hàng mới)
                Debug.LogWarning($"Không thể lấy đơn hàng: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi gọi API lấy đơn hàng: {ex.Message}");
        }
    }
    private async Task<bool> IsOrderCancelled(int orderId)
    {
        try
        {
            Debug.Log($"Checking if order {orderId} is cancelled...");

            // Call the API to check if order is cancelled
            HttpResponseMessage response = await httpClient.GetAsync(
                $"https://autochefsystem.azurewebsites.net/api/Order/check-cancelled/{orderId}");

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();

                // Parse the response - assuming it returns true/false or a JSON with a boolean property
                bool isCancelled = false;

                // Try to parse as direct boolean
                if (bool.TryParse(responseContent, out bool directResult))
                {
                    isCancelled = directResult;
                }
                else
                {
                    // Try to parse as JSON object with a result property
                    try
                    {
                        var result = JsonConvert.DeserializeObject<CancellationResponse>(responseContent);
                        if (result != null)
                        {
                            isCancelled = result.IsCancelled;
                        }
                    }
                    catch
                    {
                        // If all parsing fails, assume not cancelled
                        //Debug.LogWarning($"Could not parse cancellation response for order {orderId}. Assuming not cancelled.");
                        return false;
                    }
                }

                Debug.Log($"Trạng thái của Order {orderId}: {(isCancelled ? "Bị Cancel" : "Không bị Cancel")}");
                return isCancelled;
            }
            else
            {
                // If API call fails, log the error and assume not cancelled to be safe
                Debug.LogWarning($"Gọi API check trạng thái thất bại cho order {orderId}: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Không check được cancel status của order {orderId}: {ex.Message}");
            return false; // Assume not cancelled in case of error to be safe
        }
    }
    public class CancellationResponse
    {
        [JsonProperty("isCancelled")]
        public bool IsCancelled { get; set; }
    }
    private async Task ProcessOrderAsync(Order order)
    {
        isProcessingOrder = true;
        currentOrder = order;

        try
        {
            // Cập nhật trạng thái "Processing"
            await UpdateOrderStatus(order.OrderId, "Processing");
            UpdateStatusText($"Đang xử lý đơn hàng: {order.OrderId}");

            // Mô phỏng robot chế biến đồ ăn
            await PrepareFood(order);

            // Cập nhật trạng thái "Completed" khi hoàn thành
            await UpdateOrderStatus(order.OrderId, "Completed");
            UpdateStatusText($"Đã hoàn thành đơn hàng: {order.OrderId}");

            Debug.Log($"Hoàn thành đơn hàng {order.OrderId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi xử lý đơn hàng {order.OrderId}: {ex.Message}");
            await UpdateOrderStatus(order.OrderId, "Failed");
            UpdateStatusText($"Lỗi khi xử lý đơn hàng: {order.OrderId}");
        }
        finally
        {
            isProcessingOrder = false;
            currentOrder = null;
        }
    }

    private async Task PrepareFood(Order order)
    {
        // Find the RecipeManager
        if (recipeManager == null)
        {
            recipeManager = FindObjectOfType<EnhancedRecipeManager>();
        }

        if (recipeManager != null)
        {
            // Get the number of available recipes
            int recipeCount = 0;

            // Use reflection to get the recipes array length
            var recipesField = recipeManager.GetType().GetField("recipes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (recipesField != null)
            {
                var recipes = recipesField.GetValue(recipeManager);
                if (recipes is Array recipesArray)
                {
                    recipeCount = recipesArray.Length;
                }
            }

            // Determine which recipe to use
            int recipeId = order.RecipeId;

            // If random recipe is enabled or the received RecipeId is invalid
            if (useRandomRecipe || recipeId < 0 || recipeId >= recipeCount)
            {
                // Select a random recipe
                recipeId = UnityEngine.Random.Range(0, recipeCount);
                Debug.Log($"Using random recipe: {recipeId}");
            }

            // Process the recipe
            recipeManager.ProcessRecipe(recipeId);

            Debug.Log("Đợi robot nấu...");
            // Keep checking status until completed or failed
            string status = "";
            int checkIntervalMs = 500; // Check every 500ms
            int maxWaitTimeMs = 180000; // Maximum wait time of 3 minutes
            int elapsedTimeMs = 0;

            while (elapsedTimeMs < maxWaitTimeMs)
            {
                status = recipeManager.GetStatus();

                // Check if processing is complete
                if (status == "Completed" || status == "Failed")
                {
                    break;
                }

                // Wait before checking again
                await Task.Delay(checkIntervalMs);
                elapsedTimeMs += checkIntervalMs;

                // Log updates periodically (every ~5 seconds)
                if (elapsedTimeMs % 5000 < checkIntervalMs)
                {
                    //Debug.Log($"Still processing recipe... ({elapsedTimeMs / 1000}s elapsed)");
                }
            }

            if (status == "Completed")
            {
                Debug.Log("Recipe completed successfully!");

                // Get the operation log from EnhancedRecipeManager
                string operationLog = recipeManager.GetOperationLog();
                
            }
            else if (status == "Failed")
            {
                Debug.LogError("Recipe processing failed!");
                throw new Exception("Recipe processing failed");
            }
            else
            {
                Debug.LogWarning("Recipe processing timed out!");
                throw new Exception("Recipe processing timed out");
            }
        }
        else
        {
            Debug.LogError("RecipeManager not found. Cannot process food preparation.");
            throw new Exception("RecipeManager not found");
        }

        //if (recipeManager != null)
        //{
        //    // Process the recipe based on RecipeId
        //    recipeManager.ProcessRecipe(order.RecipeId);

        //    // The actual cooking will happen asynchronously
        //    // We can simulate the timing with your existing delays
        //    await Task.Delay((int)(prepareIngredientsTime * 1000));
        //    await Task.Delay((int)(cookingTime * 1000));
        //    await Task.Delay((int)(finishingTime * 1000));
        //}

    }

    private async Task UpdateOrderStatus(int orderId, string status)
    {
        try
        {
            var statusUpdate = new StatusUpdateRequest
            {
                OrderId = orderId,
                Status = status
            };
            var content = new StringContent(
                JsonConvert.SerializeObject(statusUpdate), // Sử dụng Newtonsoft.Json
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PutAsync(
                $"https://autochefsystem.azurewebsites.net/api/Order/update-order-status",
                content
            );

            response.EnsureSuccessStatusCode();
            Debug.Log($"Đã cập nhật trạng thái đơn hàng {orderId}: {status}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi cập nhật trạng thái: {ex.Message}");
            throw;
        }
    }

    private void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    void OnDestroy()
    {
        // Dừng polling task khi GameObject bị hủy
        cancellationToken?.Cancel();
    }

    public class Order
    {
        public int OrderId { get; set; }
        public int RecipeId { get; set; }
        public int RobotId { get; set; }
        public int LocationId { get; set; }
        public string Status { get; set; }
        public DateTime OrderedTime { get; set; }

    }

    public class OrderItem
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public string Instructions { get; set; }
    }

    public class ApiResponse
    {
        public string Message { get; set; }
    }

    public class StatusUpdateRequest
    {
        public int OrderId { get; set; }
        public string Status { get; set; }
    }
}
