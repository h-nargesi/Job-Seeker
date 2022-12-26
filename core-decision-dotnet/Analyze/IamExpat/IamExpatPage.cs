namespace Photon.JobSeeker.IamExpat
{
    abstract class IamExpatPage : Page
    {
        protected readonly IamExpat parent;

        protected IamExpatPage(IamExpat parent) : base(parent) => this.parent = parent;
    }
}