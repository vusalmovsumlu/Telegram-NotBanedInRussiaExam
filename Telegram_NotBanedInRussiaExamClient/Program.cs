using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Telegram_NotBanedInRussiaExam.Enteties;

namespace Telegram_NotBanedInRussiaExamClient
{
    class Program
    {
        private const string ServerIp = "127.0.0.1";
        private const int ServerPort = 5000;

        private static User? currentUser;
        private static System.Net.Sockets.TcpClient? client;
        private static BinaryReader? br;
        private static BinaryWriter? bw;
        private static Thread? listenThread;
        private static readonly AutoResetEvent responseReceived = new AutoResetEvent(false);
        private static List<UserListItem> lastUsers = new List<UserListItem>();

        [DllImport("winmm.dll", EntryPoint = "mciSendString")]
        private static extern long mciSendString(string command, string? returnValue, int returnLength, IntPtr winHandle);

        static void Main(string[] args)
        {
            Console.Title = "Telegram Client";

            while (true)
            {
                while (currentUser == null)
                {
                    StartMenu();
                }

                MainMenu();
            }
        }

        private static void Register()
        {
            Console.Clear();
            Console.WriteLine("--- QEYDIYYAT ---");
            Console.Write("Yeni Username daxil edin: ");
            string? username = Console.ReadLine();

            Console.Write("Email daxil edin: ");
            string? email = Console.ReadLine();

            Console.Write("Parol teyin edin: ");
            string? password = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Butun melumatlari doldurun.");
                Console.ReadLine();
                return;
            }

            // Server sene email ile 6 reqemli kod gonderir.
            try
            {
                client = new System.Net.Sockets.TcpClient();
                client.Connect(ServerIp, ServerPort);
                br = new BinaryReader(client.GetStream(), Encoding.UTF8, true);
                bw = new BinaryWriter(client.GetStream(), Encoding.UTF8, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Servere qosulmaq olmadi: " + ex.Message);
                Console.ReadLine();
                return;
            }

            bw.Write($"register_request|{email}");
            string response = br.ReadString();
            if (response != "tesdiq kodu gonderildi.")
            {
                PrintError(response);
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"[SISTEM] {email} unvanina kod gonderildi.");
            Console.Write("Emaile gelen kodu daxil edin: ");
            string? code = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(code))
            {
                Console.WriteLine("Tesdiq kodu bos ola bilmez.");
                Console.ReadLine();
                return;
            }

            bw.Write($"register_complete|{email}|{code}|{username}|{password}");
            response = br.ReadString();
            if (response == "qeydiyyat tamamlandi.")
            {
                Console.WriteLine("Qeydiyyat ugurla tamamlandi! Indi sisteme daxil ola bilersiniz.");
            }
            else
            {
                PrintError(response);
            }

            Console.ReadLine();
        }

        private static void Login()
        {
            Console.Clear();
            Console.WriteLine("--- LOGIN ---");
            Console.Write("username: ");
            string? username = Console.ReadLine();
            Console.Write("password: ");
            string? password = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("username ve password daxil edin.");
                Console.ReadLine();
                return;
            }

