using CafeteriaAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;  // นำเข้าการใช้งาน MemoryCache
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
public class StoreController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;

    public StoreController(IConfiguration configuration, IMemoryCache memoryCache)
    {
        _configuration = configuration;
        _memoryCache = memoryCache;
    }

    // API สำหรับสร้างร้านค้าพร้อมการ Authorization และเก็บ store_id ไว้ใน MemoryCache
    [Authorize]
    [HttpPost("create")]
    public async Task<IActionResult> CreateStore([FromBody] StoreDto storeDto)
    {
        if (string.IsNullOrEmpty(storeDto.StoreName))
        {
            return BadRequest(new { message = "Store name is required." });
        }

        // ดึง userId จาก JWT Token
        var userIdClaim = User.FindFirst(ClaimTypes.Name);
        if (userIdClaim == null)
        {
            return Unauthorized(new { message = "User not authorized." });
        }

        int userId = int.Parse(userIdClaim.Value);

        // SQL query สำหรับ insert ร้านค้าและดึง store_id กลับมา
        string connectionString = _configuration.GetConnectionString("CafeteriaDB");
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            // SQL command สำหรับการ insert ร้านค้าใหม่ และดึง store_id ที่สร้างขึ้นมาใหม่
            string query = @"
                INSERT INTO Store (users_id, store_name, store_phone, store_num, store_detail, store_create, store_type)
                OUTPUT INSERTED.store_id  -- Output the store_id of the newly created store
                VALUES (@UserId, @StoreName, @StorePhone, @StoreNum, @StoreDetail, @StoreCreate, @StoreType)";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                // ตั้งค่าพารามิเตอร์สำหรับการ insert
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@StoreName", storeDto.StoreName);
                cmd.Parameters.AddWithValue("@StorePhone", string.IsNullOrEmpty(storeDto.StorePhone) ? DBNull.Value : storeDto.StorePhone);
                cmd.Parameters.AddWithValue("@StoreNum", string.IsNullOrEmpty(storeDto.StoreNum) ? DBNull.Value : storeDto.StoreNum);
                cmd.Parameters.AddWithValue("@StoreDetail", string.IsNullOrEmpty(storeDto.StoreDetail) ? DBNull.Value : storeDto.StoreDetail);
                cmd.Parameters.AddWithValue("@StoreCreate", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@StoreType", string.IsNullOrEmpty(storeDto.StoreType) ? DBNull.Value : storeDto.StoreType);

                // Execute query และรับค่า store_id ที่เพิ่งสร้าง
                var storeId = (int)await cmd.ExecuteScalarAsync();

                if (storeId > 0)
                {
                    // เก็บค่า store_id ใน MemoryCache โดยกำหนดคีย์เฉพาะสำหรับ userId
                    string cacheKey = $"Store_{userId}";
                    _memoryCache.Set(cacheKey, storeId);

                    // ไม่ต้องส่ง store_id กลับไปยัง client
                    return Ok(new { message = "Store created successfully." });
                }
                else
                {
                    // กรณีเกิดปัญหาในการสร้างร้านค้า
                    return StatusCode(500, new { message = "Error creating store." });
                }
            }
        }
    }
}
