using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IO;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MenuController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MenuController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // POST: api/menu/create-menu
    [HttpPost("create-menu")]
    public async Task<IActionResult> CreateMenu([FromForm] MenuCreateDto menuDto, [FromForm] IFormFile? menuImage)
    {
        // Get the user_id from the JWT token claims
        var userId = int.Parse(User.FindFirst(ClaimTypes.Name)?.Value);

        string connectionString = _configuration.GetConnectionString("CafeteriaDB");
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            // First, fetch the store_id for the current user
            string getStoreQuery = "SELECT store_id FROM Store WHERE users_id = @UserId";
            using (SqlCommand getStoreCmd = new SqlCommand(getStoreQuery, conn))
            {
                getStoreCmd.Parameters.AddWithValue("@UserId", userId);
                var storeId = await getStoreCmd.ExecuteScalarAsync();

                if (storeId == null)
                {
                    return NotFound(new { message = "Store not found for this user." });
                }

                // Convert the image to byte array if it exists
                byte[]? menuImgBytes = null;
                if (menuImage != null && menuImage.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await menuImage.CopyToAsync(memoryStream);
                        menuImgBytes = memoryStream.ToArray();
                    }
                }

                // Now insert the menu using the store_id
                string insertMenuQuery = @"
                    INSERT INTO Menu (category_id, store_id, menu_name, menu_detail, menu_price, menu_img) 
                    VALUES (@CategoryId, @StoreId, @MenuName, @MenuDetail, @MenuPrice, @MenuImg)";
                using (SqlCommand insertMenuCmd = new SqlCommand(insertMenuQuery, conn))
                {
                    insertMenuCmd.Parameters.AddWithValue("@CategoryId", menuDto.CategoryId ?? (object)DBNull.Value);
                    insertMenuCmd.Parameters.AddWithValue("@StoreId", storeId);
                    insertMenuCmd.Parameters.AddWithValue("@MenuName", menuDto.MenuName);
                    insertMenuCmd.Parameters.AddWithValue("@MenuDetail", menuDto.MenuDetail);
                    insertMenuCmd.Parameters.AddWithValue("@MenuPrice", menuDto.MenuPrice);
                    insertMenuCmd.Parameters.AddWithValue("@MenuImg", (object)menuImgBytes ?? DBNull.Value);

                    await insertMenuCmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Menu created successfully." });
            }
        }
    }

    // PUT: api/menu/update-menu/{menuId}
    [HttpPut("update-menu/{menuId}")]
    public async Task<IActionResult> UpdateMenu(int menuId, [FromForm] MenuCreateDto menuDto, [FromForm] IFormFile? menuImage)
    {
        try
        {
            // Get the user_id from the JWT token claims
            var userId = int.Parse(User.FindFirst(ClaimTypes.Name)?.Value);

            string connectionString = _configuration.GetConnectionString("CafeteriaDB");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // First, check if the menu belongs to the current user's store
                string getMenuQuery = @"
                SELECT menu_id, menu_img 
                FROM Menu
                INNER JOIN Store ON Menu.store_id = Store.store_id
                WHERE menu_id = @MenuId AND Store.users_id = @UserId";

                byte[]? existingImage = null; // To store the existing image

                using (SqlCommand getMenuCmd = new SqlCommand(getMenuQuery, conn))
                {
                    getMenuCmd.Parameters.AddWithValue("@MenuId", menuId);
                    getMenuCmd.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await getMenuCmd.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            // Menu found, get the existing image
                            existingImage = reader.IsDBNull(1) ? null : (byte[])reader[1];
                        }
                        else
                        {
                            return NotFound(new { message = "Menu not found or you do not have permission to update this menu." });
                        }
                    }
                }

                // Convert the new image to byte array if it exists, otherwise use the existing image
                byte[]? menuImgBytes = existingImage;
                if (menuImage != null && menuImage.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await menuImage.CopyToAsync(memoryStream);
                        menuImgBytes = memoryStream.ToArray();
                    }
                }

                // Now update the menu with the new values or retain the existing image
                string updateMenuQuery = @"
                UPDATE Menu
                SET category_id = @CategoryId, menu_name = @MenuName, 
                    menu_detail = @MenuDetail, menu_price = @MenuPrice, 
                    menu_state = @MenuState, menu_img = @MenuImg
                WHERE menu_id = @MenuId";

                using (SqlCommand updateMenuCmd = new SqlCommand(updateMenuQuery, conn))
                {
                    updateMenuCmd.Parameters.AddWithValue("@CategoryId", menuDto.CategoryId ?? (object)DBNull.Value);
                    updateMenuCmd.Parameters.AddWithValue("@MenuName", menuDto.MenuName);
                    updateMenuCmd.Parameters.AddWithValue("@MenuDetail", menuDto.MenuDetail);
                    updateMenuCmd.Parameters.AddWithValue("@MenuPrice", menuDto.MenuPrice);
                    updateMenuCmd.Parameters.AddWithValue("@MenuState", menuDto.MenuState);  // Update menu_state (bit type)
                    updateMenuCmd.Parameters.AddWithValue("@MenuImg", (object)menuImgBytes ?? DBNull.Value); // Retain existing image if no new image
                    updateMenuCmd.Parameters.AddWithValue("@MenuId", menuId);

                    await updateMenuCmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = "Menu updated successfully." });
            }
        }
        catch (Exception ex)
        {
            // Log error and return detailed response
            return StatusCode(500, new { message = "An error occurred while updating the menu.", error = ex.Message });
        }
    }



    // GET: api/menu/get-all
    [HttpGet("get-all")]
    public async Task<IActionResult> GetAllMenus()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.Name)?.Value);
        string connectionString = _configuration.GetConnectionString("CafeteriaDB");
        List<MenuDto> menuList = new List<MenuDto>();

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            // Include menu_id in the SQL query
            string getMenuQuery = @"
            SELECT dbo.Menu.menu_id, dbo.Category.category_name, dbo.Menu.menu_name, 
                   dbo.Menu.menu_detail, dbo.Menu.menu_price, dbo.Menu.menu_state, dbo.Menu.menu_img
            FROM dbo.Menu
            INNER JOIN dbo.Category ON dbo.Menu.category_id = dbo.Category.category_id
            INNER JOIN dbo.Store ON dbo.Menu.store_id = dbo.Store.store_id
            INNER JOIN dbo.Users ON dbo.Store.users_id = dbo.Users.users_id
            WHERE dbo.Users.users_id = @UserId";

            using (SqlCommand cmd = new SqlCommand(getMenuQuery, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        MenuDto menu = new MenuDto
                        {
                            MenuId = reader.GetInt32(0),   // menu_id
                            CategoryName = reader.GetString(1),   // category_name
                            MenuName = reader.GetString(2),   // menu_name
                            MenuDetail = reader.GetString(3), // menu_detail
                            MenuPrice = reader.GetDecimal(4), // menu_price
                            MenuState = reader.GetBoolean(5), // menu_state
                            MenuImgBase64 = reader.IsDBNull(6) ? null : Convert.ToBase64String((byte[])reader[6])  // menu_img as Base64 string
                        };

                        menuList.Add(menu);
                    }
                }
            }
        }

        return Ok(menuList);
    }
    // DELETE: api/menu/delete-menu/{menuId}
    [HttpDelete("delete-menu/{menuId}")]
    public async Task<IActionResult> DeleteMenu(int menuId)
    {
        // Get the user_id from the JWT token claims
        var userId = int.Parse(User.FindFirst(ClaimTypes.Name)?.Value);

        string connectionString = _configuration.GetConnectionString("CafeteriaDB");
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            // First, check if the menu belongs to the current user's store
            string checkMenuQuery = @"
                SELECT menu_id 
                FROM Menu
                INNER JOIN Store ON Menu.store_id = Store.store_id
                WHERE menu_id = @MenuId AND Store.users_id = @UserId";

            using (SqlCommand checkMenuCmd = new SqlCommand(checkMenuQuery, conn))
            {
                checkMenuCmd.Parameters.AddWithValue("@MenuId", menuId);
                checkMenuCmd.Parameters.AddWithValue("@UserId", userId);

                var result = await checkMenuCmd.ExecuteScalarAsync();
                if (result == null)
                {
                    return NotFound(new { message = "Menu not found or you do not have permission to delete this menu." });
                }
            }

            // Now delete the menu
            string deleteMenuQuery = "DELETE FROM Menu WHERE menu_id = @MenuId";

            using (SqlCommand deleteMenuCmd = new SqlCommand(deleteMenuQuery, conn))
            {
                deleteMenuCmd.Parameters.AddWithValue("@MenuId", menuId);
                await deleteMenuCmd.ExecuteNonQueryAsync();
            }

            return Ok(new { message = "Menu deleted successfully." });
        }
    }

}

// MenuCreateDto model class for receiving form data
public class MenuCreateDto
{
    public int? CategoryId { get; set; }
    public string MenuName { get; set; }
    public string MenuDetail { get; set; }
    public decimal MenuPrice { get; set; }
    public bool MenuState { get; set; }  // Add MenuState here
}
// DTO for returning the menu details with category_name
public class MenuDto
{
    public int MenuId { get; set; }  // New property to hold the menu_id
    public string CategoryName { get; set; }
    public string MenuName { get; set; }
    public string MenuDetail { get; set; }
    public decimal MenuPrice { get; set; }
    public bool MenuState { get; set; }
    public string? MenuImgBase64 { get; set; }  // Base64-encoded image string
}

