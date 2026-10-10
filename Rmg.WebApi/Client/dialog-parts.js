/** A button of a window's: its text, its title, what a click does, and its class, if any. */
export function button(text, title, action, className = "") {
    const element = document.createElement("button");
    element.type = "button";
    element.textContent = text;
    element.title = title;
    if (className) element.className = className;
    element.addEventListener("click", action);
    return element;
}

/** Saves a value as a JSON file of the name given. */
export function downloadJson(name, value) {
    const link = document.createElement("a");
    link.href = URL.createObjectURL(new Blob([JSON.stringify(value, null, 1)], { type: "application/json" }));
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href));
}

/** A hidden input of a genre's JSON file, which hands the text of a file chosen on; its click() asks for one. */
export function jsonFileInput(onText) {
    const file = document.createElement("input");
    file.type = "file";
    file.accept = "application/json,.json";
    file.hidden = true;
    file.setAttribute("aria-label", "Genre file");
    file.addEventListener("change", async () => {
        const chosen = file.files[0];
        file.value = "";
        if (!chosen) return;
        onText(await chosen.text());
    });
    return file;
}
