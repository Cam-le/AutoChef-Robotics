﻿using UnityEngine;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.UI;
using Newtonsoft.Json;

public class RobotArmTcpServer : MonoBehaviour
{
    [Header("API Configuration")]
    //[SerializeField]
    //private string apiBaseUrl = "https://your-azure-api.azurewebsites.net/api";

    [SerializeField]
    private int pollIntervalSeconds = 5;

    [Header("Processing Time Simulation")]
    [SerializeField]
    private float prepareIngredientsTime = 3.0f;

    [SerializeField]
    private float cookingTime = 5.0f;

    [SerializeField]
    private float finishingTime = 2.0f;

    [Header("Robot References")]
    [SerializeField]
    private Transform robotArm;

    [SerializeField]
    private Text statusText;

    private HttpClient httpClient;
    private bool isProcessingOrder = false;
    private CancellationTokenSource cancellationToken;
    private Order currentOrder;

    void Start()
    {
        httpClient = new HttpClient();
        StartOrderPolling();

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
                            await ProcessOrderAsync(order);
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
        // Giả lập robot làm đồ ăn dựa trên các món trong đơn hàng
        // Bổ sung Items vào Order
        await Task.Delay((int)(prepareIngredientsTime * 1000)); // Thời gian chuẩn bị
        await Task.Delay((int)(cookingTime * 1000)); // Thời gian nấu
        await Task.Delay((int)(finishingTime * 1000)); // Thời gian hoàn thiện
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
