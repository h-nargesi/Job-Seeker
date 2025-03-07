﻿using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.Bayt;

class BaytPageAuth(Bayt parent) : AuthPage(parent), BaytPage
{
    protected override bool CheckInvalidUrl(string url, string content)
    {
        return !BaytPage.reg_login_but.IsMatch(content);
    }

    protected override Command[] LoginUrl()
    {
        return new Command[] { Command.Click(@"a[href^=""https://www.bayt.com/en/login/""]") };
    }
}
