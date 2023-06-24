/**
 * Simulate a key event.
 * @param {Number} keyCode The keyCode of the key to simulate
 * @param {String} type (optional) The type of event : down, up or press. The default is down
 * @param {Object} modifiers (optional) An object which contains modifiers keys { ctrlKey: true, altKey: false, ...}
 */
function simulateKey(element, keyCode, type, modifiers) {
    const evtName = (typeof (type) === "string") ? "key" + type : "keydown";
    const modifier = (typeof (modifiers) === "object") ? modifier : {};

    let event = document.createEvent("HTMLEvents");
    event.initEvent(evtName, true, false);
    event.keyCode = keyCode;

    for (const i in modifiers) {
        event[i] = modifiers[i];
    }

    document.dispatchEvent(event);
}

// Using the function
// simulateKey(72, "H");
// simulateKey(65, "A");