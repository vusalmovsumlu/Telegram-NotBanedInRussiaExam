using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Telegram_NotBanedInRussiaExam.DAL;
using Telegram_NotBanedInRussiaExam.Enteties;

namespace Telegram_NotBanedInRussiaExamClient
{
    class Program
    {
        static User CurrentUser = null;

        static void Main(string[] args)
        {
            while (CurrentUser == null)
            {
                Console.Clear();
                Console.WriteLine("=== TELEGRAM (Not Banned) ===");
                Console.WriteLine("1. Daxil ol (Login)");
                Console.WriteLine("2. Qeydiyyatdan keç (Register)");
                Console.Write("Seçiminiz: ");
                string secim = Console.ReadLine();

                if (secim == "1")
                {
                    Login();
                }
                else if (secim == "2")
                {
                    Register();
                }
            }
            MainMenu();
        }

        static void Register()
        {
            Console.Clear();
            Console.WriteLine("--- QEYDİYYAT ---");
            Console.Write("Yeni Username daxil edin: ");
            string username = Console.ReadLine();
            Console.Write("Email daxil edin: ");
            string email = Console.ReadLine();
            Console.Write("Parol təyin edin: ");
            string password = Console.ReadLine();
            Random rnd = new Random();
            int verificationCode = rnd.Next(100000, 999999);
           
            try
            {                          
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress("vusal.2008.27@gmail.com");
                mail.To.Add(email);
                mail.Subject = "Telegram Qeydiyyat Kodu";
                mail.Body = $"Sizin təsdiq kodunuz: {verificationCode}";

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.Credentials = new NetworkCredential("senin_emailin@gmail.com", "app_password_bura_yazilir");
                smtp.EnableSsl = true;
                smtp.Send(mail);
                

               
                Console.WriteLine($"\n[SİSTEM MESAJI] {email} ünvanına kod göndərildi. (SİMULYASİYA KOD: {verificationCode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Mail göndərilərkən xəta: " + ex.Message);
                return;
            }

            Console.Write("\nEmailə gələn 6 rəqəmli kodu daxil edin: ");
            int userInput;
            if (int.TryParse(Console.ReadLine(), out userInput) && userInput == verificationCode)
            {
                using (var context = new TelegramDbContext())
                {
                    if (context.Users.Any(u => u.Name == username))
                    {
                        Console.WriteLine("Bu Username artıq məşğuldur! Davam etmək üçün Enter basın...");
                        Console.ReadLine();
                        return;
                    }

                    var newUser = new User
                    {
                        Name = username,
                        Email = email,
                        Password = password,
                        IsOnline = false
                    };

                    context.Users.Add(newUser);
                    context.SaveChanges();
                    Console.WriteLine("Qeydiyyat uğurla tamamlandı!");
                    Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine("Yanlış kod daxil etdiniz");
                Console.ReadLine();
            }
        }

        static void Login()
        {
            Console.Clear();
            Console.WriteLine("--- GİRİŞ ---");
            Console.Write("Username: ");
            string username = Console.ReadLine();

            Console.Write("Parol: ");
            string password = Console.ReadLine();

            using (var context = new TelegramDbContext())
            {
                var user = context.Users.FirstOrDefault(u => u.Name == username && u.Password == password);

                if (user != null)
                {
                    CurrentUser = user;
                    user.IsOnline = true;
                    context.SaveChanges();

                    Console.WriteLine($"Xoş gəldin, {user.Name}! (Davam etmək üçün Enter)");
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("Username və ya parol yanlışdır! (Davam etmək üçün Enter)");
                    Console.ReadLine();
                }
            }
        }

        static void MainMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"[ Profil: {CurrentUser.Name} ]");
                Console.WriteLine("Menu:");
                Console.WriteLine("1. Show Users");
                Console.WriteLine("2. Go Chat");
                Console.WriteLine("3. Çıxış");
                Console.Write("Seçiminiz: ");
                string secim = Console.ReadLine();

                if (secim == "1")
                {
                    ShowUsers();
                }
                else if (secim == "2")
                {
                    GoChat();
                }
                else if (secim == "3")
                {
                    using (var context = new TelegramDbContext())
                    {
                        var user = context.Users.Find(CurrentUser.Id);
                        if (user != null)
                        {
                            user.IsOnline = false;
                            context.SaveChanges();
                        }
                    }
                    CurrentUser = null;
                    break;
                }
            }
        }

        static void ShowUsers()
        {
            Console.Clear();
            Console.WriteLine("--- SİSTEMDƏKİ İSTİFADƏÇİLƏR ---");
            using (var context = new TelegramDbContext())
            {
                var allUsers = context.Users.Where(u => u.Id != CurrentUser.Id).ToList();
                foreach (var user in allUsers)
                {
                    string status = user.IsOnline ? "[Online]" : "[Offline]";
                    Console.WriteLine($"ID: {user.Id} | Username: {user.Name} {status}");
                }
            }
            Console.WriteLine("\nGeri qayıtmaq üçün Enter basın...");
            Console.ReadLine();
        }

        static void GoChat()
        {
            Console.Clear();
            Console.WriteLine("--- ÇATA BAŞLA ---");
            Console.Write("Mesaj yazmaq istədiyiniz istifadəçinin ID-sini daxil edin: ");
            int targetUserId;

            if (int.TryParse(Console.ReadLine(), out targetUserId))
            {
                Console.WriteLine($"\nID {targetUserId} ilə çat başladılır...");
                Console.WriteLine("Nə göndərmək istəyirsiniz?");
                Console.WriteLine("1. Text\n2. File/Image\n3. Voice");
                Console.Write("Seçiminiz: ");
                string secim = Console.ReadLine();
                string messageType = "";
                string content = "";
                switch (secim)
                {
                    case "1":
                        messageType = "text";
                        Console.Write("Mesajınızı yazın: ");
                        content = Console.ReadLine();
                        break;
                    case "2":
                        messageType = "image";
                        Console.Write("Şəklin kompüterdəki yolunu daxil edin (məs: C:\\sekil.png): ");
                        content = Console.ReadLine();
                        break;
                    case "3":
                        messageType = "voice";
                        Console.Write("Səs faylının yolunu daxil edin (məs: C:\\ses.wav): ");
                        content = Console.ReadLine();
                        break;
                    default:
                        Console.WriteLine("Yanlış seçim etdiniz");
                        Console.ReadLine();
                        return;
                }

                try
                {
                    using (var client = new TcpClient())
                    {                        
                        client.Connect("127.0.0.1", 5000);
                        string gedenData = $"{CurrentUser.Id}|{targetUserId}|{messageType}|{content}";
                        byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(gedenData);
                        using (NetworkStream stream = client.GetStream())
                        {
                            stream.Write(dataBytes, 0, dataBytes.Length);
                        }
                        Console.WriteLine("\n[+] Mesaj uğurla serverə göndərildi!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Xəta baş verdi: {ex.Message}");

                }

                Console.WriteLine("\nGeri qayıtmaq üçün Enter basın...");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Düzgün ID daxil etmədiniz");
                Console.ReadLine();
            }
        }
    }
}