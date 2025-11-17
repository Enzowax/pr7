using System;
using System.Collections.Generic;

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
                    Console.WriteLine("1. Регистрация");
                    Console.WriteLine("2. Вход");
                    Console.WriteLine("3. Посмотреть товары");
                    Console.WriteLine("0. Выход");
                    Console.Write("Выбор: ");
                    string choice = Console.ReadLine();

                    if (choice == "1") Register();
                    else if (choice == "2") Login();
                    else if (choice == "3") ShowProducts();
                    else if (choice == "0") break;
                }
                else
                {
                    Console.WriteLine($"Личный кабинет ({currentUser.Username})");
                    Console.WriteLine("1. Посмотреть товары");
                    Console.WriteLine("2. Добавить товар в корзину");
                    Console.WriteLine("3. Посмотреть корзину");
                    Console.WriteLine("4. Купить товар из корзины");
                    Console.WriteLine("5. Купить ВСЮ корзину");
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
            }
        }

        static void Register()
        {
            Console.Write("Введите логин: ");
            string username = Console.ReadLine();

            Console.Write("Введите пароль: ");
            string pass1 = Console.ReadLine();

            Console.Write("Повторите пароль: ");
            string pass2 = Console.ReadLine();

            if (pass1 != pass2)
            {
                Console.WriteLine("Пароли не совпадают!");
                return;
            }

            if (db.UserExists(username))
            {
                Console.WriteLine("Такой пользователь уже существует!");
                return;
            }

            db.CreateUser(username, pass1);
            Console.WriteLine("Регистрация успешна!");
        }

        static void Login()
        {
            Console.Write("Логин: ");
            string username = Console.ReadLine();

            Console.Write("Пароль: ");
            string password = Console.ReadLine();

            currentUser = db.AuthUser(username, password);

            if (currentUser == null)
                Console.WriteLine("Неверный логин или пароль!");
            else
                Console.WriteLine("Успешный вход!");
        }

        static void ShowProducts()
        {
            Console.WriteLine("ТОВАРЫ");
            List<Product> products = db.GetProducts();

            foreach (var p in products)
                Console.WriteLine($"{p.Id}. {p.Name} — {p.Price} руб.");
        }

        static void AddToCart()
        {
            Console.Write("Введите ID товара: ");
            int id;
            if (!int.TryParse(Console.ReadLine(), out id))
            {
                Console.WriteLine("Ошибка!");
                return;
            }

            db.AddToCart(currentUser.Id, id);
            Console.WriteLine("Товар добавлен в корзину!");
        }

        static void ShowCart()
        {
            List<Product> cart = db.GetCart(currentUser.Id);

            Console.WriteLine("Корзина");
            if (cart.Count == 0)
            {
                Console.WriteLine("Корзина пуста!");
                return;
            }

            foreach (var p in cart)
                Console.WriteLine($"{p.Id}. {p.Name} — {p.Price} руб.");
        }

        static int ChoosePVZ()
        {
            List<PVZ> pvz = db.GetPVZ();

            Console.WriteLine("Выберите ПВЗ");
            foreach (var p in pvz)
                Console.WriteLine($"{p.Id}. {p.Name}, {p.Address}");

            Console.Write("Выбор: ");
            int id;
            if (!int.TryParse(Console.ReadLine(), out id)) return -1;
            return id;
        }

        static void BuyOne()
        {
            List<Product> cart = db.GetCart(currentUser.Id);
            if (cart.Count == 0) { Console.WriteLine("Корзина пуста!"); return; }

            Console.Write("Введите ID товара для покупки: ");
            int id;
            if (!int.TryParse(Console.ReadLine(), out id)) return;

            Product pr = cart.Find(p => p.Id == id);
            if (pr == null) { Console.WriteLine("Нет такого товара в корзине!"); return; }

            int pvzId = ChoosePVZ();
            if (pvzId == -1) return;

            db.CreateOrder(currentUser.Id, pr.Id, pvzId);
            db.RemoveFromCart(currentUser.Id, pr.Id);
            Console.WriteLine("Товар куплен!");
        }

        static void BuyAll()
        {
            List<Product> cart = db.GetCart(currentUser.Id);
            if (cart.Count == 0) { Console.WriteLine("Корзина пуста!"); return; }

            int pvzId = ChoosePVZ();
            if (pvzId == -1) return;

            foreach (var p in cart)
            {
                db.CreateOrder(currentUser.Id, p.Id, pvzId);
                db.RemoveFromCart(currentUser.Id, p.Id);
            }

            Console.WriteLine("Вся корзина куплена!");
        }

        static void ShowOrders()
        {
            List<Order> orders = db.GetUserOrders(currentUser.Id);

            Console.WriteLine("История заказов");
            foreach (var o in orders)
                Console.WriteLine($"{o.Id}. Товар {o.ProductId}, ПВЗ {o.PVZId}, Дата: {o.Date}");
        }
    }
}
