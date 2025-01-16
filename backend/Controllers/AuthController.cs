using CafeteriaAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using CafeteriaAPI.Data;
using Microsoft.Data.SqlClient;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // API สมัครสมาชิก (Sign-up)
    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] UserSignUpDto userDto)
    {
        if (string.IsNullOrEmpty(userDto.Username) || string.IsNullOrEmpty(userDto.Email) || string.IsNullOrEmpty(userDto.Password) || string.IsNullOrEmpty(userDto.UserType))
        {
            return BadRequest(new { message = "กรุณาใส่ข้อมูลให้ครบถ้วน" });
        }

        string connectionString = _configuration.GetConnectionString("CafeteriaDB");
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            string checkUserQuery = "SELECT COUNT(1) FROM Users WHERE username = @Username";
            using (SqlCommand checkCmd = new SqlCommand(checkUserQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@Username", userDto.Username);
                int userExists = (int)await checkCmd.ExecuteScalarAsync();

                if (userExists > 0)
                {
                    return BadRequest(new { message = "มีชื่อผู้ใช้นี้แล้ว" });
                }
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(userDto.Password);

            string insertUserQuery = @"
                INSERT INTO Users (username, email, passwordhash, user_type, fullname, phone)
                VALUES (@Username, @Email, @PasswordHash, @UserType, @FullName, @Phone)";

            using (SqlCommand cmd = new SqlCommand(insertUserQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Username", userDto.Username);
                cmd.Parameters.AddWithValue("@Email", userDto.Email);
                cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                cmd.Parameters.AddWithValue("@UserType", userDto.UserType);
                cmd.Parameters.AddWithValue("@FullName", string.IsNullOrEmpty(userDto.FullName) ? DBNull.Value : userDto.FullName);
                cmd.Parameters.AddWithValue("@Phone", string.IsNullOrEmpty(userDto.Phone) ? DBNull.Value : userDto.Phone);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    return Ok(new { message = "สมัครสมาชิกเรียบร้อย" });
                }
                else
                {
                    return StatusCode(500, new { message = "เกิดปัญหาระหว่างสมัครสมาชิก" });
                }
            }
        }
    }

    // API เข้าสู่ระบบ (Sign-in)
    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] UserSignInDto userDto)
    {
        string connectionString = _configuration.GetConnectionString("CafeteriaDB");
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            string getUserQuery = "SELECT users_id, username, passwordhash, user_type FROM Users WHERE username = @Username";
            using (SqlCommand getUserCmd = new SqlCommand(getUserQuery, conn))
            {
                getUserCmd.Parameters.AddWithValue("@Username", userDto.Username);

                using (SqlDataReader reader = await getUserCmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        int userId = reader.GetInt32(0);
                        string username = reader.GetString(1);
                        string passwordHash = reader.GetString(2);
                        string userType = reader.GetString(3);

                        if (!BCrypt.Net.BCrypt.Verify(userDto.Password, passwordHash))
                        {
                            return Unauthorized(new { message = "ชื่อผู้ใช้หรือรหัสไม่ถูกต้อง" });
                        }

                        // Create JWT Token
                        var tokenHandler = new JwtSecurityTokenHandler();
                        var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]);
                        var tokenDescriptor = new SecurityTokenDescriptor
                        {
                            Subject = new ClaimsIdentity(new Claim[]
                            {
                                new Claim(ClaimTypes.Name, userId.ToString()),
                                new Claim(ClaimTypes.Role, userType)
                            }),
                            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpiryMinutes"])),
                            Issuer = _configuration["JwtSettings:Issuer"],
                            Audience = _configuration["JwtSettings:Audience"],
                            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                        };

                        var token = tokenHandler.CreateToken(tokenDescriptor);
                        var tokenString = tokenHandler.WriteToken(token);

                        return Ok(new
                        {
                            Token = tokenString,
                            UserType = userType,
                            Username = username
                        });
                    }
                    else
                    {
                        return Unauthorized(new { message = "ชื่อผู้ใช้หรือรหัสไม่ถูกต้อง" });
                    }
                }
            }
        }
    }
}
