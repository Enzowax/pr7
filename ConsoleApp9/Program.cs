using System;
using System.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        UserService userService = new UserService();

        while (true)
        {
            Console.WriteLine("\nДобро пожаловать в GMWOG Marketplace");
            Console.WriteLine("1. Регистрация");
            Console.WriteLine("2. Вход");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите действие: ");

            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    userService.Register();
                    break;

                case "2":
                    int userId;
                    if (userService.Login(out userId))
                        Console.WriteLine($"Вы вошли под ID: {userId}");
                    break;

                case "0":
                    Console.WriteLine("Выход из программы...");
                    return;

                default:
                    Console.WriteLine("Неверный выбор!");
                    break;
            }
        }
    }
}

public class UserService
{
    private static class DbConfig
    {
        private const string connectionString = @"Data Source=localhost;Initial Catalog=GMWOG;Integrated Security=True;";

        public static SqlConnection GetConnection()
        {
            return new SqlConnection(connectionString);
        }
    }


    public void Register()
    {
        Console.Write("Введите логин: ");
        string username = Console.ReadLine();

        Console.Write("Введите пароль: ");
        string password1 = Console.ReadLine();

        Console.Write("Подтвердите пароль: ");
        string password2 = Console.ReadLine();

        if (password1 != password2)
        {
            Console.WriteLine("Пароли не совпадают!");
            return;
        }

        using (var conn = DbConfig.GetConnection())
        {
            conn.Open();

            string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @u";
            using (var checkCmd = new SqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@u", username);
                int count = (int)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    Console.WriteLine("Пользователь с таким логином уже существует!");
                    return;
                }
            }

            string insertQuery = "INSERT INTO Users (Username, Password) VALUES (@u, @p)";
            using (var insertCmd = new SqlCommand(insertQuery, conn))
            {
                insertCmd.Parameters.AddWithValue("@u", username);
                insertCmd.Parameters.AddWithValue("@p", password1);
                insertCmd.ExecuteNonQuery();
            }

            Console.WriteLine("Регистрация прошла успешно!");
        }
    }

    public bool Login(out int userId)
    {
        userId = -1;

        Console.Write("Введите логин: ");
        string username = Console.ReadLine();

        Console.Write("Введите пароль: ");
        string password = Console.ReadLine();

        using (var conn = DbConfig.GetConnection())
        {
            conn.Open();
            string query = "SELECT Id FROM Users WHERE Username=@u AND Password=@p";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", password);

                var result = cmd.ExecuteScalar();

                if (result != null)
                {
                    userId = Convert.ToInt32(result);
                    Console.WriteLine("Вход выполнен!");
                    return true;
                }
                else
                {
                    Console.WriteLine("Неверный логин или пароль!");
                    return false;
                }
            }
        }
    }
}
