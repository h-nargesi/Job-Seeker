namespace Photon.JobSeeker
{
    partial class JobBusiness
    {
        public bool AcceptAiOptions(long jobId)
        {
            var job = Fetch(jobId);
            var selection = job?.AiOptions;
            if (job == null || selection == null) return false;

            selection.HumanEdited = true;
            database.Execute(Q_CHANGE_OPTIONS, new { options = selection, now = DateTime.Now, jobId });
            return true;
        }

        public bool AcceptTextSlot(long jobId, string slot)
        {
            var job = Fetch(jobId);
            var text = job?.ResumeText;
            if (job == null || text == null || !text.TryGetValue(slot, out var value)) return false;
            if (string.IsNullOrEmpty(value.Proposal)) return false;

            value.Live = value.Proposal;
            value.Proposal = null;
            value.Status = ResumeTextSlotStatus.Accepted;
            SaveResumeText(jobId, text);
            return true;
        }

        public bool RejectTextSlot(long jobId, string slot)
        {
            var job = Fetch(jobId);
            var text = job?.ResumeText;
            if (job == null || text == null || !text.TryGetValue(slot, out var value)) return false;

            value.Status = ResumeTextSlotStatus.Rejected;
            SaveResumeText(jobId, text);
            return true;
        }

        public bool WriteLiveText(long jobId, string slot, string? value)
        {
            var job = Fetch(jobId);
            if (job == null) return false;

            var known = slot == ResumeInventory.TitleSlot
                || slot == ResumeInventory.SummarySlot
                || job.ResumeText?.ContainsKey(slot) == true;
            if (!known) return false;

            var text = job.ResumeText ?? new ResumeText();
            text.TryGetValue(slot, out var existing);
            var live = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            if (live == null && existing?.Proposal == null)
            {
                text.Remove(slot);
            }
            else
            {
                text[slot] = new ResumeTextSlot
                {
                    Live = live,
                    Proposal = existing?.Proposal,
                    Status = ResumeTextSlotStatus.Accepted,
                };
            }

            SaveResumeText(jobId, text);
            return true;
        }

        private void SaveResumeText(long jobId, ResumeText text)
        {
            database.Execute(Q_SAVE_RESUME_TEXT, new { resumeText = text, now = DateTime.Now, jobId });
        }
    }
}
