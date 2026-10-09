window.northrailChat = (() => {
    const enterHandlers = new WeakMap();
    const contactNavigationHandlers = new WeakMap();
    const contactPanelSplitterHandlers = new WeakMap();

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
                    const scrollContainer = element.closest(".crm-contact-list");
                    const stickyHeader = scrollContainer?.querySelector(".sticky");
                    const containerRect = scrollContainer?.getBoundingClientRect();
                    const stickyHeaderRect = stickyHeader?.getBoundingClientRect();
                    const visibleTop = Math.max(containerRect?.top ?? 0, stickyHeaderRect?.bottom ?? 0);
                    const optionTop = nextOption.getBoundingClientRect().top;
                    if (scrollContainer && optionTop < visibleTop) {
                        scrollContainer.scrollTop -= visibleTop - optionTop;
                    } else {
                        nextOption.scrollIntoView({ block: "nearest" });
                    }
                    nextOption.click();
                }
            };

            contactNavigationHandlers.set(element, handler);
            element.addEventListener("keydown", handler);
        },
        focusFirstContact(element) {
            const firstOption = element.querySelector('[role="option"]');
            if (!firstOption) {
                return;
            }

            firstOption.focus();
            firstOption.click();
            firstOption.scrollIntoView({ block: "nearest" });
        },
        detachContactNavigation(element) {
            const handler = contactNavigationHandlers.get(element);
            if (handler) {
                element.removeEventListener("keydown", handler);
                contactNavigationHandlers.delete(element);
            }
        },
        attachContactPanelSplitter(host) {
            const splitter = host.querySelector("[data-contact-panel-splitter]");
            const panel = host.querySelector("[data-contact-panel]");
            if (!splitter || !panel) {
                return;
            }

            const setPanelWidth = width => {
                const boundedWidth = Math.max(260, Math.min(520, width));
                panel.style.width = `${boundedWidth}px`;
                splitter.setAttribute("aria-valuenow", String(Math.round(boundedWidth)));
            };
            const handlers = {
                pointerDown(event) {
                    if (event.button !== 0) {
                        return;
                    }

                    event.preventDefault();
                    handlers.startX = event.clientX;
                    handlers.startWidth = panel.getBoundingClientRect().width;
                    splitter.setPointerCapture(event.pointerId);
                },
                pointerMove(event) {
                    if (!splitter.hasPointerCapture(event.pointerId)) {
                        return;
                    }

                    setPanelWidth(handlers.startWidth + handlers.startX - event.clientX);
                },
                pointerUp(event) {
                    if (splitter.hasPointerCapture(event.pointerId)) {
                        splitter.releasePointerCapture(event.pointerId);
                    }
                },
                keyDown(event) {
                    if (event.key === "ArrowLeft") {
                        event.preventDefault();
                        setPanelWidth(panel.getBoundingClientRect().width + 24);
                    } else if (event.key === "ArrowRight") {
                        event.preventDefault();
                        setPanelWidth(panel.getBoundingClientRect().width - 24);
                    } else if (event.key === "Home") {
                        event.preventDefault();
                        setPanelWidth(260);
                    } else if (event.key === "End") {
                        event.preventDefault();
                        setPanelWidth(520);
                    }
                }
            };

            splitter.addEventListener("pointerdown", handlers.pointerDown);
            splitter.addEventListener("pointermove", handlers.pointerMove);
            splitter.addEventListener("pointerup", handlers.pointerUp);
            splitter.addEventListener("pointercancel", handlers.pointerUp);
            splitter.addEventListener("keydown", handlers.keyDown);
            splitter.style.touchAction = "none";
            contactPanelSplitterHandlers.set(splitter, handlers);
        },
        detachContactPanelSplitter(splitter) {
            const handlers = contactPanelSplitterHandlers.get(splitter);
            if (!handlers) {
                return;
            }

            splitter.removeEventListener("pointerdown", handlers.pointerDown);
            splitter.removeEventListener("pointermove", handlers.pointerMove);
            splitter.removeEventListener("pointerup", handlers.pointerUp);
            splitter.removeEventListener("pointercancel", handlers.pointerUp);
            splitter.removeEventListener("keydown", handlers.keyDown);
            splitter.style.touchAction = "";
            contactPanelSplitterHandlers.delete(splitter);
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