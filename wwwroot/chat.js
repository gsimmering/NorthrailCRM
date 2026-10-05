window.northrailChat = (() => {
    const enterHandlers = new WeakMap();
    const contactNavigationHandlers = new WeakMap();

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
        attachContactNavigation(element) {
            const handler = event => {
                if (event.key !== "ArrowDown" && event.key !== "ArrowUp") {
                    return;
                }

                const option = event.target.closest('[role="option"]');
                if (!option || !element.contains(option)) {
                    return;
                }

                event.preventDefault();
                const options = Array.from(element.querySelectorAll('[role="option"]'));
                const currentIndex = options.indexOf(option);
                const nextIndex = Math.max(0, Math.min(options.length - 1, currentIndex + (event.key === "ArrowDown" ? 1 : -1)));
                const nextOption = options[nextIndex];
                if (nextOption && nextOption !== option) {
                    nextOption.focus();
                    nextOption.click();
                }
            };

            contactNavigationHandlers.set(element, handler);
            element.addEventListener("keydown", handler);
        },
        detachContactNavigation(element) {
            const handler = contactNavigationHandlers.get(element);
            if (handler) {
                element.removeEventListener("keydown", handler);
                contactNavigationHandlers.delete(element);
            }
        },
        scrollToBottom(element) {
            if (element) {
                element.scrollTop = element.scrollHeight;
            }
        },
        async getCurrentUserName() {
            try {
                const response = await fetch("/api/current-user", { credentials: "same-origin" });
                if (!response.ok) {
                    return null;
                }

                const identity = await response.json();
                return typeof identity?.name === "string" ? identity.name : null;
            } catch {
            }

            return null;
        }
    };
})();