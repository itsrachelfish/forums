﻿using Nancy;
using Quorum.Database.Postgres;
using Quorum.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quorum
{
    public class SessionModule : NancyModule
    {
        public SessionModule()
        {
            AuthenticationManager.EnableAuthentication(this); // TODO: Manage ISessionProviders separately

            Post("/login", _ => HandleLogin());

            Get("/logout", _ => HandleLogout());
            Post("/logout", _ => HandleLogout());

            Post("/register", _ => HandleRegistration());
        }

        public async Task<object> HandleRegistration()
        {
            string error_redirect = Request.Form["error_redirect"].Value ?? "/";
            string success_redirect = Request.Form["success_redirect"].Value ?? "/";

            string username = Request.Form["username"].Value ?? "";
            string password = Request.Form["password"].Value ?? "";
            string email = Request.Form["email"].Value ?? "";

            try
            {
                if (Context.Items.ContainsKey("session"))
                    return Response.AsRedirect(error_redirect);

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Response.AsRedirect(error_redirect);

                var user_provider = AuthenticationManager.GetProvider<IUserProvider>();
                var session_provider = AuthenticationManager.GetProvider<ISessionProvider>();
                var password_provider = AuthenticationManager.GetProvider<IPasswordLoginProvider>();
                var user_map_provider = AuthenticationManager.GetProvider<IUserMapProvider>();

                var user = user_provider.CreateUser(username);
                password_provider.CreateLogin(user, username, password, email);
                user_map_provider.CreateUserMap(user.Identifier, "local", username);

                var session = session_provider.CreateSession(password_provider.AttemptAuthenticate(username, password));
                return Response.AsRedirect(success_redirect).WithCookie("_quorum_auth", session.Id, session.ValidUntil);

            }
            catch (Exception ex)
            {
                return Response.AsRedirect(error_redirect);
            }
        }

        public async Task<object> HandleLogout()
        {
            string error_redirect = Request.Form["error_redirect"].Value ?? "/";
            string success_redirect = Request.Form["success_redirect"].Value ?? "/";

            try
            {
                if (Context.Items.ContainsKey("session"))
                {
                    var session = Context.Items["session"] as Session;

                    AuthenticationManager.GetProvider<ISessionProvider>().DestroySession(session);
                    return Response.AsRedirect(success_redirect).WithCookie("_quorum_auth", "", new DateTime(1970, 1, 1));
                }
            }
            catch (Exception ex)
            {
                return Response.AsRedirect(error_redirect);
            }

            return Response.AsRedirect(error_redirect);
        }

        public async Task<object> HandleLogin()
        {
            string error_redirect = Request.Form["error_redirect"].Value ?? "/";
            string success_redirect = Request.Form["success_redirect"].Value ?? "/";

            string username = Request.Form["username"].Value ?? "";
            string password = Request.Form["password"].Value ?? "";

            try
            {
                var session_provider = AuthenticationManager.GetProvider<ISessionProvider>();

                if(Context.Items.ContainsKey("session"))
                {
                    session_provider.DestroySession(Context.Items["session"] as Session);
                }

                var password_provider = AuthenticationManager.GetProvider<IPasswordLoginProvider>();

                var user = password_provider.AttemptAuthenticate(username, password);

                if (user == null)
                    throw new Exception("Failed to authenticate.");

                var session = session_provider.CreateSession(user);
                return Response.AsRedirect(success_redirect).WithCookie("_quorum_auth", session.Id, session.ValidUntil);

            }
            catch (Exception ex)
            {
                return Response.AsRedirect(error_redirect + "?error=" + ex.Message);
            }
        }
    }
}
