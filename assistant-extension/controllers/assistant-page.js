console.log("ASSISTANT", "assistant-page");

class AssistantPage {

    static MODE_JOB_DETAIL = "job_detail";
    static MODE_APPLY_FORM = "apply_form";

    static async PageState() {
        const core_origin = await CoreOrigin();

        return {
            url: location.href,
            origin: location.origin,
            domain: location.hostname,
            mode: AssistantPage.DefaultMode(core_origin),
            jobId: AssistantPage.JobId(),
        };
    }

    static DefaultMode(core_origin) {
        return location.origin === core_origin
            ? AssistantPage.MODE_JOB_DETAIL
            : AssistantPage.MODE_APPLY_FORM;
    }

    static JobId() {
        const from_query = new URLSearchParams(location.search).get("jobid");
        if (from_query && /^\d+$/.test(from_query)) return Number(from_query);

        const match = location.pathname.match(/\/job\/get\/(\d+)/);
        if (match) return Number(match[1]);

        return null;
    }

    static async Handle(request) {
        switch (request.title) {
            case "page-state":
                return AssistantPage.PageState();
            case "inventory":
                return {
                    inventory: FormInventory.Extract(),
                    domain: location.hostname,
                    mode: AssistantPage.DefaultMode(await CoreOrigin()),
                    jobId: AssistantPage.JobId(),
                };
            case "reset-fill":
                FillHandler.Reset();
                return { ok: true };
            default:
                return { error: "unknown-title" };
        }
    }
}

async function CoreOrigin() {
    try {
        return new URL(await StorageHandler.ServerUrlAsync()).origin;
    } catch (e) {
        return "";
    }
}

chrome.runtime.onMessage.addListener(function (request, sender, sendResponse) {
    if (!request || !request.title) return;

    if (request.title === "apply-fill") {
        const result = FillHandler.Apply(request.params?.fieldId, request.params?.value);
        sendResponse(result);
        return;
    }

    AssistantPage.Handle(request).then(sendResponse);
    return true;
});

SubmitDiff.Listen();
