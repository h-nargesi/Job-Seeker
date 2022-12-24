using System.Text.RegularExpressions;

class IamExpatPageLogin : IamExpatPage
{
    public override int Order => 1;

    private static readonly Regex reg_login_url = new(@"iamexpat\.com/login");

    public IamExpatPageLogin(Agency parent) : base(parent)
    {
    }

    public override Command[]? IssueCommand(string url, string content)
    {
        if (reg_login_url.IsMatch(content)) return null;

        var (user, pass) = GetUserPass();

        return new Command[] {
            Command.Fill(@"input[id=""edit-name""]", user),
            Command.Fill(@"input[id=""edit-pass""]", pass),
            Command.Click(@"input[id=""edit-submit""]")
        };
    }

}