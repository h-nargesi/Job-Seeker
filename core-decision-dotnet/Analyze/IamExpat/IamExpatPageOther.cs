﻿namespace Photon.JobSeeker.IamExpat
{
    class IamExpatPageOther : IamExpatPage
    {
        public override int Order => 100;

        public override TrendType TrendType => TrendType.Searching;

        public IamExpatPageOther(IamExpat parent) : base(parent) { }

        public override Command[]? IssueCommand(string url, string content)
        {
            return new Command[] { Command.Go("https://iamexpat.nl/career/jobs-netherlands") };
        }
    }
}