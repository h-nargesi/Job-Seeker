using System.Text.RegularExpressions;

namespace Photon.JobSeeker.Pages
{
    public abstract class PageBase : IComparable<PageBase>
    {
        protected PageBase(Agency parent, IPageHandler hanlder)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Handler = hanlder ?? throw new ArgumentNullException(nameof(hanlder));
        }

        public Agency Parent { get; }

        public abstract int Order { get; }

        public abstract TrendState TrendState { get; }

        protected IPageHandler Handler { get; }

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

        protected interface IPageHandler
        {
            bool CheckUrl(string text, out Command[] commands);

            string GetJobCode(Match match);
        }
    }
}