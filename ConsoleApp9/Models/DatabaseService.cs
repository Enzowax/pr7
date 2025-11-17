using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using ConsoleApp9.Models;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private string HashPassword(string password)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    public bool RegisterUser(string username, string password, string email)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            using (var checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM Users WHERE Username=@u OR Email=@e", conn))
            {
                checkCmd.Parameters.AddWithValue("@u", username);
                checkCmd.Parameters.AddWithValue("@e", email);

                if ((int)checkCmd.ExecuteScalar() > 0)
                    return false;
            }

            using (var cmd = new SqlCommand(
                "INSERT INTO Users (Username, PasswordHash, Email, CreatedAt) VALUES (@u, @p, @e, GETDATE())",
                conn))
            {
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", HashPassword(password));
                cmd.Parameters.AddWithValue("@e", email);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    public User Login(string username, string password)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            using (var cmd = new SqlCommand(
                "SELECT * FROM Users WHERE Username=@u", conn))
            {
                cmd.Parameters.AddWithValue("@u", username);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    string storedHash = r["PasswordHash"].ToString();
                    string inputHash = HashPassword(password);

                    if (storedHash != inputHash) return null;

                    return new User
                    {
                        Id = (int)r["Id"],
                        Username = r["Username"].ToString(),
                        Email = r["Email"].ToString(),
                        PasswordHash = storedHash,
                        CreatedAt = (DateTime)r["CreatedAt"]
                    };
                }
            }
        }
    }

    public List<Product> GetAllProducts()
    {
        var list = new List<Product>();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            string sql = @"
                SELECT p.*, c.Name AS CategoryName
                FROM Products p
                JOIN Categories c ON p.CategoryId = c.Id";

            using (var cmd = new SqlCommand(sql, conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Product
                    {
                        Id = (int)r["Id"],
                        Name = r["Name"].ToString(),
                        Description = r["Description"].ToString(),
                        Price = (decimal)r["Price"],
                        CategoryId = (int)r["CategoryId"],
                        StockQuantity = (int)r["StockQuantity"],
                        CategoryName = r["CategoryName"].ToString()
                    });
                }
            }
        }

        return list;
    }

    public bool AddToCart(int userId, int productId, int quantity = 1)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            int stock = 0;

            using (var checkCmd = new SqlCommand(
                "SELECT StockQuantity FROM Products WHERE Id=@id", conn))
            {
                checkCmd.Parameters.AddWithValue("@id", productId);
                var result = checkCmd.ExecuteScalar();
                if (result == null) return false;
                stock = (int)result;
            }

            if (stock < quantity) return false;

            int cartId = 0;
            int oldQty = 0;

            using (var existCmd = new SqlCommand(
                "SELECT Id, Quantity FROM Cart WHERE UserId=@u AND ProductId=@p", conn))
            {
                existCmd.Parameters.AddWithValue("@u", userId);
                existCmd.Parameters.AddWithValue("@p", productId);

                using (var r = existCmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        cartId = (int)r["Id"];
                        oldQty = (int)r["Quantity"];
                    }
                }
            }

            if (cartId > 0)
            {
                using (var updateCmd = new SqlCommand(
                    "UPDATE Cart SET Quantity=@q WHERE Id=@id", conn))
                {
                    updateCmd.Parameters.AddWithValue("@q", oldQty + quantity);
                    updateCmd.Parameters.AddWithValue("@id", cartId);
                    return updateCmd.ExecuteNonQuery() > 0;
                }
            }
            else
            {
                using (var insertCmd = new SqlCommand(
                    "INSERT INTO Cart (UserId, ProductId, Quantity) VALUES (@u, @p, @q)", conn))
                {
                    insertCmd.Parameters.AddWithValue("@u", userId);
                    insertCmd.Parameters.AddWithValue("@p", productId);
                    insertCmd.Parameters.AddWithValue("@q", quantity);
                    return insertCmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }

    public List<CartItem> GetUserCart(int userId)
    {
        var list = new List<CartItem>();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            string sql = @"
                SELECT c.Id, c.ProductId, c.Quantity,
                       p.Name, p.Price
                FROM Cart c
                JOIN Products p ON p.Id = c.ProductId
                WHERE c.UserId = @u";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@u", userId);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new CartItem
                        {
                            Id = (int)r["Id"],
                            ProductId = (int)r["ProductId"],
                            Quantity = (int)r["Quantity"],
                            ProductName = r["Name"].ToString(),
                            ProductPrice = (decimal)r["Price"],
                            UserId = userId
                        });
                    }
                }
            }
        }

        return list;
    }

    public bool CreateOrder(int userId, int pickupPointId, List<CartItem> cartItems)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();
            using (var tran = conn.BeginTransaction())
            {
                try
                {
                    int orderId;

                    using (var cmd = new SqlCommand(
                        "INSERT INTO Orders (UserId, PickupPointId, TotalAmount, OrderDate, Status) " +
                        "VALUES (@u, @p, @t, GETDATE(), 'CREATED'); SELECT SCOPE_IDENTITY();",
                        conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.Parameters.AddWithValue("@p", pickupPointId);
                        cmd.Parameters.AddWithValue("@t", cartItems.Sum(c => c.TotalPrice));

                        orderId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    foreach (var item in cartItems)
                    {
                        using (var itemCmd = new SqlCommand(
                            "INSERT INTO OrderItems (OrderId, ProductId, Quantity, Price) VALUES (@o, @pr, @q, @price)",
                            conn, tran))
                        {
                            itemCmd.Parameters.AddWithValue("@o", orderId);
                            itemCmd.Parameters.AddWithValue("@pr", item.ProductId);
                            itemCmd.Parameters.AddWithValue("@q", item.Quantity);
                            itemCmd.Parameters.AddWithValue("@price", item.ProductPrice);
                            itemCmd.ExecuteNonQuery();
                        }
                    }

                    using (var clearCmd = new SqlCommand(
                        "DELETE FROM Cart WHERE UserId=@u", conn, tran))
                    {
                        clearCmd.Parameters.AddWithValue("@u", userId);
                        clearCmd.ExecuteNonQuery();
                    }

                    tran.Commit();
                    return true;
                }
                catch
                {
                    tran.Rollback();
                    return false;
                }
            }
        }
    }

    public List<PVZ> GetAllPVZ()
    {
        var list = new List<PVZ>();

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            using (var cmd = new SqlCommand("SELECT * FROM PickupPoints", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new PVZ
                    {
                        Id = (int)r["Id"],
                        Name = r["Name"].ToString(),
                        Address = r["Address"].ToString()
                    });
                }
            }
        }

        return list;
    }
}
