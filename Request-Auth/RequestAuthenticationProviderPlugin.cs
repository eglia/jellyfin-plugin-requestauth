using System;
using System.Net;
using System.Linq;
using System.Text;
using MediaBrowser.Common;
using MediaBrowser.Common.Cryptography;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Request_Auth
{
    public class RequestAuthenticationProviderPlugin : IAuthenticationProvider, IRequiresResolvedUser
    {
        private readonly PluginConfiguration _config;
        private readonly ILogger _logger;
        private readonly ICryptoProvider _cryptographyProvider;
        public RequestAuthenticationProviderPlugin(ICryptoProvider cryptographyProvider)
        {
            _config = Plugin.Instance.Configuration;
            _logger = Plugin.Logger;
            _cryptographyProvider = cryptographyProvider;
        }

        public string Name => "Request-Authentication";

        public bool IsEnabled => true;

        /// <inheritdoc />
        // This is dumb and an artifact of the backwards way auth providers were designed.
        // This version of authenticate was never meant to be called, but needs to be here for interface compat
        // Only the providers that don't provide local user support use this
        public Task<ProviderAuthenticationResult> Authenticate(string username, string password)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        // This is the version that we need to use for local users. Because reasons.
        public async Task<ProviderAuthenticationResult> Authenticate(string username, string password, User resolvedUser)
        {
            if (resolvedUser == null)
            {
                throw new ArgumentNullException(nameof(resolvedUser));
            }

            bool success = false;

            // As long as jellyfin supports passwordless users, we need this little block here to accommodate
            if (!HasPassword(resolvedUser) && string.IsNullOrEmpty(password))
            {
                return new ProviderAuthenticationResult { Username = username };
            }

            try
            {
                HttpWebRequest r = (HttpWebRequest)WebRequest.Create(_config.RequestURL);
                string cookieName = password.Split('=')[0];
                string cookieToken = password.Split('=')[1];
                Uri uri = new Uri(_config.RequestURL);
                Cookie cookie = new Cookie(cookieName, cookieToken, "/", uri.Host);
                r.CookieContainer = new CookieContainer();
                r.CookieContainer.Add(cookie);

                HttpWebResponse response = (HttpWebResponse)await r.GetResponseAsync();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    String[] requestUser = response.Headers.GetValues("x-organizr-user");
                    if (requestUser[0].ToLower() == username.ToLower())
                    {
                        return new ProviderAuthenticationResult { Username = username };
                    }
                }
            } catch { }

            // If we reach here, request authentication has failed. Continue with default authentication.

            byte[] passwordbytes = Encoding.UTF8.GetBytes(password);

            PasswordHash readyHash = PasswordHash.Parse(resolvedUser.Password);
            if (_cryptographyProvider.GetSupportedHashMethods().Contains(readyHash.Id)
                || _cryptographyProvider.DefaultHashMethod == readyHash.Id)
            {
                byte[] calculatedHash = _cryptographyProvider.ComputeHash(
                    readyHash.Id,
                    passwordbytes,
                    readyHash.Salt.ToArray());

                if (readyHash.Hash.SequenceEqual(calculatedHash))
                {
                    success = true;
                }
            }
            else
            {
                throw new AuthenticationException($"Requested crypto method not available in provider: {readyHash.Id}");
            }

            if (!success)
            {
                throw new AuthenticationException("Invalid username or password");
            }

            return new ProviderAuthenticationResult { Username = username };
        }


        /// <inheritdoc />
        public bool HasPassword(User user)
            => !string.IsNullOrEmpty(user.Password);

        /// <inheritdoc />
        public Task ChangePassword(User user, string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
            {
                user.Password = null;
                return Task.CompletedTask;
            }

            PasswordHash newPasswordHash = _cryptographyProvider.CreatePasswordHash(newPassword);
            user.Password = newPasswordHash.ToString();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void ChangeEasyPassword(User user, string newPassword, string newPasswordHash)
        {
            if (newPassword != null)
            {
                newPasswordHash = _cryptographyProvider.CreatePasswordHash(newPassword).ToString();
            }

            if (string.IsNullOrWhiteSpace(newPasswordHash))
            {
                throw new ArgumentNullException(nameof(newPasswordHash));
            }

            user.EasyPassword = newPasswordHash;
        }

        /// <inheritdoc />
        public string GetEasyPasswordHash(User user)
        {
            return string.IsNullOrEmpty(user.EasyPassword)
                ? null
                : Hex.Encode(PasswordHash.Parse(user.EasyPassword).Hash);
        }

        /// <summary>
        /// Gets the hashed string.
        /// </summary>
        public string GetHashedString(User user, string str)
        {
            if (string.IsNullOrEmpty(user.Password))
            {
                return _cryptographyProvider.CreatePasswordHash(str).ToString();
            }

            // TODO: make use of iterations parameter?
            PasswordHash passwordHash = PasswordHash.Parse(user.Password);
            var salt = passwordHash.Salt.ToArray();
            return new PasswordHash(
                passwordHash.Id,
                _cryptographyProvider.ComputeHash(
                    passwordHash.Id,
                    Encoding.UTF8.GetBytes(str),
                    salt),
                salt,
                passwordHash.Parameters.ToDictionary(x => x.Key, y => y.Value)).ToString();
        }

        public ReadOnlySpan<byte> GetHashed(User user, string str)
        {
            if (string.IsNullOrEmpty(user.Password))
            {
                return _cryptographyProvider.CreatePasswordHash(str).Hash;
            }

            // TODO: make use of iterations parameter?
            PasswordHash passwordHash = PasswordHash.Parse(user.Password);
            return _cryptographyProvider.ComputeHash(
                    passwordHash.Id,
                    Encoding.UTF8.GetBytes(str),
                    passwordHash.Salt.ToArray());
        }
    }
}
