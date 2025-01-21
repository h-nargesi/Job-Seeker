namespace Photon.JobSeeker.Pages;

public abstract class PageBase : IComparable<PageBase>
{
    protected PageBase(Agency parent)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    public Agency Parent { get; }

    public abstract int Order { get; }

    public abstract TrendState TrendState { get; }

    public abstract Command[]? IssueCommand(string url, string content);

    public int CompareTo(PageBase? other)
    {
        if (other == null) return 1;
        else if (other == this) return 0;
        else if (other.Order > Order) return -1;
        else if (other.Order < Order) return 1;
        else return 0;
    }

    public override string ToString()
    {
        return $"{GetType().Name} ({Order})";
    }

    protected (string user, string pass) GetUserPass()
    {
        using var database = Database.Open();
        return AgencyBusiness.GetUserPass(Parent.Name);
    }

    protected abstract bool CheckInvalidUrl(string url, string content, out Command[]? commands);
}