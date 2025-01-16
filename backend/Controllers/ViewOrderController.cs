using CafeteriaAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CafeteriaAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViewOrderController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ViewOrderController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // View menu that was created
        [HttpGet]
        public ActionResult<List<OrderItems>> GetOrderItems()
        {
            List<OrderItems> orderitems = new List<OrderItems>();
            Response response = new Response();

            try
            {
                // Ensure proper connection disposal with "using" statement
                using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("CafeteriaDB")))
                {
                    string query = @"
                        SELECT dbo.Queues.queue_no,
                        dbo.Customer.customer_name,
                        dbo.OrderItem.orderitem_name,
                        dbo.OrderItem.orderitem_quantity,
                        dbo.Customer.customer_phone
                        FROM  dbo.OrderItem INNER JOIN
                        dbo.Orders ON dbo.OrderItem.order_id = dbo.Orders.order_id INNER JOIN
                        dbo.Menu ON dbo.Orders.menu_id = dbo.Menu.menu_id INNER JOIN
                        dbo.Queues ON dbo.OrderItem.orderitem_id = dbo.Queues.orderitem_id INNER JOIN
                        dbo.Customer ON dbo.Orders.customer_id = dbo.Customer.customer_id";

                    SqlDataAdapter orderItems = new SqlDataAdapter(query, connection);
                    DataTable dataTable = new DataTable();
                    orderItems.Fill(dataTable);

                    if (dataTable.Rows.Count > 0)
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            OrderItems orderitem = new OrderItems
                            {
                                queue_no = Convert.ToInt32(row["queue_no"]),
                                customer_name = row["customer_name"].ToString(),
                                orderitem_name = row["orderitem_name"].ToString(),
                                orderitem_quantity = Convert.ToInt32(row["orderitem_quantity"]),
                                customer_phone = row["customer_phone"].ToString()
                            };
                            orderitems.Add(orderitem);
                        }

                        return Ok(new { StatusCode = 200, Data = orderitems, Message = "Data retrieved successfully" });
                    }
                    else
                    {
                        return NotFound(new { StatusCode = 404, Message = "No data found" });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { StatusCode = 500, Message = "An error occurred: " + ex.Message });
            }
        }
    }
}
