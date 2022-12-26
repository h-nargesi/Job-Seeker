namespace Photon.JobSeeker
{
    public abstract class Page : IComparable<Page>
    {
        public Page(Agency parent)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        protected static long MinEligibilityScore = 100;

        public Agency Parent { get; }

        public abstract int Order { get; }

        public abstract TrendType TrendType { get; }

        public abstract Command[]? IssueCommand(string url, string content);

        public (string user, string pass) GetUserPass()
        {
            using var database = Database.Open();
            return database.Agency.GetUserPass(Parent.Name);
        }

        public int CompareTo(Page? other)
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
    }
}