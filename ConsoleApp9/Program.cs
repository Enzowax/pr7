using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace AutoServiceSimple
{
    class Program
    {
        private const string connectionString = @"Data Source=localhost;Initial Catalog=AutoServiceDB;Integrated Security=True;";

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var db = new DbHelper(connectionString);
            decimal balance = db.GetBalance();

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Автосервис");
                Console.WriteLine("1 — Работа (принять клиента / отремонтировать)");
                Console.WriteLine("2 — Склад (посмотреть запасы)");
                Console.WriteLine("3 — Покупка (купить деталь)");
                Console.WriteLine("0 — Выход");
                Console.Write("Выберите действие: ");
                var choice = Console.ReadLine();

                if (choice == "1")
                {
                    var car = db.GetRandomCar();
                    if (car == null)
                    {
                        Console.WriteLine("Нет клиентов в базе и/или нет деталей в справочнике.");
                        continue;
                    }

                    Console.WriteLine($"\nКлиент привёз: {car.Model} ({car.LicensePlate})");
                    Console.WriteLine($"Сломанная деталь: {car.BrokenPartName} (PartId={car.BrokenPartId})");
                    Console.WriteLine($"Цена ремонта (что клиент готов заплатить): {car.RepairPrice:C}");
                    Console.Write("Ремонтировать? (да/нет): ");
                    var ans = Console.ReadLine();

                    if (IsYes(ans))
                    {
                        var inv = db.GetInventoryItemByPartId(car.BrokenPartId);
                        if (inv != null && inv.Quantity > 0)
                        {
                            db.DecreaseInventory(car.BrokenPartId, 1);
                            db.AddTransaction(car.RepairPrice, "RepairIncome", $"Ремонт {car.BrokenPartName}");
                            balance += car.RepairPrice;
                            db.InsertGameState(balance);
                            Console.WriteLine($"Ремонт выполнен. Баланс: {balance:C}");
                        }
                        else
                        {
                            Console.WriteLine("Нужной детали нет на складе.");
                            Console.WriteLine("1 - Купить сейчас и установить (спишется сразу)");
                            Console.WriteLine("2 - Отказать (штраф)");
                            Console.Write("Выберите: ");
                            var c = Console.ReadLine();

                            if (c == "1")
                            {
                                var part = db.GetPartById(car.BrokenPartId);
                                if (part == null)
                                {
                                    Console.WriteLine("Деталь не найдена в каталоге. Отказ.");
                                    ApplyPenalty(db, ref balance, 300m);
                                }
                                else
                                {
                                    if (balance < part.PurchasePrice)
                                    {
                                        Console.WriteLine($"Недостаточно денег для покупки (нужно {part.PurchasePrice:C}, у вас {balance:C}). Отказ.");
                                        ApplyPenalty(db, ref balance, 300m);
                                    }
                                    else
                                    {
                                        balance -= part.PurchasePrice;
                                        db.InsertGameState(balance);
                                        db.AddTransaction(-part.PurchasePrice, "PurchaseExpense", $"Покупка {part.Name} x1");
                                        db.IncreaseInventory(part.PartId, 1);

                                        db.DecreaseInventory(part.PartId, 1);
                                        db.AddTransaction(car.RepairPrice, "RepairIncome", $"Ремонт {car.BrokenPartName}");
                                        balance += car.RepairPrice;
                                        db.InsertGameState(balance);

                                        Console.WriteLine($"Куплена деталь и выполнен ремонт. Баланс: {balance:C}");
                                    }
                                }
                            }
                            else if (c == "2")
                            {
                                ApplyPenalty(db, ref balance, 300m);
                            }
                            else
                            {
                                Console.WriteLine("Неверный выбор — считается отказом.");
                                ApplyPenalty(db, ref balance, 300m);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Вы отказались ремонтировать (клиент уехал). Штраф 300 руб.");
                        ApplyPenalty(db, ref balance, 300m);
                    }
                }
                else if (choice == "2")
                {
                    Console.WriteLine("\nСклад");
                    var inv = db.GetInventory();
                    if (inv.Count == 0) Console.WriteLine("Склад пуст.");
                    foreach (var it in inv)
                    {
                        Console.WriteLine($"PartId={it.PartId} | {it.PartName} — {it.Quantity} шт.");
                    }
                }
                else if (choice == "3")
                {
                    Console.WriteLine("\nКаталог деталей");
                    var parts = db.GetAllParts();
                    foreach (var p in parts)
                    {
                        Console.WriteLine($"{p.PartId}. {p.Name} — закуп.: {p.PurchasePrice:C}, продаж.: {p.SalePrice:C}");
                    }
                    Console.Write("Введите ID детали для покупки: ");
                    if (!int.TryParse(Console.ReadLine(), out int pid))
                    {
                        Console.WriteLine("Неверный ID.");
                        continue;
                    }
                    Console.Write("Введите количество: ");
                    if (!int.TryParse(Console.ReadLine(), out int qty) || qty <= 0)
                    {
                        Console.WriteLine("Неверное количество.");
                        continue;
                    }

                    var selected = db.GetPartById(pid);
                    if (selected == null)
                    {
                        Console.WriteLine("Деталь не найдена.");
                        continue;
                    }

                    decimal totalCost = selected.PurchasePrice * qty;
                    if (balance < totalCost)
                    {
                        Console.WriteLine($"Недостаточно средств (нужно {totalCost:C}, у вас {balance:C}).");
                        continue;
                    }

                    balance -= totalCost;
                    db.InsertGameState(balance);
                    db.AddTransaction(-totalCost, "PurchaseExpense", $"Закупка {selected.Name} x{qty}");
                    db.IncreaseInventory(selected.PartId, qty);
                    Console.WriteLine($"Куплено {qty} шт. {selected.Name}. Баланс: {balance:C}");
                }
                else if (choice == "0")
                {
                    Console.WriteLine("Выход. До свидания!");
                    break;
                }
                else
                {
                    Console.WriteLine("Неверный выбор.");
                }
            }
        }

        static bool IsYes(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input.Trim().ToLowerInvariant();
            return s == "y" | s == "yes" || s == "да";
        }


        static void ApplyPenalty(DbHelper db, ref decimal balance, decimal penalty)
        {
            balance -= penalty;
            db.InsertGameState(balance);
            db.AddTransaction(-penalty, "PenaltyExpense", "Отказ клиенту / неудача ремонта");
            Console.WriteLine($"Штраф {penalty:C} применён. Баланс: {balance:C}");
        }
    }

    class DbHelper
    {
        private readonly string _cs;
        public DbHelper(string connectionString) { _cs = connectionString; }

        public decimal GetBalance()
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT TOP(1) Balance FROM GameState ORDER BY GameStateId DESC";
                c.Open();
                var r = cmd.ExecuteScalar();
                return r == null || r == DBNull.Value ? 0m : Convert.ToDecimal(r);
            }
        }

        public void InsertGameState(decimal balance)
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO GameState (Balance) VALUES (@b)";
                cmd.Parameters.AddWithValue("@b", balance);
                c.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<InventoryRow> GetInventory()
        {
            var list = new List<InventoryRow>();
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"SELECT i.PartId, p.Name, i.Quantity
                                    FROM InventoryItems i
                                    JOIN Parts p ON i.PartId = p.PartId
                                    ORDER BY p.Name";
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new InventoryRow
                        {
                            PartId = r.GetInt32(0),
                            PartName = r.GetString(1),
                            Quantity = r.GetInt32(2)
                        });
                    }
                }
            }
            return list;
        }

        public InventoryRow GetInventoryItemByPartId(int partId)
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"SELECT i.PartId, p.Name, i.Quantity
                                    FROM InventoryItems i
                                    JOIN Parts p ON i.PartId = p.PartId
                                    WHERE i.PartId = @pid";
                cmd.Parameters.AddWithValue("@pid", partId);
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new InventoryRow
                        {
                            PartId = r.GetInt32(0),
                            PartName = r.GetString(1),
                            Quantity = r.GetInt32(2)
                        };
                    }
                }
            }
            return null;
        }

        public void DecreaseInventory(int partId, int qty)
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"UPDATE InventoryItems SET Quantity = CASE WHEN Quantity>=@q THEN Quantity-@q ELSE 0 END WHERE PartId=@pid";
                cmd.Parameters.AddWithValue("@q", qty);
                cmd.Parameters.AddWithValue("@pid", partId);
                c.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void IncreaseInventory(int partId, int qty)
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"
                    IF EXISTS(SELECT 1 FROM InventoryItems WHERE PartId=@pid)
                        UPDATE InventoryItems SET Quantity = Quantity + @q WHERE PartId=@pid
                    ELSE
                        INSERT INTO InventoryItems (PartId, Quantity) VALUES(@pid, @q)";
                cmd.Parameters.AddWithValue("@q", qty);
                cmd.Parameters.AddWithValue("@pid", partId);
                c.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<PartRow> GetAllParts()
        {
            return GetAllPartsInternal("SELECT PartId, Name, PurchasePrice, SalePrice FROM Parts ORDER BY Name");
        }

        public List<PartRow> GetAllPartsSimple() => GetAllParts();

        private List<PartRow> GetAllPartsInternal(string sql)
        {
            var list = new List<PartRow>();
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new PartRow
                        {
                            PartId = r.GetInt32(0),
                            Name = r.GetString(1),
                            PurchasePrice = r.GetDecimal(2),
                            SalePrice = r.GetDecimal(3)
                        });
                    }
                }
            }
            return list;
        }

        public PartRow GetPartById(int id)
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT PartId, Name, PurchasePrice, SalePrice FROM Parts WHERE PartId=@id";
                cmd.Parameters.AddWithValue("@id", id);
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new PartRow
                        {
                            PartId = r.GetInt32(0),
                            Name = r.GetString(1),
                            PurchasePrice = r.GetDecimal(2),
                            SalePrice = r.GetDecimal(3)
                        };
                    }
                }
            }
            return null;
        }

        public void AddTransaction(decimal amount, string type, string desc)
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO Transactions (DateCreated, Amount, Type, Description) VALUES (SYSUTCDATETIME(), @a, @t, @d)";
                cmd.Parameters.AddWithValue("@a", amount);
                cmd.Parameters.AddWithValue("@t", type);
                cmd.Parameters.AddWithValue("@d", desc ?? (object)DBNull.Value);
                c.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public CarRow GetRandomCar()
        {
            using (var c = new SqlConnection(_cs))
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT TOP 1 c.CarId, c.Model, c.LicensePlate, c.BrokenPartId, p.Name AS BrokenPartName, c.RepairPrice
                    FROM Cars c
                    JOIN Parts p ON c.BrokenPartId = p.PartId
                    ORDER BY NEWID()";
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return new CarRow
                        {
                            CarId = r.GetInt32(0),
                            Model = r.GetString(1),
                            LicensePlate = r.GetString(2),
                            BrokenPartId = r.GetInt32(3),
                            BrokenPartName = r.GetString(4),
                            RepairPrice = r.GetDecimal(5)
                        };
                    }
                }
            }
            return null;
        }
    }

    class InventoryRow { public int PartId; public string PartName; public int Quantity; }
    class PartRow { public int PartId; public string Name; public decimal PurchasePrice; public decimal SalePrice; }
    class CarRow { public int CarId; public string Model; public string LicensePlate; public int BrokenPartId; public string BrokenPartName; public decimal RepairPrice; }
}
