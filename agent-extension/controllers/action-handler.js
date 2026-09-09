console.log("AGENT", "action-handler");

class ActionHandler {

    static CloseTimer = null;
    static OnPageLoad = null;

    static async Handle(commands, dontclose) {
        if (!commands) {
            console.error("AGENT", 'ActionHandler', 'no commands', commands);
            return;
        }

        let command_count = 0;
        for (let c in commands)
            if (commands[c]) {
                await ActionHandler.Execute(commands[c], dontclose);
                command_count++;
            }
        console.log("AGENT", 'Action Count', command_count);
    }

    static async Execute(command, dontclose) {
        console.log("AGENT", "Action", command);
        switch (command?.action?.toLowerCase()) {
            case "go":
                ActionHandler.OnGo(command.params);
                break;
            case "open":
                await BackgroundMessaging.OpenTab(command.params.url);
                break;
            case "fill":
                ActionHandler.OnFill(command.object, command.params);
                ActionHandler.OnWait({ miliseconds: 300 });
                break;
            case "click":
                ActionHandler.OnClick(command.object);
                ActionHandler.OnWait({ miliseconds: 300 });
                break;
            case "recheck":
                if (!ActionHandler.OnPageLoad)
                    console.warn("The OnPageLoad event is not set!");
                ActionHandler.OnPageLoad();
                break;
            case "close":
                if (!dontclose)
                    await BackgroundMessaging.CloseTab();
                break;
            case "reload":
                ActionHandler.OnReload();
                break;
            case "wait":
                await ActionHandler.OnWait(command.params);
                break;
            default:
                console.error("Unkown action", command.action);
                break;
        }
    }

    static OnGo(params) {
        window.location = params.url;
    }

    static OnFill(object, params) {
        let elements = document.querySelectorAll(object);
        if (!elements) console.warn("Not found", object);
        elements.forEach(element => {
            if ('value' in element) element.value = params.value;
            else element.innerText = params.value;
        });
    }

    static OnClick(object) {
        let elements = document.querySelectorAll(object);
        if (!elements) console.warn("Not found", object);
        elements.forEach(element => {
            if (element) element.click()
        });
    }

    static OnReload() {
        location.reload();
    }

    static async OnWait(params) {
        await new Promise(r => setTimeout(r, params.miliseconds));
    }

    static SetCloseTimer(ms = 90000) {
        if (ActionHandler.CloseTimer)
            clearTimeout(ActionHandler.CloseTimer);
        ActionHandler.CloseTimer = setTimeout(BackgroundMessaging.CloseTab, ms);
    }
}