            try
            {
                client = new System.Net.Sockets.TcpClient();
                client.Connect(ServerIp, ServerPort);
                br = new BinaryReader(client.GetStream(), Encoding.UTF8, true);
                bw = new BinaryWriter(client.GetStream(), Encoding.UTF8, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Servere qosulmaq olmadi: " + ex.Message);
                Console.ReadLine();
                return;
            }

            bw.Write($"login|{username}|{password}");
            string response = br.ReadString();
            string[] parts = response.Split('|');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int userId))
            {
                currentUser = new User
                {
                    Id = userId,
                    Name = parts[1],
                    Password = string.Empty,
                    Email = string.Empty,
                    IsOnline = true
                };

                listenThread = new Thread(ListenForMessages);
                listenThread.IsBackground = true;
                listenThread.Start();

                Console.WriteLine($"Xos geldin, {currentUser.Name}");
            }
            else
            {
                PrintError(response);
            }

            Console.ReadLine();
        }

        private static void StartMenu()
        {
            Console.Clear();
            Console.WriteLine("1. Login");
            Console.WriteLine("2. Register");
            Console.Write("Seciminiz: ");

            string? choice = Console.ReadLine();
            if (choice == "1")
            {
                Login();
            }
            else if (choice == "2")
            {
                Register();
            }
        }

        private static void MainMenu()
        {
            while (currentUser != null)
            {
                Console.Clear();
                Console.WriteLine($"Profil: {currentUser.Name}");
                Console.WriteLine("1. Show Users");
                Console.WriteLine("2. Go Chat");
                Console.WriteLine("3. Logout");
                Console.Write("Seciminiz: ");

                string? choice = Console.ReadLine();
                if (choice == "1")
                {
                    ShowUsers();
                }
                else if (choice == "2")
                {
                    GoChat();
                }
                else if (choice == "3")
                {
                    Logout();
                }
            }
        }

        private static void ShowUsers()
        {
            Console.Clear();
            Console.WriteLine("--- USERS ---");

            RequestUsers();
            foreach (UserListItem user in lastUsers.Where(u => currentUser == null || u.id != currentUser.Id))
            {
                string status = user.isOnline ? "Online" : "Offline";
                Console.WriteLine($"ID: {user.id} | username: {user.username} | email: {user.email} | {status}");
            }

            Console.WriteLine("\nEnter basib geri qayidin...");
            Console.ReadLine();
        }

        private static void GoChat()
        {
            Console.Clear();
            Console.WriteLine("--- CHAT ---");
            if (currentUser == null)
            {
                Console.WriteLine("Chat ucun evvel Login olun ve ya Register edin.");
                Console.WriteLine("1. Login");
                Console.WriteLine("2. Register");
                Console.Write("Seciminiz: ");
                string? authChoice = Console.ReadLine();
                if (authChoice == "1")
                {
                    Login();
                }
                else if (authChoice == "2")
                {
                    Register();
                    Login();
                }

                if (currentUser == null)
                {
                    return;
                }

                Console.Clear();
                Console.WriteLine("--- CHAT ---");
            }

            RequestUsers();
            foreach (UserListItem user in lastUsers.Where(u => currentUser == null || u.id != currentUser.Id))
            {
                Console.WriteLine($"ID: {user.id} | {user.username} | {(user.isOnline ? "Online" : "Offline")}");
            }

            Console.Write("Mesaj gondereceyiniz user ID: ");
            if (!int.TryParse(Console.ReadLine(), out int receiverId) || lastUsers.All(u => u.id != receiverId))
            {
                Console.WriteLine("Duzgun ID daxil edin.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Chat basladi. Cixmaq ucun /exit yazin.");

            while (true)
            {
                Console.WriteLine("\nNe gondermek isteyirsiniz?");
                Console.WriteLine("1. Text");
                Console.WriteLine("2. File/Image");
                Console.WriteLine("3. Voice message");
                Console.WriteLine("4. Exit Chat");
                Console.Write("Seciminiz: ");

                string? choice = Console.ReadLine();
                if (choice == "4" || choice == "/exit")
                {
                    break;
                }

                string messageType;
                string content;

                if (choice == "1")
                {
                    messageType = "text";
                    Console.Write("Mesaj: ");
                    content = Console.ReadLine() ?? string.Empty;
                    if (content == "/exit")
                    {
                        break;
                    }
                }
                else if (choice == "2")
                {
                    messageType = "file";
                    Console.Write("Fayl path-i: ");
                    string? filePath = Console.ReadLine();
                    content = BuildFileContent(filePath);
                    if (content.Length == 0)
                    {
                        continue;
                    }
                }
                else if (choice == "3")
                {
                    messageType = "voice";
                    content = RecordVoice();
                    if (content.Length == 0)
                    {
                        continue;
                    }
                }
                else
                {
                    Console.WriteLine("Yanlis choice.");
                    continue;
                }

                bw?.Write($"send|{receiverId}|{messageType}|{content}");
                Console.WriteLine("Mesaj gonderildi.");
            }
        }

        private static void ListenForMessages()
        {
            try
            {
                while (br != null)
                {
                    string packet = br.ReadString();
                    HandleIncomingPacket(packet);
                }
            }
            catch
            {
                Console.WriteLine("\nServer baglantisi kesildi.");
            }
        }

        private static void HandleIncomingPacket(string packet)
        {
            if (packet.StartsWith("user_list|"))
            {
                string json = packet.Substring("user_list|".Length);
                try
                {
                    lastUsers = JsonSerializer.Deserialize<List<UserListItem>>(json) ?? new List<UserListItem>();
                }
                catch
                {
                    lastUsers = new List<UserListItem>();
                }
                responseReceived.Set();
                return;
            }

            if (!packet.StartsWith("user_list|") && !packet.StartsWith("msg|"))
            {
                responseReceived.Set();
                return;
            }

            string[] parts = packet.Split('|');
            if (parts.Length < 4 || parts[0] != "msg")
            {
                return;
            }

            string senderId = parts[1];
            string messageType = parts[2];
            string content = string.Join("|", parts.Skip(3));
            string senderName = "user " + senderId;

            foreach (UserListItem user in lastUsers)
            {
                if (user.id.ToString() == senderId)
                {
                    senderName = user.username;
                    break;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            if (messageType == "text")
            {
                Console.WriteLine($"\n[{senderName}]: {content}");
            }
            else if (messageType == "file" || messageType == "voice")
            {
                string savedPath = SaveIncomingFile(content, messageType);
                Console.WriteLine($"\n[{senderName}] {messageType} gonderdi: {savedPath}");
            }
            else
            {
                Console.WriteLine($"\n[{senderName}] [{messageType}]: {content}");
            }
            Console.ResetColor();
        }

        private static void RequestUsers()
        {
            if (client == null || bw == null || br == null)
            {
                try
                {
                    client = new System.Net.Sockets.TcpClient();
                    client.Connect(ServerIp, ServerPort);
                    br = new BinaryReader(client.GetStream(), Encoding.UTF8, true);
                    bw = new BinaryWriter(client.GetStream(), Encoding.UTF8, true);
                }
                catch
                {
                    Console.WriteLine("Servere qosulmaq olmadi.");
                    return;
                }
            }

            if (currentUser == null)
            {
                bw.Write("get_users");

                if (br != null)
                {
                    string packet = br.ReadString();
                    if (packet.StartsWith("user_list|"))
                    {
                        HandleIncomingPacket(packet);
                    }
                }
                return;
            }

            responseReceived.Reset();
            bw.Write("get_users");
            responseReceived.WaitOne(2000);
        }

        private static string BuildFileContent(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Console.WriteLine("Fayl tapilmadi.");
                return string.Empty;
            }

            try
            {
                string fileName = Path.GetFileName(filePath);
                string base64 = Convert.ToBase64String(File.ReadAllBytes(filePath));
                return $"{fileName};{base64}";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fayl oxunmadi: " + ex.Message);
                return string.Empty;
            }
        }

        private static string RecordVoice()
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("Voice recording only works on Windows with winmm.dll.");
                return string.Empty;
            }

            Console.WriteLine("Sesi baslatmaq ucun S, dayandirmaq ucun E basin.");

            while (Console.ReadKey(true).Key != ConsoleKey.S)
            {
            }

            Console.WriteLine("Ses yazilir...");
            if (mciSendString("open new Type waveaudio Alias recsound", null, 0, IntPtr.Zero) != 0 ||
                mciSendString("record recsound", null, 0, IntPtr.Zero) != 0)
            {
                Console.WriteLine("Ses yazma baslamadi.");
                mciSendString("close recsound", null, 0, IntPtr.Zero);
                return string.Empty;
            }

            while (Console.ReadKey(true).Key != ConsoleKey.E)
            {
            }

            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoiceMessages");
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, $"voice_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
            long saveResult = mciSendString($"save recsound \"{filePath}\"", null, 0, IntPtr.Zero);
            mciSendString("close recsound", null, 0, IntPtr.Zero);

            if (saveResult != 0 || !File.Exists(filePath))
            {
                Console.WriteLine("Ses fayli saxlanmadi.");
                return string.Empty;
            }

            Console.WriteLine("Ses yazildi.");
            return BuildFileContent(filePath);
        }

        private static string SaveIncomingFile(string content, string messageType)
        {
            int separatorIndex = content.IndexOf(';');
            if (separatorIndex <= 0 || separatorIndex == content.Length - 1)
            {
                return "fayl format xetasi";
            }

            try
            {
                string fileName = Path.GetFileName(content.Substring(0, separatorIndex));
                string base64 = content.Substring(separatorIndex + 1);
                string folderName = messageType == "voice" ? "ReceivedVoiceMessages" : "ReceivedFiles";
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
                Directory.CreateDirectory(folder);

                string savePath = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd_HHmmss}_{fileName}");
                File.WriteAllBytes(savePath, Convert.FromBase64String(base64));
                return savePath;
            }
            catch (Exception ex)
            {
                return "fayl saxlanmadi: " + ex.Message;
            }
        }

        private static void PrintError(string response)
        {
            Console.WriteLine(response);
        }

        private static void Logout()
        {
            try
            {
                bw?.Close();
                br?.Close();
                client?.Close();
            }
            catch
            {
            }

            currentUser = null;
            bw = null;
            br = null;
            client = null;
        }

        private class UserListItem
        {
            public int id { get; set; }
            public string username { get; set; } = string.Empty;
            public string email { get; set; } = string.Empty;
            public bool isOnline { get; set; }
        }
    }
}
