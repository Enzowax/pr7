using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace MarketplaceApp
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public bool UserExists(string username)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Users WHERE Username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        public bool CreateUser(string username, string password, string email = "example@email.com")
        {
            if (UserExists(username)) return false;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO Users (Username, PasswordHash, Email, CreatedAt) VALUES (@u, @p, @e, GETDATE())",
                    conn);
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", HashPassword(password));
                cmd.Parameters.AddWithValue("@e", email);

                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public User AuthUser(string username, string password)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM Users WHERE Username=@u", conn);
                cmd.Parameters.AddWithValue("@u", username);

                SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    reader.Close();
                    return null;
                }

                string storedHash = reader["PasswordHash"].ToString();
                if (storedHash != HashPassword(password))
                {
                    reader.Close();
                    return null;
                }

                User user = new User
                {
                    Id = (int)reader["Id"],
                    Username = reader["Username"].ToString(),
                    Email = reader["Email"].ToString(),
                    PasswordHash = storedHash,
                    CreatedAt = (DateTime)reader["CreatedAt"]
                };

                reader.Close();
                return user;
            }
        }

        public List<Product> GetProducts()
        {
            List<Product> list = new List<Product>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM Products", conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new Product
                    {
                        Id = (int)reader["Id"],
                        Name = reader["Name"].ToString(),
                        Price = (decimal)reader["Price"]
                    });
                }
                reader.Close();
            }
            return list;
        }

        public void AddToCart(int userId, int productId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO Cart (UserId, ProductId) VALUES (@u,@p)", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                cmd.Parameters.AddWithValue("@p", productId);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Product> GetCart(int userId)
        {
            List<Product> cart = new List<Product>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    @"SELECT p.Id, p.Name, p.Price 
                      FROM Cart c 
                      JOIN Products p ON p.Id = c.ProductId 
                      WHERE c.UserId=@u", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    cart.Add(new Product
                    {
                        Id = (int)reader["Id"],
                        Name = reader["Name"].ToString(),
                        Price = (decimal)reader["Price"]
                    });
                }
                reader.Close();
            }
            return cart;
        }

        public void RemoveFromCart(int userId, int productId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "DELETE FROM Cart WHERE UserId=@u AND ProductId=@p", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                cmd.Parameters.AddWithValue("@p", productId);
                cmd.ExecuteNonQuery();
            }
        }

        public List<PVZ> GetPVZ()
        {
            List<PVZ> list = new List<PVZ>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT * FROM PickupPoints", conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new PVZ
                    {
                        Id = (int)reader["Id"],
                        Name = reader["Name"].ToString(),
                        Address = reader["Address"].ToString()
                    });
                }
                reader.Close();
            }
            return list;
        }

        public void CreateOrder(int userId, int productId, int pvzId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO Orders (UserId, ProductId, PVZId, Date) VALUES (@u,@p,@z,GETDATE())", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                cmd.Parameters.AddWithValue("@p", productId);
                cmd.Parameters.AddWithValue("@z", pvzId);
                cmd.ExecuteNonQuery();
            }
        }

        public List<Order> GetUserOrders(int userId)
        {
            List<Order> orders = new List<Order>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "SELECT * FROM Orders WHERE UserId=@u ORDER BY Date DESC", conn);
                cmd.Parameters.AddWithValue("@u", userId);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    orders.Add(new Order
                    {
                        Id = (int)reader["Id"],
                        ProductId = (int)reader["ProductId"],
                        PVZId = (int)reader["PVZId"],
                        Date = (DateTime)reader["Date"]
                    });
                }
                reader.Close();
            }
            return orders;
        }
    }

    public class User { public int Id; public string Username; public string Email; public string PasswordHash; public DateTime CreatedAt; }
    public class Product { public int Id; public string Name; public decimal Price; }
    public class PVZ { public int Id; public string Name; public string Address; }
    public class Order { public int Id; public int ProductId; public int PVZId; public DateTime Date; }
}
