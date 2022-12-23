console.log("action-handler");

class ActionHandler {

    static async Handle(commands) {
        for (let c in commands) {
            await ActionHandler.Execute(commands[c]);
        }
    }

    static async Execute(command) {
        console.log(command);
        switch (command.action.toLowerCase()) {
            case "go":
                ActionHandler.Go(command.params);
                break;
            case "open":
                ActionHandler.Open(command.params);
                break;
            case "fill":
                ActionHandler.Fill(command.params, command.object);
                break;
            case "click":
                ActionHandler.Click(command.object);
                break;
            case "close":
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
        let elements = document.querySelectorAll(object);
        if (!elements) console.warn("Not found", object);
        elements.forEach(element => element.click());
    }

    static Close() {
        window.close();
    }

    static async Wait(params) {
        await new Promise(r => setTimeout(r, params.miliseconds));
    }
}