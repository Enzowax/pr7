using System;
using System.Collections.Generic;
using System.Net;
using ConsoleApp9.Models;

namespace MarketplaceApp
{
    class Program
    {
        static DatabaseService db = new DatabaseService(@"Data Source=DESKTOP-DI2A3F9;Initial Catalog=MarketplaceDB;Integrated Security=True;");
        static User currentUser = null;

        static void Main(string[] args)
        {
            while (true)
            {
                if (currentUser == null)
                {
                    Console.WriteLine("Маркетплейс Нагиева");
                    Console.WriteLine("1. Посмотреть товары");
                    Console.WriteLine("2. Регистрация");
                    Console.WriteLine("3. Вход");
                    Console.WriteLine("0. Выход");
                    Console.Write("Выбор: ");
                    string choice = Console.ReadLine();

                    if (choice == "1") ShowProducts();  
                    else if (choice == "2") Register();
                    else if (choice == "3") Login();
                    else if (choice == "0") break;
                }
                else
                {
                    Console.WriteLine($"Личный кабинет {currentUser.Username}");
                    Console.WriteLine("1. Посмотреть товары");
                    Console.WriteLine("2. Добавить товар в корзину");
                    Console.WriteLine("3. Посмотреть корзину");
                    Console.WriteLine("4. Купить товар из корзины");
                    Console.WriteLine("5. Купить всю корзину");
                    Console.WriteLine("6. История заказов");
                    Console.WriteLine("7. Выйти из аккаунта");
                    Console.Write("Выбор: ");

                    string choice = Console.ReadLine();

                    if (choice == "1") ShowProducts();
                    else if (choice == "2") AddToCart();
                    else if (choice == "3") ShowCart();
                    else if (choice == "4") BuyOne();
                    else if (choice == "5") BuyAll();
                    else if (choice == "6") ShowOrders();
                    else if (choice == "7") currentUser = null;
                }

                Console.WriteLine("Нажмите любую клавишу...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        static void Register()
        {
            Console.Write("Введите логин: ");
            string username = Console.ReadLine();

            Console.Write("Введите пароль: ");
            string password = Console.ReadLine();

            bool success = db.RegisterUser(username, password);
            if (success)
                Console.WriteLine("Регистрация успешна!");
            else
                Console.WriteLine("Ошибка регистрации (пользователь уже существует или данные некорректны).");
        }

        static void Login()
        {
            Console.Write("Логин: ");
            string username = Console.ReadLine();

            Console.Write("Пароль: ");
            string password = Console.ReadLine();

            currentUser = db.Login(username, password);

            if (currentUser == null)
                Console.WriteLine("Неверный логин или пароль!");
            else
                Console.WriteLine("Успешный вход!");
        }

        static void ShowProducts()
        {
            List<Product> products = db.GetAllProducts();
            Console.WriteLine("Товары");

            foreach (Product p in products)
            {
                Console.WriteLine("{0}. {1} — {2} руб. (Остаток: {3})", p.Id, p.Name, p.Price, p.Stock);
            }
        }

        static void AddToCart()
        {
            Console.Write("Введите ID товара: ");
            int productId;
            if (!int.TryParse(Console.ReadLine(), out productId))
            {
                Console.WriteLine("Ошибка!");
                return;
            }

            Console.Write("Введите количество: ");
            int quantity;
            if (!int.TryParse(Console.ReadLine(), out quantity))
            {
                Console.WriteLine("Ошибка!");
                return;
            }

            bool added = db.AddToCart(currentUser.Id, productId, quantity);
            Console.WriteLine(added ? "Товар добавлен в корзину!" : "Не удалось добавить товар (не хватает на складе или ошибка).");
        }

        static void ShowCart()
        {
            List<CartItem> cart = db.GetUserCart(currentUser.Id);
            Console.WriteLine("Корзина");

            if (cart.Count == 0)
            {
                Console.WriteLine("Корзина пуста!");
                return;
            }

            foreach (CartItem item in cart)
            {
                Console.WriteLine("{0}. {1} — {2} руб., Кол-во: {3}",
                    item.ProductId, item.ProductName, item.ProductPrice, item.Quantity);
            }
        }

        static int ChoosePickupPoint()
        {
            List<PickupPoint> points = db.GetAllPickupPoints();
            Console.WriteLine("Выберите пункт выдачи");

            foreach (PickupPoint p in points)
            {
                Console.WriteLine("{0}. {1}, {2}", p.Id, p.Name, p.Address);
            }

            Console.Write("Выбор: ");
            int id;
            if (!int.TryParse(Console.ReadLine(), out id)) return -1;
            return id;
        }

        static void BuyOne()
        {
            List<CartItem> cart = db.GetUserCart(currentUser.Id);
            if (cart.Count == 0) { Console.WriteLine("Корзина пуста!"); return; }

            Console.Write("Введите ID товара для покупки: ");
            int productId;
            if (!int.TryParse(Console.ReadLine(), out productId)) return;

            CartItem item = cart.Find(c => c.ProductId == productId);
            if (item == null) { Console.WriteLine("Нет такого товара в корзине!"); return; }

            int pvzId = ChoosePickupPoint();
            if (pvzId == -1) return;

            if (db.CreateOrder(currentUser.Id, pvzId, new List<CartItem>() { item }))
                Console.WriteLine("Товар куплен!");
            else
                Console.WriteLine("Ошибка при покупке товара.");
        }

        static void BuyAll()
        {
            List<CartItem> cart = db.GetUserCart(currentUser.Id);
            if (cart.Count == 0) { Console.WriteLine("Корзина пуста!"); return; }

            int pvzId = ChoosePickupPoint();
            if (pvzId == -1) return;

            if (db.CreateOrder(currentUser.Id, pvzId, cart))
                Console.WriteLine("Вся корзина куплена!");
            else
                Console.WriteLine("Ошибка при покупке корзины.");
        }

        static void ShowOrders()
        {
            List<Order> orders = db.GetUserOrders(currentUser.Id);
            List<PickupPoint> pickupPoints = db.GetAllPickupPoints(); 
            var pickupDict = new Dictionary<int, string>();
            foreach (var p in pickupPoints)
                pickupDict[p.Id] = p.Address; 

            Console.WriteLine("История заказов");

            foreach (Order o in orders)
            {
                string address = pickupDict.ContainsKey(o.PickupPointId) ? pickupDict[o.PickupPointId] : "Неизвестно";
                Console.WriteLine("Заказ {0}, Сумма: {1} руб., ПВЗ: {2} ({3}), Дата: {4}",
                    o.Id, o.TotalPrice, o.PickupPointId, address, o.CreatedAt);

                foreach (OrderItem item in o.Items)
                {
                    Console.WriteLine(" - {0} — {1} руб., Кол-во: {2}", item.ProductName, item.UnitPrice, item.Quantity);
                }
            }
        }
    }
}
