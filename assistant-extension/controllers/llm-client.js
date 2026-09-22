console.log("ASSISTANT", "llm-client");

class LlmClient {

    static REQUEST_TIMEOUT = 180000;
    static MAX_TOKENS = 8192;

    constructor(url, model) {
        this.url = url;
        this.model = model;
    }

    static async Create() {
        return new LlmClient(await StorageHandler.LlamaUrlAsync(), await StorageHandler.LlamaModelAsync());
    }

    async Chat(messages, tools, options) {
        const controller = new AbortController();
        const timer = setTimeout(function () { controller.abort(); }, LlmClient.REQUEST_TIMEOUT);

        try {
            const response = await fetch(this.url + "v1/chat/completions", {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                signal: controller.signal,
                body: JSON.stringify({
                    model: this.model,
                    messages: messages,
                    tools: tools,
                    temperature: 0.2,
                    stream: false,
                    max_tokens: (options && options.max_tokens) || LlmClient.MAX_TOKENS,
                })
            });

            const body = await response.text();

            if (!response.ok) {
                console.error("ASSISTANT", "LlmClient", "http", response.status, body.slice(0, 200));
                return { error: "llm-http-" + response.status };
            }

            return LlmClient.Parse(body);
        } catch (e) {
            const error = e && e.name === 'AbortError' ? "llm-timeout" : "llm-unreachable";
            console.error("ASSISTANT", "LlmClient", error, e);
            return { error: error };
        } finally {
            clearTimeout(timer);
        }
    }

    static Parse(raw) {
        let parsed;
        try {
            parsed = JSON.parse(raw);
        } catch (e) {
            return { error: "llm-invalid-json" };
        }

        const message = parsed && parsed.choices && parsed.choices[0] && parsed.choices[0].message;
        if (!message) return { error: "llm-no-choice" };

        return {
            content: message.content ?? "",
            tool_calls: (message.tool_calls || []).map(function (call) {
                let args = {};
                try {
                    args = JSON.parse(call.function?.arguments || "{}");
                } catch (e) {
                    args = { __parse_error: true };
                }
                return { id: call.id, name: call.function?.name, args: args };
            })
        };
    }
}
