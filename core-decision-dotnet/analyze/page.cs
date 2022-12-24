using System;
using System.Collections.Generic;

abstract class Page
{
    public Page(Agency agency)
    {
        Agency = agency ?? throw new ArgumentNullException(nameof(agency));
    }

    public abstract int Order { get; }

    public Agency Agency { get; }

    public abstract List<string> IssueCommand(string url, string content);

    public (string user, string pass) GetUserPass()
    {
        return default;
    }
}