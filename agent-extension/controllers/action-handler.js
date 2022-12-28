console.log("action-handler");

class ActionHandler {

    static async Handle(commands, dontclose) {
        for (let c in commands) {
            await ActionHandler.Execute(commands[c], dontclose);
        }
    }

    static async Execute(command, dontclose) {
        console.log("Action", command);
        switch (command?.action?.toLowerCase()) {
            case "go":
                ActionHandler.Go(command.params);
                break;
            case "open":
                ActionHandler.Open(command.params);
                break;
            case "fill":
                ActionHandler.Fill(command.params, command.object);
                ActionHandler.Wait({ miliseconds: 300 });
                break;
            case "click":
                ActionHandler.Click(command.object);
                ActionHandler.Wait({ miliseconds: 300 });
                break;
            case "close":
                if (!dontclose)
                    ActionHandler.Close();
                break;
            case "wait":
                await ActionHandler.Wait(command.params);
                break;
            default:
                console.error("Unkown action", command.action);
                break;
        }
    }

    static Go(params) {
        window.location = params.url;
    }

    static Open(params) {
        window.open(params.url);
    }

    static Fill(params, object) {
        let elements = document.querySelectorAll(object);
        if (!elements) console.warn("Not found", object);
        elements.forEach(element => {
            if ('value' in element) element.value = params.value;
            else element.innerText = params.value;
        });
    }

    static Click(object) {
        const info = GetElement(object);
        if (!info.elements) console.warn("Not found", object);
        info.elements.forEach(element => {
            switch (info.extra) {
                case "next":
                    element = element.nextSibling;
                    break;
            }
            if (!element) element.click();
        });
    }

    static Close() {
        window.open('', '_self', '');
        window.close();
    }

    static async Wait(params) {
        await new Promise(r => setTimeout(r, params.miliseconds));
    }

    static GetElement(object) {
        if (object.includes("@")) {
            const parts = object.split("@");
            return {
                extra: parts[1],
                elements: document.querySelectorAll(parts[0])
            };

        } else return {
            extra: null,
            elements: document.querySelectorAll(object)
        };
    }
}