﻿using System.Text.RegularExpressions;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageLogin : IamExpatPage
    {
        public override int Order => 1;

        public override TrendType TrendType => TrendType.None;

        private static readonly Regex reg_login_url = new(@"iamexpat\.com/login");

        public IamExpatPageLogin(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            if (!reg_login_url.IsMatch(url)) return null;

            var (user, pass) = GetUserPass();

            return new Command[] {
            Command.Fill(@"input[id=""edit-name""]", user),
            Command.Fill(@"input[id=""edit-pass""]", pass),
            Command.Click(@"input[id=""edit-submit""]")
        };
        }
    }
}