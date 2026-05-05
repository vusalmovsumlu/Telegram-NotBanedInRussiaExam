using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Telegram_NotBanedInRussiaExam.DAL;
using Telegram_NotBanedInRussiaExam.Enteties;

namespace Telegram_NotBanedInRussiaExam
{
    class Program
    {
        static TcpListener? listener;
        static Dictionary<int, UserInfo> onlineUsers = new Dictionary<int, UserInfo>();

        public class UserInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public TcpClient Client { get; set; } = null!;
            public BinaryWriter bw { get; set; } = null!;
            public string RemoteEndPoint { get; set; } = string.Empty;
        }

        static void Main(string[] args)
        {
            var ep = new IPEndPoint(IPAddress.Parse("172.17.0.70"), 5000);

            listener = new TcpListener(ep);
            listener.Start();

            Console.WriteLine($"Listening on {listener.LocalEndpoint}");
            SetAllUsersOffline();

            while (true)
            {
                var client = listener.AcceptTcpClient();
                Task.Run(() => HandleClient(client));
            }
        }

        static void HandleClient(TcpClient client)
        {
            int currentUserId = -1;
            string clientIp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            string savedemail = "";
            string savedcode = "";
            DateTime savedtime = DateTime.MinValue;

            try
            {
                var stream = client.GetStream();
                using var br = new BinaryReader(stream, Encoding.UTF8, true);
                using var bw = new BinaryWriter(stream, Encoding.UTF8, true);

                while (true)
                {
                    string packet = br.ReadString();
                    string[] parts = packet.Split('|');
                    string command = parts[0];

                    if (command == "register_request")
                    {
                        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        {
                            bw.Write("email daxil edin.");
                            continue;
                        }

                        string email = parts[1].Trim();
                        using (var context = new TelegramDbContext())
                        {
                            if (context.Users.Any(u => u.Email == email))
                            {
                                bw.Write("bu email artiq qeydiyyatdan kecib.");
                                continue;
                            }
                        }

                        string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                        savedemail = email;
                        savedcode = code;
                        savedtime = DateTime.Now.AddMinutes(10);

                        if (!SendVerificationEmail(email, code, out string error))
                        {
                            savedemail = "";
                            savedcode = "";
                            savedtime = DateTime.MinValue;
                            bw.Write($"email gonderilmedi: {error}");
                            continue;
                        }

                        bw.Write("tesdiq kodu gonderildi.");
                        continue;
                    }

                    if (command == "register_complete")
                    {
                        if (parts.Length < 5)
                        {
                            bw.Write("email, kod, username ve parol daxil edin.");
                            continue;
                        }

                        string email = parts[1].Trim();
                        string code = parts[2].Trim();
                        string username = parts[3].Trim();
                        string password = parts[4];

                        if (savedemail != email)
                        {
                            bw.Write("tesdiq kodu sehvdir ve ya vaxti bitib.");
                            continue;
                        }

                        if (savedtime < DateTime.Now || savedcode != code)
                        {
                            bw.Write("tesdiq kodu sehvdir ve ya vaxti bitib.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                        {
                            bw.Write("username ve parol daxil edin.");
                            continue;
                        }

                        using (var context = new TelegramDbContext())
                        {
                            if (context.Users.Any(u => u.Name == username))
                            {
                                bw.Write("bu username artiq var.");
                                continue;
                            }

                            context.Users.Add(new User
                            {
                                Name = username,
                                Email = email,
                                Password = password,
                                IsOnline = false
                            });
                            context.SaveChanges();
                        }

                        savedemail = "";
                        savedcode = "";
                        savedtime = DateTime.MinValue;
                        bw.Write("qeydiyyat tamamlandi.");
                        continue;
                    }

                    if (command == "login")
                    {
                        if (parts.Length < 3)
                        {
                            bw.Write("username ve parol daxil edin.");
                            continue;
                        }

                        string username = parts[1].Trim();
                        string password = parts[2];

                        using (var context = new TelegramDbContext())
                        {
                            User? dbUser = context.Users.FirstOrDefault(u => u.Name == username && u.Password == password);
                            if (dbUser == null)
                            {
                                bw.Write("username ve ya parol sehvdir.");
                                continue;
                            }

                            dbUser.IsOnline = true;
                            context.SaveChanges();

                            var userInfo = new UserInfo
                            {
                                Id = dbUser.Id,
                                Name = dbUser.Name,
                                Client = client,
                                bw = bw,
                                RemoteEndPoint = clientIp
                            };

                            if (onlineUsers.ContainsKey(dbUser.Id))
                            {
                                onlineUsers[dbUser.Id] = userInfo;
                            }
                            else
                            {
                                onlineUsers.Add(dbUser.Id, userInfo);
                            }

                            currentUserId = dbUser.Id;
                            Console.WriteLine($"{dbUser.Name} connected");
                            bw.Write($"{dbUser.Id}|{dbUser.Name}");
                        }

                        BroadcastUserList();
                        continue;
                    }

                    if (command == "get_users")
                    {
                        SendUserList(bw);
                        continue;
                    }

                    if (command == "send")
                    {
                        if (currentUserId == -1)
                        {
                            bw.Write("evvel login olun.");
                            continue;
                        }

                        HandleMessage(currentUserId, parts, client);
                        continue;
                    }

                    bw.Write("emr sehvdir.");
                }
            }
            catch
            {
                DisconnectUser(currentUserId);
            }
            finally
            {
                client.Close();
            }
        }

        static void HandleMessage(int senderId, string[] parts, TcpClient client)
        {
            if (parts.Length < 4 || !int.TryParse(parts[1], out int receiverId))
            {
                return;
            }

            string messageType = parts[2];
            string content = string.Join("|", parts.Skip(3));
            Console.WriteLine($"CLIENT {senderId} to {receiverId}: [Type: {messageType}]");

            using (var context = new TelegramDbContext())
            {
                if (!context.Users.Any(u => u.Id == receiverId))
                {
                    return;
                }

                string senderIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                context.Messages.Add(new Message
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    MessageType = messageType,
                    Content = content,
                    SenderIp = senderIp,
                    ReceiverIp = onlineUsers.ContainsKey(receiverId) ? onlineUsers[receiverId].RemoteEndPoint : string.Empty,
                    Timestamp = DateTime.Now
                });
                context.SaveChanges();
            }

            if (onlineUsers.ContainsKey(receiverId))
            {
                try
                {
                    var receiverInfo = onlineUsers[receiverId];
                    receiverInfo.bw.Write($"msg|{senderId}|{messageType}|{content}");
                    Console.WriteLine($"Forwarded to {receiverId}");
                }
                catch
                {
                    DisconnectUser(receiverId);
                    SendOfflineEmailNotification(receiverId, senderId, messageType);
                }
            }
            else
            {
                Console.WriteLine($"receiver {receiverId} is offline");
                SendOfflineEmailNotification(receiverId, senderId, messageType);
            }
        }

