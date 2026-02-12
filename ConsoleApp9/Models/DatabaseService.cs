using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using ConsoleApp9.Models;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public bool RegisterUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        using (var cn = new SqlConnection(_connectionString))
        {
            cn.Open();

            using (var check = cn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(1) FROM Users WHERE Username=@u";
                check.Parameters.AddWithValue("@u", username);
                var exists = (int)check.ExecuteScalar() > 0;
                if (exists) return false;
            }


            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO Users (Username, Password, CreatedAt) VALUES (@u, @p, GETDATE())";
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", password);
                return cmd.ExecuteNonQuery() == 1;
            }
        }
    }

    public User Login(string username, string password)
    {
        using (var cn = new SqlConnection(_connectionString))
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Username, Password, CreatedAt FROM Users WHERE Username=@u AND Password=@p";
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", password);
            cn.Open();

            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;

                return new User
                {
                    Id = r.GetInt32(0),
                    Username = r.GetString(1),
                    Password = r.GetString(2),
                    CreatedAt = r.GetDateTime(3)
                };
            }
        }
    }


    public List<Product> GetAllProducts()
    {
        var list = new List<Product>();

        using (var cn = new SqlConnection(_connectionString))
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT p.Id, p.Name, p.Description, p.Price, p.Stock, c.Name as CategoryName
                                FROM Products p LEFT JOIN Categories c ON p.CategoryId = c.Id";
            cn.Open();

            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Product
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Description = r.IsDBNull(2) ? null : r.GetString(2),
                        Price = r.GetDecimal(3),
                        Stock = r.GetInt32(4),
                        CategoryName = r.IsDBNull(5) ? null : r.GetString(5)
                    });
                }
            }
        }

        return list;
    }

    public bool AddToCart(int userId, int productId, int quantity = 1)
    {
        if (quantity <= 0) return false;

        using (var cn = new SqlConnection(_connectionString))
        {
            cn.Open();

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "SELECT Stock FROM Products WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", productId);
                var o = cmd.ExecuteScalar();
                if (o == null) return false;
                var stock = (int)o;
                if (stock < quantity) return false;
            }

            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"IF EXISTS (SELECT 1 FROM CartItems WHERE UserId=@u AND ProductId=@p)
                                    UPDATE CartItems SET Quantity = Quantity + @q WHERE UserId=@u AND ProductId=@p
                                    ELSE
                                    INSERT INTO CartItems (UserId, ProductId, Quantity) VALUES (@u,@p,@q)";

                cmd.Parameters.AddWithValue("@u", userId);
                cmd.Parameters.AddWithValue("@p", productId);
                cmd.Parameters.AddWithValue("@q", quantity);

                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    public List<CartItem> GetUserCart(int userId)
    {
        var list = new List<CartItem>();

        using (var cn = new SqlConnection(_connectionString))
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT ci.Id, ci.ProductId, p.Name, p.Price, ci.Quantity
                                FROM CartItems ci JOIN Products p ON ci.ProductId = p.Id
                                WHERE ci.UserId = @u";
            cmd.Parameters.AddWithValue("@u", userId);
            cn.Open();

            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new CartItem
                    {
                        Id = r.GetInt32(0),
                        ProductId = r.GetInt32(1),
                        ProductName = r.GetString(2),
                        ProductPrice = r.GetDecimal(3),
                        Quantity = r.GetInt32(4),
                        UserId = userId
                    });
                }
            }
        }

        return list;
    }

    public bool CreateOrder(int userId, int pickupPointId, List<CartItem> cartItems)
    {
        if (cartItems == null || cartItems.Count == 0) return false;

        using (var cn = new SqlConnection(_connectionString))
        {
            cn.Open();
            using (var tran = cn.BeginTransaction())
            {
                try
                {
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.Transaction = tran;

                        decimal total = 0;
                        foreach (var c in cartItems) total += c.ProductPrice * c.Quantity;

                        cmd.CommandText = "INSERT INTO Orders (UserId, PickupPointId, TotalPrice) OUTPUT INSERTED.Id VALUES (@u,@pvz,@total)";
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.Parameters.AddWithValue("@pvz", pickupPointId);
                        cmd.Parameters.AddWithValue("@total", total);
                        var orderId = (int)cmd.ExecuteScalar();
                        cmd.Parameters.Clear();

                        foreach (var c in cartItems)
                        {
                            cmd.CommandText = "SELECT Stock FROM Products WHERE Id=@pid";
                            cmd.Parameters.AddWithValue("@pid", c.ProductId);
                            var stock = (int)cmd.ExecuteScalar();
                            cmd.Parameters.Clear();

                            if (stock < c.Quantity)
                                throw new Exception("Not enough stock for " + c.ProductName);

                            cmd.CommandText = "INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice) VALUES (@o,@p,@q,@pr)";
                            cmd.Parameters.AddWithValue("@o", orderId);
                            cmd.Parameters.AddWithValue("@p", c.ProductId);
                            cmd.Parameters.AddWithValue("@q", c.Quantity);
                            cmd.Parameters.AddWithValue("@pr", c.ProductPrice);
                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();

                            cmd.CommandText = "UPDATE Products SET Stock = Stock - @q WHERE Id=@p";
                            cmd.Parameters.AddWithValue("@q", c.Quantity);
                            cmd.Parameters.AddWithValue("@p", c.ProductId);
                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();

                            cmd.CommandText = "DELETE FROM CartItems WHERE UserId=@u AND ProductId=@p";
                            cmd.Parameters.AddWithValue("@u", userId);
                            cmd.Parameters.AddWithValue("@p", c.ProductId);
                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();
                        }
                    }

                    tran.Commit();
                    return true;
                }
                catch
                {
                    try { tran.Rollback(); } catch { }
                    return false;
                }
            }
        }
    }

    public List<Order> GetUserOrders(int userId, bool sortByNewest = true)
    {
        var orders = new List<Order>();

        using (var cn = new SqlConnection(_connectionString))
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT Id, TotalPrice, CreatedAt, PickupPointId 
                                FROM Orders 
                                WHERE UserId=@u 
                                ORDER BY CreatedAt " + (sortByNewest ? "DESC" : "ASC");
            cmd.Parameters.AddWithValue("@u", userId);
            cn.Open();

            var orderIds = new List<int>();

            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var o = new Order
                    {
                        Id = r.GetInt32(0),
                        TotalPrice = r.GetDecimal(1),
                        CreatedAt = r.GetDateTime(2),
                        PickupPointId = r.GetInt32(3),
                        Items = new List<OrderItem>()
                    };
                    orders.Add(o);
                    orderIds.Add(o.Id);
                }
            }

            if (orderIds.Count == 0) return orders;

            using (var itemsCmd = cn.CreateCommand())
            {
                itemsCmd.CommandText = @"SELECT oi.OrderId, oi.ProductId, oi.Quantity, oi.UnitPrice, p.Name
                                         FROM OrderItems oi
                                         JOIN Products p ON oi.ProductId = p.Id
                                         WHERE oi.OrderId IN (" + string.Join(",", orderIds) + ")";

                using (var ri = itemsCmd.ExecuteReader())
                {
                    var dict = new Dictionary<int, List<OrderItem>>();

                    while (ri.Read())
                    {
                        var oid = ri.GetInt32(0);
                        var item = new OrderItem
                        {
                            OrderId = oid,
                            ProductId = ri.GetInt32(1),
                            Quantity = ri.GetInt32(2),
                            UnitPrice = ri.GetDecimal(3),
                            ProductName = ri.GetString(4)
                        };

                        if (!dict.ContainsKey(oid)) dict[oid] = new List<OrderItem>();
                        dict[oid].Add(item);
                    }

                    foreach (var o in orders)
                        if (dict.ContainsKey(o.Id))
                            o.Items = dict[o.Id];
                }
            }
        }

        return orders;
    }

    public List<PickupPoint> GetAllPickupPoints()
    {
        var list = new List<PickupPoint>();

        using (var cn = new SqlConnection(_connectionString))
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name, Address FROM PickupPoints WHERE IsActive = 1";
            cn.Open();

            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new PickupPoint
                    {
                        Id = r.GetInt32(0),
                        Name = r.GetString(1),
                        Address = r.GetString(2)
                    });
                }
            }
        }

        return list;
    }
}
