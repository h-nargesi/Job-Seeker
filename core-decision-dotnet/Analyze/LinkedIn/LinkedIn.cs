namespace Photon.JobSeeker.LinkedIn
{
    class LinkedIn : Agency
    {
        public override string Name => "LinkedIn";

        protected override IEnumerable<Type> GetSubPages()
        {
            return TypeHelper.GetSubTypes(typeof(LinkedInPage));
        }
    }
}