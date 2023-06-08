using Photon.JobSeeker.Pages;

namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageOther : OtherPages, IamExpatPageInterface
    {
        public override int Order => 100;

        public override TrendState TrendState => TrendState.Other;

        public IamExpatPageOther(IamExpat parent) : base(parent) { }
    }
}