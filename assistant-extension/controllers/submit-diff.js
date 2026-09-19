console.log("ASSISTANT", "submit-diff");

class SubmitDiff {

    static Listening = false;

    static Listen() {
        if (SubmitDiff.Listening) return;
        SubmitDiff.Listening = true;

        document.addEventListener("submit", function () {
            SubmitDiff.Capture();
        }, true);
    }

    static async Capture() {
        try {
            const diffs = FillHandler.Diffs();
            if (!diffs.length) return;

            const queued = (await StorageHandler.GetSession(StorageHandler.PENDING_DIFFS, [])) || [];
            for (const diff of diffs) {
                diff.domain = location.hostname;
                queued.push(diff);
            }
            await StorageHandler.SetSession(StorageHandler.PENDING_DIFFS, queued);

            FillHandler.Reset();
            BackgroundMessaging.Message("flush-diffs");
        } catch (e) {
            console.error("ASSISTANT", "SubmitDiff", e);
        }
    }
}