        static void SendUserList(BinaryWriter bw)
        {
            using var context = new TelegramDbContext();
            var list = context.Users
                .OrderBy(u => u.Name)
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Name,
                    Email = u.Email,
                    isOnline = u.IsOnline
                })
                .ToList();

            bw.Write("user_list|" + JsonSerializer.Serialize(list));
        }

        static void BroadcastUserList()
        {
            foreach (var u in onlineUsers.Values)
            {
                try
                {
                    SendUserList(u.bw);
                }
                catch
                {
                }
            }
        }

        static void DisconnectUser(int userId)
        {
            if (userId == -1)
            {
                return;
            }

            if (onlineUsers.ContainsKey(userId))
            {
                var removedUser = onlineUsers[userId];
                onlineUsers.Remove(userId);
                using var context = new TelegramDbContext();
                var dbUser = context.Users.Find(userId);
                if (dbUser != null)
                {
                    dbUser.IsOnline = false;
                    context.SaveChanges();
                }

                Console.WriteLine($"{removedUser.Name} disconnected");
                BroadcastUserList();
            }
        }

        static bool SendVerificationEmail(string email, string code, out string error)
        {
            return SendEmail(email, "Telegram registration code", $"Your verification code is: {code}", out error);
        }

        static void SendOfflineEmailNotification(int receiverId, int senderId, string msgType)
        {
            try
            {
                using var context = new TelegramDbContext();
                var receiver = context.Users.Find(receiverId);
                var sender = context.Users.Find(senderId);

                if (receiver == null || sender == null)
                {
                    return;
                }

                string typeText = msgType == "text" ? "text message" : msgType == "file" ? "file/image" : "voice message";
                string body = $"Hello {receiver.Name},\n\n{sender.Name} sent you a new {typeText} while you were offline.\nOpen the client to read it.";
                if (!SendEmail(receiver.Email, "Telegram: new message", body, out string error))
                {
                    Console.WriteLine($"Offline notification failed: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Offline notification failed: {ex.Message}");
            }
        }

        static bool SendEmail(string to, string subject, string body, out string error)
        {
            try
            {
                string fromemail = "vusal.2008.27@gmail.com";
                string apppassword = "mqufuevohymdebgw";

                using var mail = new MailMessage();
                mail.From = new MailAddress(fromemail);
                mail.To.Add(to);
                mail.Subject = subject;
                mail.Body = body;

                using var smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.Credentials = new NetworkCredential(fromemail, apppassword);
                smtp.EnableSsl = true;
                smtp.Send(mail);

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static void SetAllUsersOffline()
        {
            try
            {
                using var context = new TelegramDbContext();
                foreach (var user in context.Users.Where(u => u.IsOnline))
                {
                    user.IsOnline = false;
                }
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not reset online users: {ex.Message}");
            }
        }
    }
}
