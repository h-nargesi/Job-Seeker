console.log("AGENT", "readiness");

const READINESS_POLL_MS = 400;
const READINESS_SETTLE_MS = 250;
const READINESS_DEFAULT_TIMEOUT_MS = 8000;

function CompileReadinessRules(rules) {
    const compiled = [];

    for (let r in rules || []) {
        const rule = rules[r];
        if (!rule || !rule.url || !rule.selectors) continue;

        try {
            compiled.push({ url: new RegExp(rule.url, 'i'), selectors: rule.selectors });
        } catch (e) {
            console.warn("AGENT", 'Readiness', "bad rule", rule.url, e);
        }
    }

    return compiled;
}

function RuleMatchesUrl(rules, href) {
    for (let r in rules) {
        if (rules[r].url.test(href)) return true;
    }

    return false;
}

function MatchedReadinessSelector(rules, href) {
    for (let r in rules) {
        const rule = rules[r];
        if (!rule.url.test(href)) continue;

        for (let s in rule.selectors) {
            try {
                if (document.querySelector(rule.selectors[s])) return rule.selectors[s];
            } catch (e) {
                console.warn("AGENT", 'Readiness', rule.selectors[s], e);
            }
        }
    }

    return null;
}

async function WaitForReadiness(scope) {
    const cooldown = CooldownRemaining();
    if (cooldown > 0) {
        console.error("AGENT", 'Readiness', "cooldown remaining", cooldown);
        await ActionHandler.OnWait({ miliseconds: cooldown });
    }

    const rules = CompileReadinessRules(scope.rules);
    const deadline = scope.waiting || READINESS_DEFAULT_TIMEOUT_MS;

    if (!RuleMatchesUrl(rules, window.location.href)) {
        console.log("AGENT", 'Readiness', "no matching rule - legacy wait", deadline);

        const challenge_kind = ChallengeDetector.Detect(document);
        await ActionHandler.OnWait({ miliseconds: deadline });
        return SendingPageInfo(scope, challenge_kind);
    }

    const started = Date.now();

    while (true) {
        const challenge_kind = ChallengeDetector.Detect(document);
        if (challenge_kind) {
            console.log("AGENT", 'Readiness', "challenge", challenge_kind);
            return SendingPageInfo(scope, challenge_kind);
        }

        const selector = MatchedReadinessSelector(rules, window.location.href);
        if (selector) {
            console.log("AGENT", 'Readiness', "marker", selector);
            await ActionHandler.OnWait({ miliseconds: READINESS_SETTLE_MS });
            return SendingPageInfo(scope, null);
        }

        if (Date.now() - started >= deadline) {
            console.log("AGENT", 'Readiness', "deadline reached - sending anyway");
            return SendingPageInfo(scope, null);
        }

        await ActionHandler.OnWait({ miliseconds: READINESS_POLL_MS });
    }
}
