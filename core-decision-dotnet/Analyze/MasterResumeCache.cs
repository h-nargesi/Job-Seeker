namespace Photon.JobSeeker;

public sealed class MasterResumeCache(IViewRenderService views)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? text;

    public async Task<string> GetAsync(HttpContext http)
    {
        if (text != null) return text;

        await gate.WaitAsync();
        try
        {
            if (text != null) return text;
            var html = await views.RenderToStringAsync(http, "~/views/resume.cshtml",
                new ResumePage { Context = ResumeHtml.MasterContext() });
            text = ResumeHtml.PruneStripCap(html);
            return text;
        }
        finally
        {
            gate.Release();
        }
    }
}
