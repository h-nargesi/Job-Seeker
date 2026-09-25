console.log("AGENT", "challenge-detector");

class ChallengeDetector {

    static HTTP_403 = "http-403";
    static HTTP_403_PATTERN = /\b403\b|access denied|forbidden|zugriff verweigert/i;
    static HTTP_403_BODY_LIMIT = 500;

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

        return ChallengeDetector.DetectHttp403(doc);
    }

    static DetectHttp403(doc) {
        try {
            if (ChallengeDetector.HTTP_403_PATTERN.test(doc.title || "")) return ChallengeDetector.HTTP_403;

            const heading = doc.querySelector("h1");
            if (heading && ChallengeDetector.HTTP_403_PATTERN.test(heading.textContent || "")) {
                const body = doc.body ? (doc.body.innerText || doc.body.textContent || "") : "";
                if (body.length < ChallengeDetector.HTTP_403_BODY_LIMIT) return ChallengeDetector.HTTP_403;
            }
        } catch (e) {
            console.warn("AGENT", "ChallengeDetector", "http-403", e);
        }

        return null;
    }
}
