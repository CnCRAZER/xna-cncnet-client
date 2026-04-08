using ClientCore;
using ClientCore.Extensions;
using Microsoft.Win32;
using Newtonsoft.Json;
using Rampastring.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

#nullable enable

namespace DTAClient.Domain.Multiplayer.CnCNet
{
    public class CnCNetAPI
    {
        public event Action<bool>? Initialized;
        public event EventHandler? AccountUpdated;
        // Registration URL is separate (web)
        public const string API_REGISTER_URL = "https://ladder.cncnet.org/auth/register";

        private const string API_AUTH_LOGIN = "auth/login";
        // Official API: GET /api/v1/user/account (auth: Bearer <JWT>)
        private const string API_USER_ACCOUNT = "user/account";
        // Official API: POST /api/v1/player/create (auth: Bearer <JWT>)
        private const string API_PLAYER_CREATE = "player/create";
        // Official API: POST /api/v1/player/status (auth: Bearer <JWT>)
        private const string API_PLAYER_STATUS = "player/status";

        private static string ApiBaseUrl
        {
            get
            {
                string url = ClientConfiguration.Instance.CnCNetApiUrl;
                if (!url.EndsWith("/"))
                    url += "/";
                return url;
            }
        }

        public string? Nickname { get; set; }
        public string? AuthToken { get; private set; }
        public bool IsAuthed { get; private set; }

        public List<AuthPlayer> Accounts { get; private set; } = new List<AuthPlayer>();
        public string? ErrorMessage { get; private set; }

        private const int REQUEST_TIMEOUT = 10000; // In milliseconds
        private const string tokenPath = "SOFTWARE\\CnCNet\\QuickMatch";
        private static string TokenFilePath => SafePath.CombineFilePath(ProgramConstants.ClientUserFilesPath, "access.token");

        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(REQUEST_TIMEOUT)
        };

        public CnCNetAPI() { }

        private static CnCNetAPI? _instance;
        public static CnCNetAPI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CnCNetAPI();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Checks and verifies auth token is active, retrieves latest account data.
        /// This method is safe to call from a background thread.
        /// </summary>
        public async Task InitializeAccountAsync()
        {
            try
            {
                AuthToken = ReadAuthToken();

                IsAuthed = await VerifyTokenAsync();

                Initialized?.Invoke(IsAuthed);
            }
            catch
            {
                Initialized?.Invoke(false);
                Logger.Log("Failed to get access token for QM account");
            }
        }

