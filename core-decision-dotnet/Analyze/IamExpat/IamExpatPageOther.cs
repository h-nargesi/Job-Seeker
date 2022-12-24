class IamExpatPageOther : IamExpatPage
{
    public override int Order => 100;

    public IamExpatPageOther(Agency parent) : base(parent) { }

    public override Command[]? IssueCommand(string url, string content)
    {
        return new Command[] { Command.Go("https://iamexpat.nl/career/jobs-netherlands") };
    }
}
