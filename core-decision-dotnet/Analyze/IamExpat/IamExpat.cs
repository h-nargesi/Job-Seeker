class IamExpat : Agency
{
    public override string Name => "IamExpat";

    protected override IEnumerable<Type> GetSubPages()
    {
        return TypeHelper.GetSubTypes(typeof(IamExpatPage));
    }
}