window.northrailChat = (() => {
    const enterHandlers = new WeakMap();

    return {
        attachEnterToSend(element, dotNetReference) {
            const handler = event => {
                if (event.key !== "Enter" || event.isComposing || event.keyCode === 229) {
                    return;
                }

                event.preventDefault();
                if (event.shiftKey) {
                    const start = element.selectionStart;
                    const end = element.selectionEnd;
                    element.setRangeText("\n", start, end, "end");
                    element.dispatchEvent(new InputEvent("input", {
                        bubbles: true,
                        inputType: "insertLineBreak",
                        data: null
                    }));
                    return;
                }

                dotNetReference.invokeMethodAsync("SendFromKeyboardAsync").catch(() => {});
            };

            enterHandlers.set(element, handler);
            element.addEventListener("keydown", handler);
        },
        detachEnterToSend(element) {
            const handler = enterHandlers.get(element);
            if (handler) {
                element.removeEventListener("keydown", handler);
                enterHandlers.delete(element);
            }
        },
        scrollToBottom(element) {
            if (element) {
                element.scrollTop = element.scrollHeight;
            }
        }
    };
})();