/**
 * Simulate a key event.
 * @param {Number} keyCode The keyCode of the key to simulate
 * @param {String} type (optional) The type of event : down, up or press. The default is down
 * @param {Object} modifiers (optional) An object which contains modifiers keys { ctrlKey: true, altKey: false, ...}
 */
function simulateKey(element, keyCode, type, modifiers) {
    var evtName = (typeof (type) === "string") ? "key" + type : "keydown";
    var modifier = (typeof (modifiers) === "object") ? modifier : {};

    var event = document.createEvent("HTMLEvents");
    event.initEvent(evtName, true, false);
    event.keyCode = keyCode;

    for (var i in modifiers) {
        event[i] = modifiers[i];
    }

    document.dispatchEvent(event);
}

// Using the function
simulateKey(72, "H");
simulateKey(65, "A");