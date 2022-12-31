﻿namespace Photon.JobSeeker.LinkedIn
{
    class LinkedInPageLogin : LinkedInPage
    {
        public override int Order => 1;

        public override TrendType TrendType => TrendType.Searching;

        public LinkedInPageLogin(LinkedIn parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_butl.IsMatch(content)) return null;

            var (user, pass) = GetUserPass();

            return new Command[] {
                Command.Fill(@"input[id=""session_key""]", user),
                Command.Fill(@"input[id=""session_password""]", pass),
                Command.Click(@"button[type=""submit""]")
            };
        }
    }
}