        /// <summary>
        /// Verifies token by calling an authenticated endpoint.
        /// </summary>
        private async Task<bool> VerifyTokenAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ApiBaseUrl + API_USER_ACCOUNT);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);

                HttpResponseMessage response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return false;

                string json = await response.Content.ReadAsStringAsync();
                List<AuthPlayer>? accounts = JsonConvert.DeserializeObject<List<AuthPlayer>>(json);
                Accounts = accounts ?? new List<AuthPlayer>();

                AccountUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return false;
            }
        }

        /// <summary>
        /// Used to login and get Auth Token.
        /// </summary>
        public async Task<bool> LoginAsync(string email, string password, bool stayLoggedIn)
        {
            try
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["email"] = email,
                    ["password"] = password
                });

                HttpResponseMessage response = await httpClient.PostAsync(ApiBaseUrl + API_AUTH_LOGIN, form);

                if (!response.IsSuccessStatusCode)
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            ErrorMessage = "You have entered an incorrect email or password.".L10N("Client:CnCNet:LoginInvalidCredentials");
                            break;
                        case HttpStatusCode.NotFound:
                            ErrorMessage = "Login service endpoint not found. Please verify CnCNetApiUrl points to the API base (e.g. https://ladder.cncnet.org/api/v1/).".L10N("Client:CnCNet:LoginEndpointNotFound");
                            break;
                        default:
                            ErrorMessage = "An error occurred, status code: " + response.StatusCode;
                            break;
                    }

                    return false;
                }

                string json = await response.Content.ReadAsStringAsync();
                AuthTokenResponse? authToken = JsonConvert.DeserializeObject<AuthTokenResponse>(json);
                AuthToken = authToken?.Token;

                bool success = await GetAccountsAsync();
                if (!success)
                    return false;

                if (stayLoggedIn)
                {
                    WriteAuthToken(AuthToken ?? string.Empty);
                    IsAuthed = true;
                }
                else
                {
                    ClearAuthToken();
                    IsAuthed = false;
                }

                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                ErrorMessage = "Connection failed: " + ex.Message;
                return false;
            }
        }

        public void Logout()
        {
            try
            {
                ClearAuthToken();
            }
            catch { }

            IsAuthed = false;
            AuthToken = string.Empty;
            Accounts.Clear();
        }

        private string ReadAuthToken()
        {
            try
            {
#if NETFRAMEWORK
                RegistryKey key = Registry.CurrentUser.OpenSubKey(tokenPath);
                if (key != null)
                {
                    string token = key.GetValue("accessToken", "").ToString();
                    key.Close();
                    return token;
                }
                return string.Empty;
#else
                if (OperatingSystem.IsWindows())
                {
                    RegistryKey? key = Registry.CurrentUser.OpenSubKey(tokenPath);
                    if (key != null)
                    {
                        string token = key.GetValue("accessToken", "")?.ToString() ?? string.Empty;
                        key.Close();
                        return token;
                    }

                    return string.Empty;
                }
                // Non-Windows: read from file under Client user files
                var fi = SafePath.GetFile(TokenFilePath);
                if (fi.Exists)
                    return File.ReadAllText(fi.FullName).Trim();
                return string.Empty;
#endif
            }
            catch { return string.Empty; }
        }

        private void WriteAuthToken(string token)
        {
            try
            {
#if NETFRAMEWORK
                RegistryKey key = Registry.CurrentUser.CreateSubKey(tokenPath);
                key.SetValue("accessToken", token ?? string.Empty);
                key.Close();
#else
                if (OperatingSystem.IsWindows())
                {
                    RegistryKey key = Registry.CurrentUser.CreateSubKey(tokenPath);
                    key.SetValue("accessToken", token ?? string.Empty);
                    key.Close();
                }
                else
                {
                    // Ensure directory exists
                    DirectoryInfo dir = SafePath.GetDirectory(ProgramConstants.ClientUserFilesPath);
                    if (!dir.Exists) dir.Create();
                    File.WriteAllText(TokenFilePath, token ?? string.Empty);
                }
#endif
            }
            catch { }
        }

        private void ClearAuthToken()
        {
            try
            {
#if NETFRAMEWORK
                RegistryKey key = Registry.CurrentUser.CreateSubKey(tokenPath);
                key.SetValue("accessToken", "");
                key.Close();
#else
                if (OperatingSystem.IsWindows())
                {
                    RegistryKey key = Registry.CurrentUser.CreateSubKey(tokenPath);
                    key.SetValue("accessToken", "");
                    key.Close();
                }
                else
                {
                    var fi = SafePath.GetFile(TokenFilePath);
                    if (fi.Exists) fi.Delete();
                }
#endif
            }
            catch { }
        }

        /// <summary>
        /// Creates a new player nickname on the CnCNet ladder.
        /// Uses the ladder abbreviation from ClientConfiguration (defaults to "custom").
        /// </summary>
        public async Task<bool> CreatePlayerAsync(string username)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + API_PLAYER_CREATE);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["ladderAbbrev"] = ClientConfiguration.Instance.CnCNetLadderAbbrev
                });

                HttpResponseMessage response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(body))
                    {
                        ErrorMessage = body;
                        return false;
                    }

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.BadRequest:
                            ErrorMessage = "Failed to create nickname. It may already be taken or you have already created one this month.".L10N("Client:CnCNet:CreatePlayerBadRequest");
                            break;
                        case HttpStatusCode.Unauthorized:
                            ErrorMessage = "Your session has expired. Please log in again.".L10N("Client:CnCNet:CreatePlayerUnauthorized");
                            break;
                        default:
                            ErrorMessage = "An error occurred, status code: " + response.StatusCode;
                            break;
                    }

                    return false;
                }

                await ActivatePlayerAsync(username);

                return await GetAccountsAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                ErrorMessage = "Connection failed: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Activates a player nickname by toggling its status on the CnCNet ladder.
        /// This creates a PlayerActiveHandle so the nickname appears as active.
        /// </summary>
        private async Task ActivatePlayerAsync(string username)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + API_PLAYER_STATUS);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["ladderAbbrev"] = ClientConfiguration.Instance.CnCNetLadderAbbrev
                });

                await httpClient.SendAsync(request);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Logger.Log("Failed to activate player: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets nick names from their account (active for current month).
        /// </summary>
        public async Task<bool> GetAccountsAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ApiBaseUrl + API_USER_ACCOUNT);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);

                HttpResponseMessage response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return false;

                string json = await response.Content.ReadAsStringAsync();
                List<AuthPlayer>? accounts = JsonConvert.DeserializeObject<List<AuthPlayer>>(json);
                Accounts = accounts ?? new List<AuthPlayer>();

                AccountUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return false;
            }
        }

    }

    public class AuthTokenResponse
    {
        [JsonProperty("token")]
        public string? Token { get; set; }
    }

    public class AuthLadder
    {
        [JsonProperty("abbreviation")]
        public string? Abbreviation { get; set; }
    }

    public class AuthPlayer
    {
        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("ladder_id")]
        public int LadderId { get; set; }

        [JsonProperty("ladder")]
        public AuthLadder? Ladder { get; set; }
    }
}
