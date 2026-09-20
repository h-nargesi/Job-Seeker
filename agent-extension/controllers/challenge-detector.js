console.log("AGENT", "challenge-detector");

class ChallengeDetector {

    static SELECTORS = [
        ["cf-interstitial", [
            "#challenge-form",
            "#challenge-running",
            "#challenge-stage",
            "#challenge-error-text",
            "#cf-please-wait",
            "script[src*='/cdn-cgi/challenge-platform']",
            "iframe[src*='cdn-cgi']",
        ]],
        ["cf-turnstile", [
            ".cf-turnstile:not([data-size='invisible'])",
            "iframe[src*='challenges.cloudflare.com']",
        ]],
        ["recaptcha", [
            ".g-recaptcha:not([data-size='invisible'])",
        ]],
        ["hcaptcha", [
            ".h-captcha:not([data-size='invisible'])",
        ]],
        ["captcha", [
            "iframe[title*='captcha']",
            "iframe[title*='Captcha']",
        ]],
    ];

    static Detect(doc) {
        for (let g in ChallengeDetector.SELECTORS) {
            const [kind, selectors] = ChallengeDetector.SELECTORS[g];

            for (let s in selectors) {
                try {
                    if (doc.querySelector(selectors[s])) return kind;
                } catch (e) {
                    console.warn("AGENT", "ChallengeDetector", selectors[s], e);
                }
            }
        }

        return null;
    }
}
