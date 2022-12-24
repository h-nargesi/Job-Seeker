class IamExpat : Agency
{
    protected IamExpat(ILogger<Agency> logger) : base(logger) { }

    public override string Name => "IamExpat";

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IamExpatPage));
    }
}